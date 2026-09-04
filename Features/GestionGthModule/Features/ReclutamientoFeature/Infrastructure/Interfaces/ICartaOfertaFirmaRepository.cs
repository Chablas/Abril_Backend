using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Acceso a datos de la página PÚBLICA donde el postulante ve y firma su carta oferta. Todo entra
    /// por el token del enlace: no hay usuario autenticado del que colgar los permisos, así que el
    /// token es lo único que decide a qué carta oferta se está entrando.
    /// </summary>
    public interface ICartaOfertaFirmaRepository
    {
        /// <summary>
        /// Todo lo que la página necesita al abrirse, en una sola consulta: de qué puesto es la
        /// propuesta, el nombre del archivo que se está viendo, la firma que el postulante ya tenga
        /// registrada en su ficha y en qué estado quedó el documento.
        /// </summary>
        Task<CartaOfertaFirmaPublicoDto> GetPublicoByToken(string token);

        /// <summary>
        /// Resuelve el token para las acciones (mostrar el PDF, guardar la firma, firmar): a qué
        /// carta oferta apunta, de qué ficha es la firma y dónde está el documento. Trae además los
        /// datos del colaborador y de su vacante, que son los del aviso a GTH al firmar: son joins de
        /// una fila sobre llaves que la consulta ya recorre, así que firmar no paga un roundtrip
        /// extra por ellos. No escribe nada.
        /// </summary>
        Task<CartaOfertaFirmaContextoDto> PrepararPorToken(string token);

        /// <summary>
        /// Guarda (o reemplaza) la firma en la ficha del postulante y devuelve la que quedó, ya como
        /// data URL, para repintarla en la página sin una segunda consulta.
        /// </summary>
        Task<CartaOfertaFirmaGuardarResultDto> GuardarFirma(int personId, byte[] imageBytes, string mime);

        /// <summary>
        /// Firma registrada en la ficha, en bytes, para estamparla en el PDF. Null si el postulante
        /// todavía no dibujó ninguna: es lo que mantiene bloqueado el botón «Firmar».
        /// </summary>
        Task<(byte[] Bytes, string Mime)?> GetFirmaBytes(int personId);

        /// <summary>
        /// Deja la carta ya firmada por el postulante en su fila —las mismas columnas
        /// <c>firmada_*</c> que llena GTH cuando sube el documento a mano— y marca que la firmó él
        /// desde el enlace. Mueve el requerimiento a CARTA_OFERTA_FIRMADA, que es lo que le pone la
        /// revisión en la bandeja a GTH. Devuelve el momento de la firma en hora de Perú.
        /// </summary>
        Task<DateTime> GuardarFirmadaPorPostulante(
            int cartaOfertaId, FileDigitalDocumentoDto carta, FileDigitalCarpetaDto? carpeta);

        /// <summary>
        /// Sella la PRIMERA apertura del enlace —la fecha de conformidad que imprime el formato de
        /// aceptación de la carta, que se escribe una sola vez— y, junto con ella, dónde quedaron el
        /// .docx y el PDF que el servicio rehizo con esa fecha. Devuelve la fecha registrada (la de
        /// antes si otra pestaña llegó primero; null si el token no resuelve).
        ///
        /// Los dos documentos son opcionales: no hay nada que rehacer cuando la carta se adjuntó ya
        /// armada, y ahí esto solo sella la fecha.
        /// </summary>
        Task<DateTimeOffset?> GuardarConformidad(
            string token,
            DateTimeOffset fecha,
            FileDigitalDocumentoDto? generada,
            FileDigitalDocumentoDto? carta);

        /// <summary>
        /// Registra que el colaborador cerró su trámite con «Finalizar». Devuelve el momento en hora
        /// de Perú, o null si la carta YA estaba finalizada — que es como el servicio sabe que no
        /// tiene que volver a avisarle al solicitante.
        /// </summary>
        Task<DateTime?> MarcarFinalizada(int cartaOfertaId);
    }
}
