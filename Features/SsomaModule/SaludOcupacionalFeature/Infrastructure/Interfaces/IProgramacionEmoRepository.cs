using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface IProgramacionEmoRepository
    {
        Task<PagedResponseDto<ProgramacionListDto>> List(ProgramacionFilterDto filter);
        Task<int> Create(ProgramacionCreateDto dto, int? userId);
        Task Update(int id, ProgramacionUpdateDto dto, int? userId);
        Task UpdateEstado(int id, string estado, int? emoResultadoId, int? userId);
        Task ClinicaAccion(int id, ProgramacionClinicaAccionDto dto, int? userId);
        Task<List<ProgramacionHabilitacionDto>> GetHabilitacionAsync(ProgramacionHabilitacionFiltrosDto filtros);
        Task PatchNotificadoAsync(int id, bool notificado);
        Task UndoCheckInAsync(int id);
        Task<ProgramacionResumenDto> GetResumen(ProgramacionFilterDto filter);
        Task<ProgramacionDestinatariosPreviewDto> GetDestinatarios(int workerId, int? clinicaId);

        /// <summary>
        /// Razones sociales del grupo con sus cupos, para el desplegable que el modal de
        /// programación muestra cuando el trabajador llegó SIN razón social — toda ficha de
        /// pre-ingreso, porque la asignación se hace acá y en ningún otro punto del proceso. La
        /// cuenta vive en <see cref="RazonSocialCuposHelper"/>, que es la misma que lee
        /// Configuración → Razones Sociales.
        ///
        /// <paramref name="workerId"/> es la ficha a la que se le va a asignar: es lo que decide si
        /// le aplica el tope de 20 (ver <see cref="RazonesSocialesEmoDto.SinTopePorReemplazo"/>).
        /// Sin él la respuesta sale con el tope puesto, que es lo seguro.
        /// </summary>
        Task<RazonesSocialesEmoDto> GetRazonesSociales(int? workerId);
        Task<ProgramacionInasistenciaEnviarCorreoResultDto> EnviarInasistencias(DateOnly fecha);

        /// <summary>
        /// Marca como "No se presentó" toda programación en un estado previo a la atención
        /// (Programado/Confirmado/Aceptado por Clínica/Reprogramado) cuya fecha ya pasó, o es
        /// hoy y ya se cumplió la hora de corte (13:00 hora Lima). Libera al trabajador para
        /// poder reprogramarse y evita que el auto-programador la lea como "activa" y genere
        /// una segunda fila para el mismo trabajador/tipo EMO.
        /// </summary>
        Task<int> CerrarInasistenciasVencidasAsync();
    }
}
