using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Interfaces
{
    public interface IConvivirDocumentosService
    {
        /// <summary><paramref name="propietarioId"/> null = la primera propiedad.</summary>
        Task<ConvivirDocumentosDto> GetDocumentos(int userId, int? propietarioId);

        /// <summary>El archivo, y el documento deja de ser «Nuevo».</summary>
        Task<(byte[] Contenido, string ContentType, string NombreArchivo)> Descargar(int userId, int documentoId);
    }
}
