using System.Security.Cryptography;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Abril_Backend.Shared.Services.Residentes.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Services;

public class AtsService : IAtsService
{
    private readonly IAtsRepository _repository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;
    private readonly IConfiguration _configuration;
    private readonly INotificacionesService _notificacionesService;
    private readonly IResidenteProyectoResolver _residentes;
    private readonly string[] _logoPaths;

    public AtsService(
        IAtsRepository repository,
        IFileStorageService fileStorageService,
        IStorageContainerResolver containerResolver,
        IConfiguration configuration,
        INotificacionesService notificacionesService,
        IResidenteProyectoResolver residentes,
        IWebHostEnvironment env)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
        _configuration = configuration;
        _notificacionesService = notificacionesService;
        _residentes = residentes;
        _logoPaths =
        [
            Path.Combine(env.WebRootPath, "images", "abril-logo.png"),
            Path.Combine(env.WebRootPath, "images", "logo-abril.jpg"),
            Path.Combine(env.ContentRootPath, "Templates", "logo-abril.jpg"),
        ];
    }

    public Task<int> ResolverWorkerId(int userId) => _repository.ResolverWorkerIdAsync(userId);

    /// <summary>Cada _repository.Get* abre su propio DbContext (IDbContextFactory) — antes se
    /// esperaban una por una (9 round trips secuenciales a la BD cada vez que se abre "Nuevo
    /// ATS"). Como no comparten contexto, es seguro dispararlas todas en paralelo con
    /// Task.WhenAll: el tiempo total pasa a ser el de la más lenta, no la suma de las 9.</summary>
    public async Task<AtsInitDto> GetInit(int workerId)
    {
        var (puestoId, proyectoActualId) = await _repository.GetPuestoYProyectoActual(workerId);

        var proyectosTask = _repository.GetProyectosActivos();
        var pasosTask = _repository.GetPasosParaPuesto(puestoId);
        var peligrosTask = _repository.GetPeligrosConRiesgos();
        var eppsTask = _repository.GetEppActivos();
        var herramientasTask = _repository.GetHerramientasActivas();
        var plantillasTask = _repository.GetPlantillasActivas();
        var plantillaSugeridaTask = _repository.GetPlantillaSugerida(puestoId);
        var tieneConsentimientoTask = _repository.TieneConsentimiento(workerId);

        await Task.WhenAll(proyectosTask, pasosTask, peligrosTask, eppsTask, herramientasTask,
            plantillasTask, plantillaSugeridaTask, tieneConsentimientoTask);

        return new AtsInitDto
        {
            Proyectos = await proyectosTask,
            ProyectoActualId = proyectoActualId,
            PuestoId = puestoId,
            Pasos = await pasosTask,
            Peligros = await peligrosTask,
            Epps = await eppsTask,
            Herramientas = await herramientasTask,
            Plantillas = await plantillasTask,
            PlantillaSugeridaId = await plantillaSugeridaTask,
            TieneConsentimiento = await tieneConsentimientoTask,
        };
    }

    public async Task<AtsPasoDto> CrearPasoPersonalizado(int categoriaId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new AbrilException("Describe el paso.", 400);
        return await _repository.CrearPasoPersonalizado(categoriaId, texto.Trim());
    }

    public async Task<int> Crear(int workerId, AtsGuardarRequestDto dto)
    {
        await ExigirAutorizacionPermiso(workerId);
        Validar(dto);
        return await _repository.Crear(workerId, dto);
    }

    public async Task Editar(int id, int workerId, AtsGuardarRequestDto dto)
    {
        await ExigirAutorizacionPermiso(workerId);
        Validar(dto);
        await _repository.Editar(id, workerId, dto);
    }

    /// <summary>Sin la autorización de uso de firma digital e imagen firmada en físico (escaneada
    /// y subida por un Coordinador SSOMA) un trabajador no puede crear ni editar un ATS — mismo
    /// principio que el gate de enrolamiento de Arquitectura Comercial (SSO-FO-150), aplicado
    /// aquí a la creación del ATS en vez de al enrolamiento biométrico: antes de capturar su
    /// selfie/geolocalización/firma, el trabajador debe haber autorizado por escrito ese uso.
    /// GetInit() NO lleva este gate a propósito: esa misma acción la usa también la pantalla de
    /// administración de plantillas/pasos, que cualquier coordinador debe poder abrir sin tener
    /// él mismo esa autorización subida.</summary>
    private async Task ExigirAutorizacionPermiso(int workerId)
    {
        if (!await _repository.TieneAutorizacionPermiso(workerId))
            throw new AbrilException(
                "Aún no puedes crear un ATS: falta que tu Coordinador SSOMA suba tu autorización " +
                "firmada de uso de firma digital e imagen. Coordina con él para que la suba y podrás continuar.", 403);
    }

    public Task<bool> TieneAutorizacionPermiso(int workerId) => _repository.TieneAutorizacionPermiso(workerId);

    public Task<List<AtsAutorizacionTrabajadorDto>> GetTrabajadoresParaAutorizacion() => _repository.GetTrabajadoresParaAutorizacion();

    public async Task SubirAutorizacionPermiso(int workerId, Stream archivo, string nombreArchivo, int subidoPorUserId)
    {
        var container = _containerResolver.GetAtsContainerName();
        var fileName = $"autorizacion_{workerId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(nombreArchivo)}";
        var urls = await _fileStorageService.UploadFilesAsync([(archivo, fileName)], container);
        await _repository.SubirAutorizacionPermiso(workerId, urls[0], subidoPorUserId);
    }

    public async Task CapturarFirmaDigitalAutorizacion(int workerId, AtsAutorizacionFirmaDigitalRequestDto body, int capturadoPorUserId)
    {
        if (string.IsNullOrWhiteSpace(body.FirmaBase64))
            throw new AbrilException("La firma es obligatoria.", 400);

        var firmaBytes = FirmaImagenHelper.DecodePng(body.FirmaBase64);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("autorizacion-firma-digital", workerId, firmaBytes, "png");

        await _repository.CapturarFirmaDigitalAutorizacion(workerId, firmaUrl, firmaHash, capturadoPorUserId);
    }

    public Task<(string? FirmaDigitalUrl, string? Nombre, string? Dni)> GetFirmaDigitalAutorizacion(int workerId)
        => _repository.GetFirmaDigitalAutorizacion(workerId);

    /// <summary>Descarga la imagen de la firma digital autorizada YA capturada de un trabajador
    /// (misma que usa GenerarPlantillaAutorizacionPdf) para que el frontend la reutilice al firmar
    /// un ATS o al Autorizar/dar Visto Bueno — el navegador no puede traer el blob directo (storage
    /// privado), así que esto pasa por el backend con las credenciales del servidor.</summary>
    public async Task<byte[]?> GetFirmaDigitalAutorizacionImagen(int workerId)
    {
        var (firmaDigitalUrl, _, _) = await _repository.GetFirmaDigitalAutorizacion(workerId);
        return firmaDigitalUrl == null ? null : await DescargarBytes(firmaDigitalUrl);
    }

    public async Task<byte[]> GenerarPlantillaAutorizacionPdf(int workerId)
    {
        var (firmaDigitalUrl, nombre, dni) = await _repository.GetFirmaDigitalAutorizacion(workerId);
        if (firmaDigitalUrl == null)
            throw new AbrilException("Primero debes capturar la firma digital del trabajador — recién ahí se habilita descargar la plantilla.", 400);

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        byte[]? firmaDigitalBytes = await DescargarBytes(firmaDigitalUrl);

        return AtsAutorizacionPdfService.GenerarPdf(nombre ?? string.Empty, dni, logoBytes, firmaDigitalBytes);
    }

    private static readonly HashSet<string> NivelesValidos = ["A", "M", "B"];

    private static void Validar(AtsGuardarRequestDto dto)
    {
        if (dto.ProyectoId <= 0)
            throw new AbrilException("Selecciona el proyecto.", 400);
        if (string.IsNullOrWhiteSpace(dto.Actividad))
            throw new AbrilException("Describe la actividad a realizar.", 400);
        if (dto.Riesgos.Count == 0)
            throw new AbrilException("Identifica al menos un peligro con su riesgo asociado.", 400);

        foreach (var r in dto.Riesgos)
        {
            if (!NivelesValidos.Contains(r.RiesgoBase))
                throw new AbrilException("El riesgo base debe ser Alto, Medio o Bajo.", 400);
            if (string.IsNullOrWhiteSpace(r.Controles))
                throw new AbrilException("Todo riesgo identificado debe tener sus controles definidos.", 400);
            if (!NivelesValidos.Contains(r.RiesgoResidual))
                throw new AbrilException("El riesgo residual (después de los controles) debe ser Alto, Medio o Bajo.", 400);
        }
    }

    public async Task<AtsResponseDto> GetPorId(int id, int callerUserId, int workerId, bool esAdmin)
    {
        var ats = await _repository.GetPorId(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        var (esAutoriza, esSsoma) = await PuedeAutorizarYVistoBueno(ats, callerUserId, workerId, esAdmin);

        if (!esAdmin && ats.WorkerId != workerId && !esAutoriza && !esSsoma)
            throw new AbrilException("Este ATS no te pertenece.", 403);

        MarcarPermisos(ats, esAutoriza, esSsoma);
        await _repository.CompletarInfoPetar([ats], new() { [ats.Id] = (esAutoriza, esSsoma) });
        return ats;
    }

    /// <summary>Autorizar = Residente asignado al proyecto (Project.ResidenteWorkersId) O el
    /// Ingeniero/Arquitecto de Producción vinculado actualmente a ese proyecto (por puesto, no
    /// hay campo dedicado) O el Jefe/Administrador SSOMA. Visto Bueno SSOMA = el correo de
    /// Coordinador SSOMA configurado en el proyecto O cualquier prevencionista de Abril (puesto
    /// "Prevencionista", cualquier proyecto) O el Jefe/Administrador SSOMA. Nadie se autovalida su
    /// propio ATS — EXCEPTO el Jefe/Administrador SSOMA, que pidió acceso total sin bloqueo ni
    /// siquiera sobre lo suyo (es la última instancia, no hay a quién más escalar).
    /// El Residente se reconoce por persona (IResidenteProyectoResolver), no por la ficha del
    /// caller: una persona puede tener varias fichas por reingreso y el proyecto guarda una.</summary>
    private async Task<(bool EsAutoriza, bool EsSsoma)> PuedeAutorizarYVistoBueno(AtsResponseDto ats, int callerUserId, int workerId, bool esAdmin)
    {
        if (ats.WorkerId == workerId && !esAdmin) return (false, false);
        if (ats.WorkerId == workerId && esAdmin) return (true, true);

        var responsables = await _repository.GetResponsables(ats.ProyectoId);
        var callerEmail = await _repository.GetEmailCorporativoWorker(workerId);

        var esResidente = await _residentes.EsResidenteDelProyectoAsync(callerUserId, ats.ProyectoId);
        var esProduccion = !esResidente && await _repository.EsProduccionDeProyecto(workerId, ats.ProyectoId);
        var esCoordSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
        var esPrevencionista = !esCoordSsoma && await _repository.EsPrevencionistaAbril(workerId);

        return (esResidente || esProduccion || esAdmin, esCoordSsoma || esPrevencionista || esAdmin);
    }

    /// <summary>
    /// Un trabajador normal solo ve los ATS de SU proyecto actual (evita que vea, y sobre todo
    /// que le salgan botones sobre, ATS de otros proyectos que no le corresponden). El Jefe/
    /// Administrador SSOMA sigue viendo TODOS, sin filtrar — necesita saber qué falta aprobar en
    /// cualquier proyecto, y esAdmin también lo habilita a Autorizar/Visto Bueno en cualquiera
    /// (ver PuedeAutorizarYVistoBueno).
    /// </summary>
    public async Task<AtsListResponseDto> Listar(AtsFiltroDto filtro, int callerUserId, int workerId, bool esAdmin)
    {
        if (!esAdmin)
        {
            var (_, proyectoActualId) = await _repository.GetPuestoYProyectoActual(workerId);
            filtro.ProyectoId = proyectoActualId;
        }

        var res = await _repository.Listar(filtro);

        // El "caller" (quien pide la lista) es el mismo en las 20 filas de la página — antes se
        // repetían GetResponsables/EsProduccionDeProyecto/EsPrevencionista/GetEmailCorporativo
        // por CADA fila (hasta 80 queries extra por página). Lo que depende solo del caller se
        // calcula una vez; lo que depende del proyecto se cachea por proyecto, no por fila.
        var callerEmail = await _repository.GetEmailCorporativoWorker(workerId);
        var esPrevencionistaCaller = await _repository.EsPrevencionistaAbril(workerId);
        var obrasDondeEsResidente = (await _residentes.ProyectosDelResidenteAsync(callerUserId)).ToHashSet();
        var responsablesPorProyecto = new Dictionary<int, AtsResponsablesDto>();
        var esProduccionPorProyecto = new Dictionary<int, bool>();

        var permisosPorAtsId = new Dictionary<int, (bool, bool)>();
        foreach (var ats in res.Data)
        {
            if (!responsablesPorProyecto.TryGetValue(ats.ProyectoId, out var responsables))
            {
                responsables = await _repository.GetResponsables(ats.ProyectoId);
                responsablesPorProyecto[ats.ProyectoId] = responsables;
            }
            if (!esProduccionPorProyecto.TryGetValue(ats.ProyectoId, out var esProduccion))
            {
                esProduccion = await _repository.EsProduccionDeProyecto(workerId, ats.ProyectoId);
                esProduccionPorProyecto[ats.ProyectoId] = esProduccion;
            }

            var esResidente = obrasDondeEsResidente.Contains(ats.ProyectoId);
            var (esAutoriza, esSsoma) = CalcularPermisos(ats, workerId, esAdmin, responsables, callerEmail, esResidente, esProduccion, esPrevencionistaCaller);
            MarcarPermisos(ats, esAutoriza, esSsoma);
            permisosPorAtsId[ats.Id] = (esAutoriza, esSsoma);
        }

        await _repository.CompletarInfoPetar(res.Data, permisosPorAtsId);
        return res;
    }

    private static (bool EsAutoriza, bool EsSsoma) CalcularPermisos(
        AtsResponseDto ats, int workerId, bool esAdmin, AtsResponsablesDto responsables,
        string? callerEmail, bool esResidente, bool esProduccion, bool esPrevencionistaCaller)
    {
        if (ats.WorkerId == workerId) return esAdmin ? (true, true) : (false, false);

        var esProduccionEfectivo = !esResidente && esProduccion;
        var esCoordSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
        var esPrevencionista = !esCoordSsoma && esPrevencionistaCaller;

        return (esResidente || esProduccionEfectivo || esAdmin, esCoordSsoma || esPrevencionista || esAdmin);
    }

    private static void MarcarPermisos(AtsResponseDto ats, bool esAutoriza, bool esSsoma)
    {
        ats.PuedeAutorizar = esAutoriza && ats.Estado == "Firmado" && ats.AutorizaFirmaUrl == null;
        ats.PuedeVistoBuenoSsoma = esSsoma && ats.Estado == "Firmado" && ats.SsomaFirmaUrl == null;
    }

    public async Task Firmar(int id, int workerId, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        if (entidad.WorkerId != workerId)
            throw new AbrilException("Este ATS no te pertenece.", 403);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este ATS ya fue firmado.", 409);

        // OJO: el PETAR solo se puede generar sobre un ATS ya Firmado (PetarService.Crear lo exige),
        // así que NO se puede bloquear la firma acá — sería un candado imposible de abrir. En vez de
        // eso, el frontend redirige a "Generar PETAR" automáticamente apenas termina de firmar
        // (ver AtsNuevo.firmar() en el frontend).
        if (string.IsNullOrWhiteSpace(body.SelfieBase64))
            throw new AbrilException("La selfie es obligatoria para firmar el ATS.", 400);
        if (string.IsNullOrWhiteSpace(body.FirmaBase64))
            throw new AbrilException("La firma es obligatoria.", 400);

        if (!await _repository.TieneConsentimiento(workerId))
        {
            if (!body.AceptaConsentimiento)
                throw new AbrilException("Debes aceptar el uso de tu imagen y geolocalización para firmar el ATS digital.", 400);
            await _repository.RegistrarConsentimiento(workerId, ipOrigen);
        }

        var (selfieUrl, selfieHash) = await SubirImagenConHash("selfie", workerId, body.SelfieBase64);
        var selfieDuplicada = await _repository.ExisteSelfieHash(selfieHash);

        var firmaBytes = FirmaImagenHelper.DecodePng(body.FirmaBase64);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("firma", workerId, firmaBytes, "png");

        var horaServidor = DateTime.UtcNow;
        await _repository.Firmar(entidad, selfieUrl, selfieHash, firmaUrl, firmaHash, horaServidor, body, ipOrigen, userAgent);

        if (selfieDuplicada)
        {
            var hashAnterior = await _repository.GetUltimoHashAuditLog(id);
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{hashAnterior}|SelfieDuplicada|{id}|{DateTime.UtcNow:O}")));
            await _repository.AgregarAuditLog(id, "SelfieDuplicada", null, ipOrigen,
                "La selfie ya había sido usada en otro ATS — revisar manualmente.", hashAnterior ?? string.Empty, hash);
        }

        await AvisarResponsables(id, entidad.ProyectoId);
    }

    /// <summary>
    /// Avisa al Residente y al/los Coordinador(es) SSOMA del proyecto apenas el ejecutante firma
    /// — por la campanita de notificaciones, NO por correo (un correo por cada ATS del día satura
    /// la bandeja; la campanita ya trae su propio contador de no leídas y no genera spam). El
    /// botón "Autorizar"/"Visto Bueno SSOMA" solo se habilita para ELLOS en la lista de ATS (ver
    /// MarcarPermisos), nunca para cualquier otro usuario aunque tenga acceso al módulo.
    /// Si el proyecto no tiene Residente o correo SSOMA cargado, o esos correos no tienen usuario
    /// en el sistema, CrearPorCorreosAsync simplemente no crea nada — no bloquea la firma del
    /// ejecutante, que ya quedó registrada.
    /// </summary>
    private async Task AvisarResponsables(int atsId, int proyectoId)
    {
        var responsables = await _repository.GetResponsables(proyectoId);

        var destinatarios = new List<string>();
        if (!string.IsNullOrWhiteSpace(responsables.ResidenteEmail)) destinatarios.Add(responsables.ResidenteEmail);
        destinatarios.AddRange(responsables.SsomaEmails);
        if (destinatarios.Count == 0) return;

        try
        {
            await _notificacionesService.CrearPorCorreosAsync(
                "ATS_PENDIENTE_FIRMA",
                destinatarios,
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "ATS pendiente de tu firma",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = "Un ATS ya fue firmado por el ejecutante y está pendiente de tu Autorización/Visto Bueno.",
                    Referencia = $"/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort: la firma del ejecutante ya quedó guardada, el aviso es una comodidad */ }
    }

    public async Task FirmarAutorizacion(int id, int callerUserId, bool esAdmin, AtsFirmarVistoRequestDto body)
        => await FirmarVistoComun(id, callerUserId, esAdmin, body, "Autoriza");

    public async Task FirmarVistoSsoma(int id, int callerUserId, bool esAdmin, AtsFirmarVistoRequestDto body)
        => await FirmarVistoComun(id, callerUserId, esAdmin, body, "Ssoma");

    private async Task FirmarVistoComun(int id, int callerUserId, bool esAdmin, AtsFirmarVistoRequestDto body, string rol)
    {
        if (string.IsNullOrWhiteSpace(body.FirmaBase64))
            throw new AbrilException("La firma es obligatoria.", 400);

        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);

        // Nadie se autovalida su propio ATS — excepto el Jefe/Administrador SSOMA (ver
        // PuedeAutorizarYVistoBueno): pidió acceso total, incluido sobre lo suyo.
        if (entidad.WorkerId == workerId && !esAdmin)
            throw new AbrilException("No puedes dar visto bueno a tu propio ATS.", 403);

        // El Jefe/Administrador SSOMA puede firmar cualquiera de los dos vistos en cualquier
        // proyecto — es quien pidió tener acceso total para destrabar lo que falte aprobar.
        if (!esAdmin)
        {
            var responsables = await _repository.GetResponsables(entidad.ProyectoId);
            if (rol == "Autoriza")
            {
                var esResidente = await _residentes.EsResidenteDelProyectoAsync(callerUserId, entidad.ProyectoId);
                var esProduccion = !esResidente && await _repository.EsProduccionDeProyecto(workerId, entidad.ProyectoId);
                if (!esResidente && !esProduccion)
                    throw new AbrilException("Solo el Residente o el Ing./Arq. de Producción de este proyecto pueden firmar como Autoriza.", 403);
            }
            else
            {
                var email = await _repository.GetEmailCorporativoWorker(workerId);
                var esCoordSsoma = email != null && responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
                var esPrevencionista = !esCoordSsoma && await _repository.EsPrevencionistaAbril(workerId);
                if (!esCoordSsoma && !esPrevencionista)
                    throw new AbrilException("Solo el Coordinador SSOMA de este proyecto o un prevencionista de Abril pueden dar el Visto Bueno.", 403);
            }
        }

        var (nombre, cargo) = await _repository.GetNombreYCargo(workerId);

        var firmaBytes = FirmaImagenHelper.DecodePng(body.FirmaBase64);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes(rol.ToLower(), workerId, firmaBytes, "png");

        await _repository.FirmarVisto(id, rol, workerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow);
    }

    public async Task<byte[]> GenerarPdf(int id)
    {
        var ats = await _repository.GetPorId(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        if (ats.Estado != "Firmado")
            throw new AbrilException("Solo se puede exportar un ATS ya firmado.", 409);

        byte[]? selfieBytes = ats.SelfieUrl != null ? await DescargarBytes(ats.SelfieUrl) : null;
        byte[]? firmaBytes = ats.FirmaUrl != null ? await DescargarBytes(ats.FirmaUrl) : null;
        byte[]? firmaAutorizaBytes = ats.AutorizaFirmaUrl != null ? await DescargarBytes(ats.AutorizaFirmaUrl) : null;
        byte[]? firmaSsomaBytes = ats.SsomaFirmaUrl != null ? await DescargarBytes(ats.SsomaFirmaUrl) : null;

        // Ruta PÚBLICA (fuera del shell logueado) — un inspector externo no tiene ni debe
        // necesitar una cuenta de Abril para verificar el documento.
        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "https://intranet.abril.pe";
        var verificacionUrl = $"{baseUrl}/ats-verificar/{ats.Id}?hash={(ats.PdfHash != null ? ats.PdfHash[..Math.Min(12, ats.PdfHash.Length)] : "")}";

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        var pdfBytes = AtsPdfService.Generar(ats, selfieBytes, firmaBytes, verificacionUrl, firmaAutorizaBytes, firmaSsomaBytes, logoBytes);

        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(pdfBytes);
        var fileName = $"ats_{ats.Id}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.pdf";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);

        // PdfHash ya quedó fijado al firmar (ver AtsRepository.Firmar/ComputeHashVerificacion) —
        // acá solo se actualiza la URL del archivo. Antes este método recalculaba el hash a
        // partir de los bytes del PDF recién generado, que ya llevaban impreso el QR con el
        // hash de la exportación ANTERIOR: la verificación pública nunca podía coincidir.
        await _repository.GuardarPdf(id, urls[0], ats.PdfHash ?? string.Empty);

        return pdfBytes;
    }

    public async Task<AtsVerificacionPublicaDto> VerificarPublico(int id, string? hash)
    {
        var ats = await _repository.GetPorId(id);
        if (ats == null || ats.Estado != "Firmado")
            return new AtsVerificacionPublicaDto { Encontrado = false, Valido = false };

        var prefijoEsperado = ats.PdfHash?[..Math.Min(12, ats.PdfHash.Length)];
        var valido = !string.IsNullOrWhiteSpace(hash) && string.Equals(hash, prefijoEsperado, StringComparison.OrdinalIgnoreCase);

        return new AtsVerificacionPublicaDto
        {
            Encontrado = true,
            Valido = valido,
            WorkerNombre = ats.WorkerNombre,
            ProyectoNombre = ats.ProyectoNombre,
            Actividad = ats.Actividad,
            Fecha = ats.Fecha,
            HoraServidorFirma = ats.HoraServidorFirma,
            Estado = ats.Estado,
        };
    }

    public Task<List<AtsPlantillaDto>> GetPlantillas() => _repository.GetPlantillasActivas();
    public Task<List<AtsPuestoDto>> GetPuestos() => _repository.GetPuestos();
    public Task<List<AtsPasoPuestoDto>> GetPasoPuestoMapeo() => _repository.GetPasoPuestoMapeo();
    public Task SetPasoPuestos(int pasoId, List<int> puestoIds) => _repository.SetPasoPuestos(pasoId, puestoIds);
    public Task<List<AtsPlantillaPuestoDto>> GetPlantillaPuestoMapeo() => _repository.GetPlantillaPuestoMapeo();
    public Task SetPlantillaPuestos(int plantillaId, List<int> puestoIds) => _repository.SetPlantillaPuestos(plantillaId, puestoIds);

    public async Task<int> CrearPlantilla(AtsPlantillaGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("La plantilla necesita un nombre.", 400);
        return await _repository.CrearPlantilla(dto);
    }

    public async Task EditarPlantilla(int id, AtsPlantillaGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("La plantilla necesita un nombre.", 400);
        await _repository.EditarPlantilla(id, dto);
    }

    public Task DesactivarPlantilla(int id) => _repository.DesactivarPlantilla(id);

    public Task<List<AtsPeligroDto>> GetPeligrosConRiesgos() => _repository.GetPeligrosConRiesgos();

    public Task SetRiesgoRequierePetar(int riesgoId, bool requierePetar) => _repository.SetRiesgoRequierePetar(riesgoId, requierePetar);

    // ── Actividades/pasos por plantilla ──────────────────────────────────

    public Task<List<AtsPlantillaActividadDto>> GetActividadesDePlantilla(int plantillaId) => _repository.GetActividadesDePlantilla(plantillaId);

    public async Task<int> CrearActividad(int plantillaId, AtsPlantillaActividadGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("La actividad necesita un texto.", 400);
        return await _repository.CrearActividad(plantillaId, dto.Texto.Trim());
    }

    public async Task EditarActividad(int actividadId, AtsPlantillaActividadGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("La actividad necesita un texto.", 400);
        await _repository.EditarActividad(actividadId, dto.Texto.Trim());
    }

    public Task EliminarActividad(int actividadId) => _repository.EliminarActividad(actividadId);

    public async Task<int> CrearPaso(int actividadId, AtsPlantillaPasoGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("El paso necesita un texto.", 400);
        return await _repository.CrearPaso(actividadId, dto.Texto.Trim());
    }

    public async Task EditarPaso(int pasoId, AtsPlantillaPasoGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("El paso necesita un texto.", 400);
        await _repository.EditarPaso(pasoId, dto.Texto.Trim());
    }

    public Task EliminarPaso(int pasoId) => _repository.EliminarPaso(pasoId);

    public Task SetActividadPeligros(int actividadId, AtsPlantillaActividadPeligrosRequestDto dto) => _repository.SetActividadPeligros(actividadId, dto.PeligroIds);

    // ── Controles sugeridos por riesgo ───────────────────────────────────

    public Task<List<AtsRiesgoConControlesDto>> GetRiesgosConControles() => _repository.GetRiesgosConControles();

    public async Task<int> CrearControl(int riesgoId, AtsRiesgoControlGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("El control necesita un texto.", 400);
        return await _repository.CrearControl(riesgoId, dto.Texto.Trim(), dto.Tipo);
    }

    public async Task EditarControl(int controlId, AtsRiesgoControlGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("El control necesita un texto.", 400);
        await _repository.EditarControl(controlId, dto.Texto.Trim(), dto.Tipo);
    }

    public Task EliminarControl(int controlId) => _repository.EliminarControl(controlId);

    private async Task<(string Url, string Hash)> SubirImagenConHash(string prefijo, int workerId, string base64)
    {
        byte[] bytes;
        try
        {
            var payload = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw new AbrilException("La imagen enviada no es válida.", 400);
        }

        if (bytes.Length == 0)
            throw new AbrilException("La imagen enviada está vacía.", 400);

        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var url = await SubirBytes(prefijo, workerId, bytes, "jpg");
        return (url, hash);
    }

    private async Task<string> SubirBytes(string prefijo, int workerId, byte[] bytes, string ext)
    {
        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(bytes);
        var fileName = $"{prefijo}_{workerId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.{ext}";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);
        return urls[0];
    }

    private static async Task<byte[]?> DescargarBytes(string url)
    {
        try
        {
            using var http = new HttpClient();
            return await http.GetByteArrayAsync(url);
        }
        catch
        {
            return null;
        }
    }
}
