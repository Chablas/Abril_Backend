using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface IHabTrabajadorRepository
    {
        Task<(List<WorkerHabilitacionListDto> Items, int Total)> GetWorkersHabilitacionAsync(
            string? search, int? empresaId, int? proyectoId,
            string? estadoHabilitacion, string? contratistaCasa,
            int page, int pageSize, bool soloRetirados = false, bool soloSinEmo = false, bool soloEmoVencido = false, bool soloSinVidaLey = false,
            int? areaScopeId = null, bool soloSinLectura = false, bool soloSinCertificado = false, bool soloSinInterconsulta = false,
            bool soloSinEmoCompleto = false);

        Task<List<WorkerEntregableDto>> GetEntregablesWorkerAsync(int workerId);

        Task<SsHabTrabajador> UpdateEntregableAsync(int id, WorkerEntregableUpdateDto dto, int? userId, int? empresaId = null);

        Task<int?> GetEntregableItemIdAsync(int habTrabajadorId);

        Task<List<SsHabDocumentoVersionDto>> GetVersionesDocumentoAsync(int habTrabajadorId);

        Task CambiarObraAsync(int workerId, WorkerCambiarObraDto dto);

        /// <summary>Reingresa a un trabajador retirado. Si el retiro más reciente fue automático
        /// (por documentación) y todavía hay ítems con requiere_vigencia sin Aprobar, bloquea con
        /// 400 salvo que <paramref name="esOverrideAutorizado"/> sea true (Administrador/Coordinador
        /// SSOMA de Abril — nunca una sesión de contratista).</summary>
        Task ReingresoAsync(int workerId, WorkerReingresoDto dto, bool esOverrideAutorizado = false);

        Task<int?> GetEmpresaActivaWorkerAsync(int workerId);

        Task InicializarEntregablesAsync(int workerId);

        Task<WorkerDetalleDto?> GetByIdAsync(int workerId);

        Task<WorkerDetalleDto> UpdateAsync(int workerId, WorkerUpdateDto dto);

        Task BajaAsync(int workerId, DateOnly fechaRetiro);

        Task BajaMasivaAsync(List<int> ids, DateOnly fechaRetiro);

        Task<List<WorkerEventoDto>> GetEventosAsync(int workerId);

        Task<WorkerProyectoDto> AgregarProyectoAsync(int workerId, AgregarProyectoDto dto);

        Task<List<WorkerProyectoDto>> GetProyectosAsync(int workerId);

        Task RetirarDeProyectoAsync(int workerId, int proyectoId);

        Task MarcarInduccionAsync(int workerId, int proyectoId);

        Task<List<WorkerReparacionVinculacionDto>> RepararVinculacionesAsync();

        Task<string?> GetResponsableItemTrabajadorAsync(int entregableId);

        /// <summary>Interconsultas médicas pendientes (widget junto a "EMOs Programados"), con
        /// solo razón social/proyecto actual/días de retraso — sin datos clínicos.</summary>
        Task<List<InterconsultaPendienteHabDto>> GetInterconsultasPendientesAsync();

        /// <summary>Retiros automáticos ejecutados en los últimos <paramref name="dias"/> días
        /// (widget "Retiros Automáticos", mismo lugar que "Interconsultas Pendientes").</summary>
        Task<List<RetiroAutomaticoRecienteDto>> GetRetirosAutomaticosRecientesAsync(int dias);
    }
}
