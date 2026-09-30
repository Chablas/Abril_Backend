using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Interfaces
{
    public interface IPropietarioDocumentosService
    {
        Task<PropietarioDocumentosDto> GetDocumentos(int personId);

        /// <summary>
        /// El Guardar del modal: todos los documentos nuevos de una vez. <paramref name="data"/> es el
        /// JSON de una lista de <see cref="PropietarioDocumentoNuevoDto"/> y <paramref name="archivos"/>
        /// sus archivos, en el mismo orden.
        /// </summary>
        Task<PropietarioDocumentosDto> Guardar(int personId, string? data, List<IFormFile>? archivos, int userId);

        Task<PropietarioDocumentosDto> Eliminar(int personId, int documentoId, int userId);

        Task<(byte[] Contenido, string ContentType, string NombreArchivo)> Descargar(int personId, int documentoId);
    }
}
