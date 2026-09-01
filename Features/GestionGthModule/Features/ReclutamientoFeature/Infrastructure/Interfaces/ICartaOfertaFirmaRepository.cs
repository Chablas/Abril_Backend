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
    }
}
