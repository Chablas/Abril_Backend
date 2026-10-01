using System.Security.Cryptography;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Services;

public class PetarService : IPetarService
{
    private readonly IPetarRepository _repository;
    // Reusa la resolución de Residente/SSOMA por proyecto y utilidades de identidad ya construidas
    // para ATS — un PETAR nace de un ATS y comparte exactamente los mismos responsables.
    private readonly IAtsRepository _atsRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;
    private readonly IConfiguration _configuration;
    private readonly INotificacionesService _notificacionesService;
    private readonly string[] _logoPaths;

    public PetarService(
        IPetarRepository repository,
        IAtsRepository atsRepository,
        IFileStorageService fileStorageService,
        IStorageContainerResolver containerResolver,
        IConfiguration configuration,
        INotificacionesService notificacionesService,
        IWebHostEnvironment env)
    {
        _repository = repository;
        _atsRepository = atsRepository;
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

    public Task<int> ResolverWorkerId(int userId) => _atsRepository.ResolverWorkerIdAsync(userId);

    public Task<List<PetarTipoDto>> GetTiposCatalogo() => _repository.GetTiposConItems();

    public async Task<PetarInitDto> GetInit(int atsId)
    {
        var (_, lugar, actividad, estadoAts) = await _repository.GetDatosAts(atsId);
        if (estadoAts != "Firmado")
            throw new AbrilException("El ATS debe estar firmado antes de generar un PETAR.", 409);

        return new PetarInitDto
        {
            AtsId = atsId,
            AtsLugar = lugar,
            AtsActividad = actividad,
            Tipos = await _repository.GetTiposConItems(),
        };
    }

    public async Task<int> Crear(int workerId, PetarGuardarRequestDto dto)
    {
        Validar(dto);
        return await _repository.Crear(workerId, dto);
    }

    public async Task Editar(int id, int workerId, PetarGuardarRequestDto dto)
    {
        Validar(dto);
        await _repository.Editar(id, workerId, dto);
    }

    private static void Validar(PetarGuardarRequestDto dto)
    {
        if (dto.AtsId <= 0)
            throw new AbrilException("Falta el ATS de origen.", 400);
        if (dto.TipoId <= 0)
            throw new AbrilException("Selecciona el tipo de trabajo de alto riesgo.", 400);
        if (string.IsNullOrWhiteSpace(dto.DescripcionTrabajo))
            throw new AbrilException("Describe el trabajo a realizar.", 400);
        if (dto.Respuestas.Count == 0)
            throw new AbrilException("Completa el checklist de verificación.", 400);
        if (dto.Respuestas.Any(r => r.Respuesta == "NO"))
            throw new AbrilException(
                "Hay ítems del checklist marcados como NO cumplidos — el trabajo no puede iniciar hasta corregirlos.", 400);
    }

    public async Task<PetarResponseDto> GetPorId(int id, int workerId, bool esAdmin)
    {
        var petar = await _repository.GetPorId(id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        var responsables = await _atsRepository.GetResponsables(petar.ProyectoId);
        var callerEmail = await _atsRepository.GetEmailCorporativoWorker(workerId);

        var esResidente = responsables.ResidenteWorkerId == workerId;
        var esSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);

        if (!esAdmin && petar.WorkerId != workerId && !esResidente && !esSsoma)
            throw new AbrilException("Este PETAR no te pertenece.", 403);

        MarcarPermisos(petar, workerId, esResidente || esAdmin, esSsoma || esAdmin);
        return petar;
    }

    public async Task<PetarListResponseDto> Listar(PetarFiltroDto filtro, int workerId, bool esAdmin)
    {
        var res = await _repository.Listar(filtro);
        var callerEmail = await _atsRepository.GetEmailCorporativoWorker(workerId);

        var cache = new Dictionary<int, Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos.AtsResponsablesDto>();
        foreach (var petar in res.Data)
        {
            if (!cache.TryGetValue(petar.ProyectoId, out var responsables))
            {
                responsables = await _atsRepository.GetResponsables(petar.ProyectoId);
                cache[petar.ProyectoId] = responsables;
            }

            var esResidente = responsables.ResidenteWorkerId == workerId;
            var esSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
            MarcarPermisos(petar, workerId, esResidente || esAdmin, esSsoma || esAdmin);
        }

        return res;
    }

    private static void MarcarPermisos(PetarResponseDto p, int workerId, bool esResidente, bool esSsoma)
    {
        p.PuedeFirmarSupervisor = esResidente && p.FirmaUrl != null && p.SupervisorFirmaUrl == null && p.Estado == "Borrador";
        p.PuedeFirmarSsoma = esSsoma && p.FirmaUrl != null && p.SsomaFirmaUrl == null && p.Estado == "Borrador";
        p.PuedeCerrar = p.WorkerId == workerId && p.Estado == "Firmado";
    }

    public async Task Firmar(int id, int workerId, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        if (entidad.WorkerId != workerId)
            throw new AbrilException("Este PETAR no te pertenece.", 403);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este PETAR ya fue firmado.", 409);

        if (string.IsNullOrWhiteSpace(body.SelfieBase64))
            throw new AbrilException("La selfie es obligatoria para firmar el PETAR.", 400);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);

        // El consentimiento de uso de imagen/geolocalización ya se dio al firmar el primer ATS —
        // no se vuelve a pedir acá, es el mismo trabajador con la misma autorización vigente.

        var (selfieUrl, selfieHash) = await SubirImagenConHash("selfie", workerId, body.SelfieBase64);
        var selfieDuplicada = await _repository.ExisteSelfieHash(selfieHash);

        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("firma", workerId, firmaBytes, "png");

        var horaServidor = DateTime.UtcNow;
        await _repository.Firmar(entidad, selfieUrl, selfieHash, firmaUrl, firmaHash, horaServidor, body, ipOrigen, userAgent);

        if (selfieDuplicada)
        {
            var hashAnterior = await _repository.GetUltimoHashAuditLog(id);
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{hashAnterior}|SelfieDuplicada|{id}|{DateTime.UtcNow:O}")));
            await _repository.AgregarAuditLog(id, "SelfieDuplicada", null, ipOrigen,
                "La selfie ya había sido usada en otro PETAR/ATS — revisar manualmente.", hashAnterior ?? string.Empty, hash);
        }

        await AvisarResponsables(entidad.ProyectoId);
    }

