using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Interfaces
{
    public interface IActasReunionService
    {
        Task<ReunionPaginaInicialDto> GetPaginaInicial(ReunionFiltroRequest filtro);
        Task<PagedResultDto<ReunionListItemDto>> GetReuniones(ReunionFiltroRequest filtro);
        Task<ReunionDetalleDto> GetDetalle(int reunionId);
        Task<int> Create(ReunionCreateRequest request, int userId);
        Task Update(int reunionId, ReunionUpdateRequest request, int userId);
        Task Reprogramar(int reunionId, ReunionReprogramarRequest request, int userId);
        Task CambiarEstado(int reunionId, ReunionCambiarEstadoRequest request, int userId);
        Task Eliminar(int reunionId, int userId);

        Task<int> CrearAcuerdo(int reunionId, ReunionAcuerdoRequest request, int userId);
        Task ActualizarAcuerdo(int reunionAcuerdoId, ReunionAcuerdoRequest request, int userId);
        Task EliminarAcuerdo(int reunionAcuerdoId, int userId);

        Task<List<ReunionArchivoDto>> SubirArchivos(int reunionId, IFormFileCollection files, int userId);
        Task EliminarArchivo(int reunionArchivoId, int userId);

        // ── Carpeta de SharePoint para adjuntos (singleton) ──────────────────
        /// <summary>Devuelve la carpeta única configurada (o null si aún no se configuró).</summary>
        Task<ReunionFolderDto?> GetFolder();
        /// <summary>Valida el link, lo resuelve vía Graph y guarda/actualiza la carpeta única.</summary>
        Task<ReunionFolderDto> SaveFolder(ReunionFolderSaveDto dto, int userId);
    }
}
