using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Interfaces;

public interface ICharlaService
{
    Task<ProyectoDto?> GetMiProyectoAsync(int userId);
    Task<List<ProyectoDto>> GetTodosProyectosAsync();
    Task<ResumenDto> GetResumenAsync(int proyectoId, int mes, int anio);
    Task<List<StaffDto>> GetStaffProyectoAsync(int proyectoId);

    // Tab 1 — Asistencia (coordinator creates charla days + marks attendance)
    Task<List<CharlaResumenDto>> GetCharlasAsync(int proyectoId, int mes, int anio);
    Task<CharlaResumenDto> CrearCharlaAsync(CrearCharlaDto dto, int userId);
    Task EliminarCharlaAsync(int id);
    Task<List<AsistenciaDetailDto>> GetAsistenciaAsync(int charlaId);
    Task GuardarAsistenciaAsync(int charlaId, GuardarAsistenciaDto dto, int userId);

    // Tab 2 — Capacitaciones Staff (staff self-upload; coordinator approves)
    Task<List<CapacitacionDto>> GetCapacitacionesAsync(int proyectoId, int mes, int anio);
    Task<CapacitacionDto> SubirCapacitacionAsync(int workerId, DateTime fecha, string tema, Stream evidencia, string fileName, int userId);
    Task<CapacitacionDto> CambiarEstadoAsync(int id, string estado, int userId);
    Task EliminarCapacitacionAsync(int id);
}
