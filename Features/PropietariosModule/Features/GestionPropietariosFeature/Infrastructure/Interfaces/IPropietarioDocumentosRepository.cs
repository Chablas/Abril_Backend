using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Interfaces
{
    public interface IPropietarioDocumentosRepository
    {
        /// <summary>Tipos + propiedades vigentes de la persona con sus documentos, en un solo viaje.</summary>
        Task<PropietarioDocumentosDto> GetDocumentos(int personId);

        /// <summary>Persona, propiedades y tipos válidos entre los pedidos, y el link de la carpeta.</summary>
        Task<PropietarioDocumentosSubidaDto> GetContextoSubida(int personId, int[] propietarioIds, int[] tipoIds);

        /// <summary>Inserta los documentos ya subidos y devuelve el modal repintado, en un solo viaje.</summary>
        Task<PropietarioDocumentosDto> InsertarYListar(int personId, List<PropietarioDocumentoInsertDto> filas, int userId);

        /// <summary>Baja lógica (el archivo se queda en SharePoint). Null si no es un documento vigente de la persona.</summary>
        Task<PropietarioDocumentosDto?> EliminarYListar(int personId, int documentoId, int userId);

        /// <summary>Null si no es un documento vigente de una propiedad vigente de la persona.</summary>
        Task<PropietarioDocumentoArchivoRefDto?> GetArchivo(int personId, int documentoId);
    }
}
