using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;

public interface IAtsService
{
    Task<int> ResolverWorkerId(int userId);
    Task<AtsInitDto> GetInit(int workerId);
    Task<AtsPasoDto> CrearPasoPersonalizado(int categoriaId, string texto);

    Task<int> Crear(int workerId, AtsGuardarRequestDto dto);
    Task Editar(int id, int workerId, AtsGuardarRequestDto dto);
    Task<AtsResponseDto> GetPorId(int id, int callerUserId, int workerId, bool esAdmin);
    Task<AtsListResponseDto> Listar(AtsFiltroDto filtro, int workerId, bool esAdmin);

    Task Firmar(int id, int workerId, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent);
    Task FirmarAutorizacion(int id, int callerUserId, bool esAdmin, AtsFirmarVistoRequestDto body);
    Task FirmarVistoSsoma(int id, int callerUserId, bool esAdmin, AtsFirmarVistoRequestDto body);
    Task<byte[]> GenerarPdf(int id);
    Task<AtsVerificacionPublicaDto> VerificarPublico(int id, string? hash);

    Task<List<AtsPlantillaDto>> GetPlantillas();
    Task<List<AtsPuestoDto>> GetPuestos();
    Task<List<AtsPasoPuestoDto>> GetPasoPuestoMapeo();
    Task SetPasoPuestos(int pasoId, List<int> puestoIds);
    Task<List<AtsPlantillaPuestoDto>> GetPlantillaPuestoMapeo();
    Task SetPlantillaPuestos(int plantillaId, List<int> puestoIds);
    Task<int> CrearPlantilla(AtsPlantillaGuardarRequestDto dto);
    Task EditarPlantilla(int id, AtsPlantillaGuardarRequestDto dto);
    Task DesactivarPlantilla(int id);
    Task<List<AtsPeligroDto>> GetPeligrosConRiesgos();
    Task SetRiesgoRequierePetar(int riesgoId, bool requierePetar);

    // ── Actividades/pasos por plantilla ──────────────────────────────────
    Task<List<AtsPlantillaActividadDto>> GetActividadesDePlantilla(int plantillaId);
    Task<int> CrearActividad(int plantillaId, AtsPlantillaActividadGuardarRequestDto dto);
    Task EditarActividad(int actividadId, AtsPlantillaActividadGuardarRequestDto dto);
    Task EliminarActividad(int actividadId);
    Task<int> CrearPaso(int actividadId, AtsPlantillaPasoGuardarRequestDto dto);
    Task EditarPaso(int pasoId, AtsPlantillaPasoGuardarRequestDto dto);
    Task EliminarPaso(int pasoId);
    Task SetActividadPeligros(int actividadId, AtsPlantillaActividadPeligrosRequestDto dto);

    // ── Controles sugeridos por riesgo ───────────────────────────────────
    Task<List<AtsRiesgoConControlesDto>> GetRiesgosConControles();
    Task<int> CrearControl(int riesgoId, AtsRiesgoControlGuardarRequestDto dto);
    Task EditarControl(int controlId, AtsRiesgoControlGuardarRequestDto dto);
    Task EliminarControl(int controlId);

    // ── Autorización de uso de firma digital e imagen (firmada en físico) — gate para poder hacer ATS ────
    Task<bool> TieneAutorizacionPermiso(int workerId);
    Task<List<AtsAutorizacionTrabajadorDto>> GetTrabajadoresParaAutorizacion();
    Task SubirAutorizacionPermiso(int workerId, Stream archivo, string nombreArchivo, int subidoPorUserId);
    Task CapturarFirmaDigitalAutorizacion(int workerId, AtsAutorizacionFirmaDigitalRequestDto body, int capturadoPorUserId);
    Task<(string? FirmaDigitalUrl, string? Nombre, string? Dni)> GetFirmaDigitalAutorizacion(int workerId);
    Task<byte[]> GenerarPlantillaAutorizacionPdf(int workerId);
}
