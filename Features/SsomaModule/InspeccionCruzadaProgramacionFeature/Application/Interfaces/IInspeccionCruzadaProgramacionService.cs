using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Interfaces
{
    public interface IInspeccionCruzadaProgramacionService
    {
        Task<List<ProyectoSimpleInspeccionCruzadaDto>> GetProyectosDisponiblesAsync();

        Task<List<AnilloDto>> GetAnillosAsync();
        Task<AnilloDto> CrearAnilloAsync(string nombre);
        Task<MiembroAnilloDto> AgregarMiembroAsync(int anilloId, int proyectoId);
        Task ReordenarAsync(ReordenarDto dto);
        Task SetActivoAsync(int id, bool activo);

        /// <summary>
        /// Devuelve la programación en [anioDesde/mesDesde, anioHasta/mesHasta], generando
        /// primero los meses del rango que todavía no existan (uno por anillo, avanzando el
        /// desplazamiento circular).
        /// </summary>
        Task<List<ProgramacionInspeccionCruzadaDto>> GetProgramacionAsync(
            int anioDesde, int mesDesde, int anioHasta, int mesHasta);

        Task ReasignarAsync(int id, ReasignarProgramacionDto dto);
    }
}
