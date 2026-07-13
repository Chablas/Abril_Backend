using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Interfaces
{
    public interface IActasReunionRepository
    {
        Task<ReunionPaginaInicialDto> GetPaginaInicial(ReunionFiltroRequest filtro);
        Task<PagedResultDto<ReunionListItemDto>> GetReuniones(ReunionFiltroRequest filtro);
        Task<ReunionDetalleDto> GetDetalle(int reunionId);
        Task<int> Create(ReunionCreateRequest request, int userId);
        Task Update(int reunionId, ReunionUpdateRequest request, int userId);
        Task Reprogramar(int reunionId, ReunionReprogramarRequest request, int userId);
        Task CambiarEstado(int reunionId, string estado, int userId);
        Task Eliminar(int reunionId, int userId);

        Task<int> CrearAcuerdo(int reunionId, ReunionAcuerdoRequest request, int userId);
        Task ActualizarAcuerdo(int reunionAcuerdoId, ReunionAcuerdoRequest request, int userId);
        Task EliminarAcuerdo(int reunionAcuerdoId, int userId);

        Task<List<ReunionArchivoDto>> AgregarArchivos(int reunionId, List<(string Url, string? OriginalFileName)> archivos, int userId);
        Task EliminarArchivo(int reunionArchivoId, int userId);

        // ── Carpeta de SharePoint para adjuntos (singleton) ──────────────────
        /// <summary>Devuelve la carpeta única vigente (state = true) o null si aún no se configuró.</summary>
        Task<ReunionFolderDto?> GetFolderSingleton();
        /// <summary>Crea o actualiza (upsert) la carpeta única con la ubicación resuelta.</summary>
        Task UpsertFolder(string linkUrl, string driveId, string folderId, string? folderName, string? webUrl, int userId);
        /// <summary>Destino de subida vigente (driveId + folderId) o null si no hay carpeta configurada.</summary>
        Task<(string DriveId, string FolderId)?> GetFolderDestination();
        /// <summary>Proyecto y número de la reunión, para nombrar su subcarpeta en SharePoint.</summary>
        Task<(string ProjectDescription, int Numero)> GetDatosCarpetaReunion(int reunionId);
    }
}
