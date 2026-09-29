using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;

public interface IAtsRepository
{
    Task<int> ResolverWorkerIdAsync(int userId);
    Task<(int? PuestoId, int? ProyectoActualId)> GetPuestoYProyectoActual(int workerId);

    Task<List<AtsCategoriaPasoDto>> GetPasosParaPuesto(int? puestoId);
    Task<AtsPasoDto> CrearPasoPersonalizado(int categoriaId, string texto);
    Task<List<AtsPeligroDto>> GetPeligrosConRiesgos();

    /// <summary>El Coordinador SSOMA marca manualmente qué riesgos exigen PETAR — nunca un valor
    /// por defecto del sistema, es una decisión de catálogo que le corresponde a la persona.</summary>
    Task SetRiesgoRequierePetar(int riesgoId, bool requierePetar);
    Task<List<AtsEppDto>> GetEppActivos();
    Task<List<AtsHerramientaDto>> GetHerramientasActivas();
    Task<List<AtsProyectoDto>> GetProyectosActivos();
    Task<List<AtsPlantillaDto>> GetPlantillasActivas();
    Task<List<AtsPuestoDto>> GetPuestos();
    Task<List<AtsPasoPuestoDto>> GetPasoPuestoMapeo();
    Task SetPasoPuestos(int pasoId, List<int> puestoIds);
    Task<int?> GetPlantillaSugerida(int? puestoId);
    Task<List<AtsPlantillaPuestoDto>> GetPlantillaPuestoMapeo();
    Task SetPlantillaPuestos(int plantillaId, List<int> puestoIds);

    Task<int> Crear(int workerId, AtsGuardarRequestDto dto);
    Task Editar(int id, int workerId, AtsGuardarRequestDto dto);
    Task<SsAts?> GetEntidad(int id);
    Task<AtsResponseDto?> GetPorId(int id);

    /// <summary>Marca RequierePetar y llena Petares en cada dto — separado de ToDto porque cruza a
    /// ss_petar (otra feature) y a un chequeo de catálogo que no vale la pena repetir por fila.</summary>
    Task CompletarInfoPetar(List<AtsResponseDto> ats, Dictionary<int, (bool EsResidente, bool EsSsoma)> permisosPorAtsId);

    Task<bool> TieneConsentimiento(int workerId);
    Task RegistrarConsentimiento(int workerId, string? ipOrigen);

    // ── Autorización de uso de firma digital e imagen (firmada en físico) — gate para poder hacer ATS ────
    Task<bool> TieneAutorizacionPermiso(int workerId);
    Task SubirAutorizacionPermiso(int workerId, string archivoUrl, int? subidoPorUserId);
    Task<List<AtsAutorizacionTrabajadorDto>> GetTrabajadoresParaAutorizacion();
    Task<(string? FirmaDigitalUrl, string? Nombre, string? Dni)> GetFirmaDigitalAutorizacion(int workerId);
    Task CapturarFirmaDigitalAutorizacion(int workerId, string firmaUrl, string firmaHash, int capturadoPorUserId);

    Task Firmar(SsAts ats, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash, DateTime horaServidor, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent);

    /// <summary>Residente y correo(s) de Coordinador SSOMA del proyecto de un ATS — de acá salen
    /// tanto la autorización server-side de FirmarVisto cuanto los destinatarios del aviso por
    /// correo cuando el ejecutante firma.</summary>
    Task<AtsResponsablesDto> GetResponsables(int proyectoId);

    Task<string?> GetEmailDeUsuario(int userId);
    Task<string?> GetEmailCorporativoWorker(int workerId);

    Task<(string Nombre, string? Dni)> GetNombreYDni(int workerId);

    /// <summary>Nombre completo y puesto (cargo) de un worker — usado para snapshotear quién firmó
    /// como "Autoriza" o "Visto Bueno SSOMA".</summary>
    Task<(string Nombre, string? Cargo)> GetNombreYCargo(int workerId);

    /// <summary>"Autoriza" o "Ssoma" — cuál de las dos firmas adicionales se está registrando.</summary>
    Task FirmarVisto(int atsId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor);

    Task GuardarPdf(int id, string pdfUrl, string pdfHash);

    Task<string?> GetUltimoHashAuditLog(int atsId);
    Task AgregarAuditLog(int atsId, string evento, int? userId, string? ipOrigen, string? detalle, string hashAnterior, string hash);

    Task<bool> ExisteSelfieHash(string selfieHash);

    Task<AtsListResponseDto> Listar(AtsFiltroDto filtro);

    // ── Administración de plantillas ────────────────────────────────────
    Task<int> CrearPlantilla(AtsPlantillaGuardarRequestDto dto);
    Task EditarPlantilla(int id, AtsPlantillaGuardarRequestDto dto);
    Task DesactivarPlantilla(int id);

    // ── Actividades/pasos por plantilla ──────────────────────────────────
    Task<List<AtsPlantillaActividadDto>> GetActividadesDePlantilla(int plantillaId);
    Task<int> CrearActividad(int plantillaId, string texto);
    Task EditarActividad(int actividadId, string texto);
    Task EliminarActividad(int actividadId);
    Task<int> CrearPaso(int actividadId, string texto);
    Task EditarPaso(int pasoId, string texto);
    Task EliminarPaso(int pasoId);
    Task SetActividadPeligros(int actividadId, List<int> peligroIds);

    // ── Controles sugeridos por riesgo ───────────────────────────────────
    Task<List<AtsRiesgoConControlesDto>> GetRiesgosConControles();
    Task<int> CrearControl(int riesgoId, string texto, string tipo);
    Task EditarControl(int controlId, string texto, string tipo);
    Task EliminarControl(int controlId);
}
