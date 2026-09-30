using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Shared.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Infrastructure.Interfaces
{
    public interface IConvivirDocumentosRepository
    {
        /// <summary>
        /// La propiedad (la pedida si es suya; si no, la primera) y sus documentos vigentes, en un
        /// solo viaje. Filtra por el usuario del token, no por lo que mande la app.
        /// </summary>
        Task<(ConvivirPropiedadDto? Propiedad, List<ConvivirDocumentoFila> Documentos)> GetDocumentos(int userId, int? propietarioId);

        /// <summary>Null si no es un documento vigente de una propiedad vigente del usuario.</summary>
        Task<ConvivirDocumentoArchivoFila?> GetArchivo(int userId, int documentoId);

        /// <summary>
        /// Guarda la primera apertura (las siguientes no la pisan) y deja leído su aviso de la
        /// campana.
        /// </summary>
        Task MarcarLeido(int documentoId);
    }
}