    private async Task AvisarResponsables(int proyectoId)
    {
        var responsables = await _atsRepository.GetResponsables(proyectoId);
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
                    Titulo = "PETAR de alto riesgo pendiente de tu firma",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = "Un PETAR ya fue firmado por el ejecutante y está pendiente de Supervisor/Visto Bueno SSOMA antes de poder iniciar el trabajo.",
                    Referencia = "/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort */ }
    }

    public async Task FirmarSupervisor(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => await FirmarVistoComun(id, callerUserId, esAdmin, body, "Supervisor");

    public async Task FirmarVistoSsoma(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => await FirmarVistoComun(id, callerUserId, esAdmin, body, "Ssoma");

    private async Task FirmarVistoComun(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body, string rol)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        var workerId = await _atsRepository.ResolverWorkerIdAsync(callerUserId);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);
        var responsables = await _atsRepository.GetResponsables(entidad.ProyectoId);

        if (!esAdmin)
        {
            if (rol == "Supervisor")
            {
                if (responsables.ResidenteWorkerId != workerId)
                    throw new AbrilException("Solo el Residente/Supervisor asignado a este proyecto puede firmar este visto.", 403);
            }
            else
            {
                var email = await _atsRepository.GetEmailCorporativoWorker(workerId);
                if (email == null || !responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                    throw new AbrilException("Solo el Coordinador SSOMA de este proyecto puede dar el Visto Bueno.", 403);
            }
        }

        var (nombre, cargo) = await _atsRepository.GetNombreYCargo(workerId);

        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes(rol.ToLower(), workerId, firmaBytes, "png");

        // Si la otra firma ya existía, esta es la que completa las 3 y autoriza el inicio del
        // trabajo — el ejecutante es quien más necesita saberlo (está esperando para empezar).
        var quedaAutorizado = rol == "Supervisor" ? entidad.SsomaFirmaUrl != null : entidad.SupervisorFirmaUrl != null;

        await _repository.FirmarVisto(id, rol, workerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow);

        if (quedaAutorizado)
            await AvisarEjecutanteAutorizado(entidad);
    }

    private async Task AvisarEjecutanteAutorizado(SsPetar entidad)
    {
        var email = await _atsRepository.GetEmailCorporativoWorker(entidad.WorkerId);
        if (string.IsNullOrWhiteSpace(email)) return;

        try
        {
            await _notificacionesService.CrearPorCorreosAsync(
                "PETAR_AUTORIZADO",
                [email],
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "Tu PETAR fue autorizado",
                    Subtitulo = entidad.DescripcionTrabajo,
                    Descripcion = "Supervisor y SSOMA ya firmaron — ya puedes iniciar el trabajo de alto riesgo.",
                    Referencia = "/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort */ }
    }

    public async Task Cerrar(int id, int workerId, PetarCerrarRequestDto body)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("PETAR no encontrado.", 404);

        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("cierre", workerId, firmaBytes, "png");

        await _repository.Cerrar(id, workerId, firmaUrl, firmaHash, body.Observaciones, DateTime.UtcNow);
        await AvisarCierre(entidad.ProyectoId);
    }

    private async Task AvisarCierre(int proyectoId)
    {
        var responsables = await _atsRepository.GetResponsables(proyectoId);
        var destinatarios = new List<string>();
        if (!string.IsNullOrWhiteSpace(responsables.ResidenteEmail)) destinatarios.Add(responsables.ResidenteEmail);
        destinatarios.AddRange(responsables.SsomaEmails);
        if (destinatarios.Count == 0) return;

        try
        {
            await _notificacionesService.CrearPorCorreosAsync(
                "PETAR_CERRADO",
                destinatarios,
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "PETAR cerrado",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = "El ejecutante cerró el PETAR — el trabajo de alto riesgo asociado ha finalizado.",
                    Referencia = "/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort */ }
    }

    public async Task<byte[]> GenerarPdf(int id)
    {
        var petar = await _repository.GetPorId(id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        // OJO: Estado se queda en "Borrador" hasta que Supervisor Y SSOMA también firman (ver
        // FirmarVisto) — NO cambia solo porque el ejecutante ya firmó. El gate real es si ya
        // existe la firma del ejecutante, no el Estado.
        if (petar.FirmaUrl == null)
            throw new AbrilException("Solo se puede exportar un PETAR con al menos la firma del ejecutante.", 409);

        byte[]? selfieBytes = petar.SelfieUrl != null ? await DescargarBytes(petar.SelfieUrl) : null;
        byte[]? firmaBytes = petar.FirmaUrl != null ? await DescargarBytes(petar.FirmaUrl) : null;
        byte[]? firmaSupervisorBytes = petar.SupervisorFirmaUrl != null ? await DescargarBytes(petar.SupervisorFirmaUrl) : null;
        byte[]? firmaSsomaBytes = petar.SsomaFirmaUrl != null ? await DescargarBytes(petar.SsomaFirmaUrl) : null;

        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "https://intranet.abril.pe";
        var verificacionUrl = $"{baseUrl}/petar-verificar/{petar.Id}?hash={(petar.PdfHash != null ? petar.PdfHash[..Math.Min(12, petar.PdfHash.Length)] : "")}";

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        var pdfBytes = PetarPdfService.Generar(petar, selfieBytes, firmaBytes, verificacionUrl, firmaSupervisorBytes, firmaSsomaBytes, logoBytes);
        var pdfHash = Convert.ToHexString(SHA256.HashData(pdfBytes));

        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(pdfBytes);
        var fileName = $"petar_{petar.Id}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.pdf";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);

        await _repository.GuardarPdf(id, urls[0], pdfHash);
        return pdfBytes;
    }

    public async Task<PetarVerificacionPublicaDto> VerificarPublico(int id, string? hash)
    {
        var petar = await _repository.GetPorId(id);
        if (petar == null || petar.Estado == "Borrador")
            return new PetarVerificacionPublicaDto { Encontrado = false, Valido = false };

        var prefijoEsperado = petar.PdfHash?[..Math.Min(12, petar.PdfHash.Length)];
        var valido = !string.IsNullOrWhiteSpace(hash) && string.Equals(hash, prefijoEsperado, StringComparison.OrdinalIgnoreCase);

        return new PetarVerificacionPublicaDto
        {
            Encontrado = true,
            Valido = valido,
            WorkerNombre = petar.WorkerNombre,
            ProyectoNombre = petar.ProyectoNombre,
            TipoNombre = petar.TipoNombre,
            Fecha = petar.Fecha,
            Estado = petar.Estado,
        };
    }

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
        var fileName = $"petar_{prefijo}_{workerId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.{ext}";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);
        return urls[0];
    }

    /// <summary>Toda firma del PETAR sale de la firma digital ya capturada en la autorización
    /// (SSO-FO-151) — ya no se acepta una firma dibujada al momento.</summary>
    private async Task<byte[]> ObtenerFirmaDigitalObligatoria(int workerId)
    {
        var (url, _, _) = await _atsRepository.GetFirmaDigitalAutorizacion(workerId);
        var bytes = url == null ? null : await DescargarBytes(url);
        return bytes ?? throw new AbrilException(
            "No hay una firma digital registrada para este trabajador. El Coordinador SSOMA debe capturarla en Autorizaciones antes de poder firmar.", 400);
    }

    private static async Task<byte[]?> DescargarBytes(string url)
    {
        try
        {
            using var http = new HttpClient();
            return await http.GetByteArrayAsync(url);
        }
        catch { return null; }
    }

    // ── PETAR Grupal ──────────────────────────────────────────────────────

    /// <summary>Cualquier trabajador puede crear el checklist grupal (mismo criterio que el ATS
    /// grupal) — nace ligado a un ATS grupal ya existente, del que hereda el proyecto.</summary>
    public async Task<PetarGrupoCrearResponseDto> CrearGrupo(int workerId, PetarGrupoCrearRequestDto dto)
    {
        if (dto.TipoId <= 0)
            throw new AbrilException("Selecciona el tipo de trabajo de alto riesgo.", 400);
        if (string.IsNullOrWhiteSpace(dto.DescripcionTrabajo))
            throw new AbrilException("Describe el trabajo a realizar.", 400);
        if (dto.Respuestas.Count == 0)
            throw new AbrilException("Completa el checklist de verificación.", 400);
        if (dto.Respuestas.Any(r => r.Respuesta == "NO"))
            throw new AbrilException("Hay ítems del checklist marcados como NO cumplidos — el trabajo no puede iniciar hasta corregirlos.", 400);

        var atsGrupo = await _atsRepository.GetGrupoEntidad(dto.AtsGrupoId)
            ?? throw new AbrilException("El ATS grupal de origen no existe.", 404);

        var grupo = await _repository.CrearGrupo(workerId, atsGrupo.ProyectoId, dto);
        return new PetarGrupoCrearResponseDto { Id = grupo.Id };
    }

    /// <summary>Lo que ve el trabajador en la misma página pública del QR del ATS grupal, para
    /// elegir cuáles PETAR le aplican a él (no todos en la cuadrilla hacen la misma tarea de alto
    /// riesgo).</summary>
    public async Task<List<PetarGrupoResumenPublicoDto>> GetGruposPublicoPorAtsToken(Guid atsToken)
    {
        var atsGrupo = await _atsRepository.GetGrupoPorToken(atsToken);
        if (atsGrupo is null) return [];
        return await _repository.GetGruposActivosPorAtsGrupo(atsGrupo.Id);
    }

    /// <summary>A diferencia de CerrarGrupo (que sí exige ser el autor o admin), VER el estado no
    /// se restringe al autor — el Residente/SSOMA que debe firmar normalmente NO es quien creó el
    /// ATS grupal, y necesita poder abrir este mismo panel para hacerlo. El control real de quién
    /// puede firmar sigue viviendo en FirmarVistoGrupoComun, no acá.</summary>
    public async Task<List<PetarGrupoEstadoDto>> GetEstadosPorAtsGrupo(int atsGrupoId, int workerId, bool esAdmin)
    {
        var lista = await _repository.GetEstadosPorAtsGrupo(atsGrupoId);
        foreach (var dto in lista)
            await MarcarPermisosGrupo(dto, workerId, esAdmin);
        return lista;
    }

    public async Task<PetarGrupoEstadoDto> GetEstadoGrupo(int id, int workerId, bool esAdmin)
    {
        var dto = await _repository.GetEstadoGrupo(id) ?? throw new AbrilException("PETAR grupal no encontrado.", 404);
        await MarcarPermisosGrupo(dto, workerId, esAdmin);
        return dto;
    }

    /// <summary>Mismo criterio de permisos que FirmarVistoGrupoComun (Residente=Supervisor,
    /// Coordinador SSOMA=Ssoma) — acá solo decide qué botón mostrar, la firma real se valida de
    /// nuevo server-side al firmar.</summary>
    private async Task MarcarPermisosGrupo(PetarGrupoEstadoDto dto, int workerId, bool esAdmin)
    {
        if (dto.Estado != "Activo") return;
        var responsables = await _atsRepository.GetResponsables(dto.ProyectoId);
        var esResidente = esAdmin || responsables.ResidenteWorkerId == workerId;
        var email = await _atsRepository.GetEmailCorporativoWorker(workerId);
        var esSsoma = esAdmin || (email != null && responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase));
        dto.PuedeFirmarSupervisor = esResidente && !dto.SupervisorFirmado;
        dto.PuedeFirmarSsoma = esSsoma && !dto.SsomaFirmado;
    }

    public async Task CerrarGrupo(int id, int workerId, bool esAdmin)
    {
        if (!esAdmin && !await _repository.EsAutorDeGrupo(id, workerId))
            throw new AbrilException("Este PETAR grupal no te pertenece.", 403);
        await _repository.CerrarGrupo(id);
    }

    public Task FirmarSupervisorGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => FirmarVistoGrupoComun(id, callerUserId, esAdmin, body, "Supervisor");

    public Task FirmarSsomaGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => FirmarVistoGrupoComun(id, callerUserId, esAdmin, body, "Ssoma");

    private async Task FirmarVistoGrupoComun(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body, string rol)
    {
        var grupo = await _repository.GetGrupoEntidad(id) ?? throw new AbrilException("PETAR grupal no encontrado.", 404);
        var workerId = await _atsRepository.ResolverWorkerIdAsync(callerUserId);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);

        if (!esAdmin)
        {
            var responsables = await _atsRepository.GetResponsables(grupo.ProyectoId);
            if (rol == "Supervisor")
            {
                if (responsables.ResidenteWorkerId != workerId)
                    throw new AbrilException("Solo el Residente/Supervisor asignado a este proyecto puede firmar este visto.", 403);
            }
            else
            {
                var email = await _atsRepository.GetEmailCorporativoWorker(workerId);
                if (email == null || !responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                    throw new AbrilException("Solo el Coordinador SSOMA de este proyecto puede dar el Visto Bueno.", 403);
            }
        }

        var (nombre, cargo) = await _atsRepository.GetNombreYCargo(workerId);

        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes(rol.ToLower() + "-grupo", workerId, firmaBytes, "png");

        await _repository.FirmarVistoGrupo(id, rol, workerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow);
    }

    /// <summary>Adhesión liviana a un PETAR grupal — mismo QR/token que el ATS grupal del que
    /// nace. Reusa Firmar() (que ya existe para el flujo individual) para no duplicar la lógica
    /// de selfie/hash/auditoría.</summary>
    public async Task<int> UnirseAGrupo(int petarGrupoId, PetarGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent)
    {
        if (!Guid.TryParse(body.AtsToken, out var atsToken))
            throw new AbrilException("Enlace inválido.", 400);

        var atsGrupo = await _atsRepository.GetGrupoPorToken(atsToken) ?? throw new AbrilException("Enlace inválido.", 404);
        var petarGrupo = await _repository.GetGrupoEntidad(petarGrupoId) ?? throw new AbrilException("Este PETAR ya no está disponible.", 404);
        if (petarGrupo.AtsGrupoId != atsGrupo.Id)
            throw new AbrilException("Este PETAR no corresponde a este ATS.", 400);
        if (petarGrupo.Estado != "Activo")
            throw new AbrilException("Este PETAR grupal ya fue cerrado.", 409);

        var atsPropio = await _atsRepository.GetEntidad(body.AtsIdPropio)
            ?? throw new AbrilException("Primero debes firmar tu ATS antes de firmar el PETAR.", 400);
        if (atsPropio.WorkerId != body.WorkerId || atsPropio.AtsGrupoId != atsGrupo.Id)
            throw new AbrilException("Este ATS no corresponde a este trabajador/grupo.", 403);

        if (await _repository.YaFirmoPetarGrupo(petarGrupo.Id, body.WorkerId))
            throw new AbrilException("Ya firmaste este PETAR.", 409);

        await ObtenerFirmaDigitalObligatoria(body.WorkerId); // antes de crear el borrador, para no dejarlo huérfano

        var petarId = await _repository.CrearDesdeGrupo(body.WorkerId, body.AtsIdPropio, petarGrupo);

        var firmarDto = new PetarFirmarRequestDto
        {
            SelfieBase64 = body.SelfieBase64,
            FirmaBase64 = body.FirmaBase64,
            HoraDispositivo = body.HoraDispositivo,
            Lat = body.Lat,
            Lng = body.Lng,
            PrecisionMetros = body.PrecisionMetros,
        };
        await Firmar(petarId, body.WorkerId, firmarDto, ipOrigen, userAgent);

        return petarId;
    }
}
