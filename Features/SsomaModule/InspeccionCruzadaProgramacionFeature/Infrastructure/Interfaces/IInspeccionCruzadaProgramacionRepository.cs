using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Interfaces
{
    public interface IInspeccionCruzadaProgramacionRepository
    {
        Task<List<(int ProyectoId, string Nombre)>> GetProyectosActivosAsync();

        // ── Anillos ──────────────────────────────────────────────────────
        Task<List<SsInspeccionCruzadaAnillo>> GetAnillosAsync();
        Task<SsInspeccionCruzadaAnillo> CrearAnilloAsync(string nombre);

        // ── Miembros del anillo ──────────────────────────────────────────
        Task<List<SsInspeccionCruzadaRotacion>> GetMiembrosAsync(int anilloId);
        Task<List<SsInspeccionCruzadaRotacion>> GetTodosLosMiembrosAsync();
        Task<SsInspeccionCruzadaRotacion> AgregarMiembroAsync(int anilloId, int proyectoId);
        Task<bool> ReordenarAsync(List<(int Id, int Orden)> items);
        Task<bool> SetActivoAsync(int id, bool activo);

        // ── Cursor (uno por anillo) ────────────────────────────────────────
        Task<SsInspeccionCruzadaCursor> GetOrCreateCursorAsync(int anilloId);
        Task GuardarCursorAsync(int anilloId, int offset, int anio, int mes);

        // ── Programación (calendario mensual) ──────────────────────────────
        Task<List<SsInspeccionCruzadaProgramacion>> GetProgramacionAsync(int anioDesde, int mesDesde, int anioHasta, int mesHasta);
        Task<SsInspeccionCruzadaProgramacion?> GetProgramacionByIdAsync(int id);
        Task<SsInspeccionCruzadaProgramacion> CrearProgramacionAsync(
            int anio, int mes, int anilloId, int proyectoInspectorId, int proyectoInspeccionadoId);
        Task GuardarProgramacionAsync(SsInspeccionCruzadaProgramacion programacion);

        Task<Dictionary<int, string>> GetProyectoNombresAsync(IEnumerable<int> proyectoIds);
    }
}
