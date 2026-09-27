using System.Security.Cryptography;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Services;

public class AtsService : IAtsService
{
    private readonly IAtsRepository _repository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;
    private readonly IConfiguration _configuration;
    private readonly INotificacionesService _notificacionesService;
    private readonly string[] _logoPaths;

    public AtsService(
        IAtsRepository repository,
        IFileStorageService fileStorageService,
        IStorageContainerResolver containerResolver,
        IConfiguration configuration,
        INotificacionesService notificacionesService,
        IWebHostEnvironment env)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
        _configuration = configuration;
        _notificacionesService = notificacionesService;
        _logoPaths =
        [
            Path.Combine(env.WebRootPath, "images", "abril-logo.png"),
            Path.Combine(env.WebRootPath, "images", "logo-abril.jpg"),
            Path.Combine(env.ContentRootPath, "Templates", "logo-abril.jpg"),
        ];
    }

    public Task<int> ResolverWorkerId(int userId) => _repository.ResolverWorkerIdAsync(userId);

    public async Task<AtsInitDto> GetInit(int workerId)
    {
        var (puestoId, proyectoActualId) = await _repository.GetPuestoYProyectoActual(workerId);

        return new AtsInitDto
        {
            Proyectos = await _repository.GetProyectosActivos(),
            ProyectoActualId = proyectoActualId,
            PuestoId = puestoId,
            Pasos = await _repository.GetPasosParaPuesto(puestoId),
            Peligros = await _repository.GetPeligrosConRiesgos(),
            Epps = await _repository.GetEppActivos(),
            Herramientas = await _repository.GetHerramientasActivas(),
            Plantillas = await _repository.GetPlantillasActivas(),
            PlantillaSugeridaId = await _repository.GetPlantillaSugerida(puestoId),
            TieneConsentimiento = await _repository.TieneConsentimiento(workerId),
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

    public async Task<byte[]> GenerarPlantillaAutorizacionPdf(int workerId)
    {
        var (nombre, dni) = await _repository.GetNombreYDni(workerId);

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        return AtsAutorizacionPdfService.GenerarPdf(nombre, dni, logoBytes);
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
        var responsables = await _repository.GetResponsables(ats.ProyectoId);
        var callerEmail = await _repository.GetEmailCorporativoWorker(workerId);

        var esResidente = responsables.ResidenteWorkerId == workerId;
        var esSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);

        if (!esAdmin && ats.WorkerId != workerId && !esResidente && !esSsoma)
            throw new AbrilException("Este ATS no te pertenece.", 403);

        MarcarPermisos(ats, esResidente || esAdmin, esSsoma || esAdmin);
        await _repository.CompletarInfoPetar([ats]);
        return ats;
    }

    /// <summary>
    /// Sin filtro por proyecto/responsable a propósito: el Jefe/Administrador SSOMA debe poder
    /// ver TODOS los ATS de la empresa para saber cuáles faltan aprobar, no solo los de "sus"
    /// proyectos — <paramref name="esAdmin"/> también le habilita los botones de Autorizar/Visto
    /// Bueno en cualquier proyecto (ver MarcarPermisos), no solo verlos.
    /// </summary>
    public async Task<AtsListResponseDto> Listar(AtsFiltroDto filtro, int workerId, bool esAdmin)
    {
        var res = await _repository.Listar(filtro);
        var callerEmail = await _repository.GetEmailCorporativoWorker(workerId);

        // Cachea GetResponsables por proyecto — la lista puede traer el mismo proyecto muchas veces.
        var cache = new Dictionary<int, AtsResponsablesDto>();
        foreach (var ats in res.Data)
        {
            if (!cache.TryGetValue(ats.ProyectoId, out var responsables))
            {
                responsables = await _repository.GetResponsables(ats.ProyectoId);
                cache[ats.ProyectoId] = responsables;
            }

            var esResidente = responsables.ResidenteWorkerId == workerId;
            var esSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
            MarcarPermisos(ats, esResidente || esAdmin, esSsoma || esAdmin);
        }

        await _repository.CompletarInfoPetar(res.Data);
        return res;
    }

    private static void MarcarPermisos(AtsResponseDto ats, bool esResidente, bool esSsoma)
    {
        ats.PuedeAutorizar = esResidente && ats.Estado == "Firmado" && ats.AutorizaFirmaUrl == null;
        ats.PuedeVistoBuenoSsoma = esSsoma && ats.Estado == "Firmado" && ats.SsomaFirmaUrl == null;
    }

    public async Task Firmar(int id, int workerId, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        if (entidad.WorkerId != workerId)
            throw new AbrilException("Este ATS no te pertenece.", 403);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este ATS ya fue firmado.", 409);

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
        var responsables = await _repository.GetResponsables(entidad.ProyectoId);

        // El Jefe/Administrador SSOMA puede firmar cualquiera de los dos vistos en cualquier
        // proyecto — es quien pidió tener acceso total para destrabar lo que falte aprobar.
        if (!esAdmin)
        {
            if (rol == "Autoriza")
            {
                if (responsables.ResidenteWorkerId != workerId)
                    throw new AbrilException("Solo el Residente asignado a este proyecto puede firmar como Autoriza.", 403);
            }
            else
            {
                var email = await _repository.GetEmailCorporativoWorker(workerId);
                if (email == null || !responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                    throw new AbrilException("Solo el Coordinador SSOMA de este proyecto puede dar el Visto Bueno.", 403);
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

        var pdfBytes = AtsPdfService.Generar(ats, selfieBytes, firmaBytes, verificacionUrl, firmaAutorizaBytes, firmaSsomaBytes);
        var pdfHash = Convert.ToHexString(SHA256.HashData(pdfBytes));

        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(pdfBytes);
        var fileName = $"ats_{ats.Id}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.pdf";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);

        await _repository.GuardarPdf(id, urls[0], pdfHash);

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
