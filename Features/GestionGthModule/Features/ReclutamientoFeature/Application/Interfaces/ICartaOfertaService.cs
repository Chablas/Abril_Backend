using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    /// <summary>
    /// Carta oferta del seleccionado: el ÚLTIMO paso del proceso de reclutamiento, el que lo cierra.
    ///
    /// Arranca cuando el EMO de ingreso sale Apto (o Apto con Restricciones): GTH consigue la carta
    /// —la genera acá desde la plantilla Word o la adjunta ya armada en PDF—, se guarda en el file
    /// del colaborador y al candidato le llega un correo con el enlace donde la lee y la firma en
    /// línea (ver <see cref="ICartaOfertaFirmaService"/>). Cuando GTH aprueba el documento firmado,
    /// el requerimiento pasa a CERRADO y recién ahí el colaborador puede entrar a Onboarding.
    ///
    /// El candidato SIEMPRE recibe un PDF, sea cual sea la vía: es el único formato que se puede
    /// mostrar y firmar dentro de la intranet sin convertir nada en el navegador.
    /// </summary>
    public interface ICartaOfertaService
    {
        /// <summary>
        /// Arma la carta oferta en el sistema: rellena la plantilla Word con los datos del
        /// requerimiento y las condiciones que puso GTH, y deja el .docx en el file del colaborador
        /// para que lo revise —y lo corrija en Word si hace falta— antes de mandarlo.
        ///
        /// NO envía nada ni mueve la fase del requerimiento: deja un borrador. Se puede repetir
        /// mientras la carta no se haya enviado; cada generación pisa la anterior.
        /// </summary>
        Task<CartaOfertaAccionResultDto> Generar(
            int requerimientoId, CartaOfertaGenerarDto dto, int? userId);

        /// <summary>
        /// Envía la carta oferta al seleccionado: la guarda en su file digital y le manda el correo
        /// con el enlace de firma. Mueve el requerimiento de EMO_APTO / EMO_APTO_RESTRICCIONES a
        /// CARTA_OFERTA.
        ///
        /// Sin <paramref name="cartaContent"/> se manda la que se generó acá: el PDF sale de
        /// convertir su .docx tal como esté en SharePoint. Con archivo, ese archivo es la carta
        /// (tiene que ser PDF) y la generada, si la hubiera, queda solo como antecedente.
        /// </summary>
        Task<CartaOfertaAccionResultDto> Enviar(
            int requerimientoId,
            CartaOfertaEnviarDto dto,
            string? cartaFileName,
            string? cartaContentType,
            byte[]? cartaContent,
            int? userId);

        /// <summary>
        /// Reenvía el correo con el enlace de firma (el correo del envío no salió, el candidato lo
        /// perdió o cambió de correo). El token del enlace original se conserva.
        /// </summary>
        Task<CartaOfertaAccionResultDto> ReenviarEnlace(int requerimientoId, string? correo, int? userId);

        /// <summary>
        /// Adjunta a mano la carta que el candidato devolvió firmada. Es la vía de RESPALDO: lo
        /// normal es que la firme él desde el enlace. Deja el requerimiento en CARTA_OFERTA_FIRMADA,
        /// pendiente de aprobación.
        /// </summary>
        Task<CartaOfertaAccionResultDto> SubirFirmada(
            int requerimientoId, string fileName, string contentType, byte[] content, int? userId);

        /// <summary>
        /// Aprueba la carta oferta firmada y CIERRA el proceso: el requerimiento pasa a CERRADO y el
        /// seleccionado aparece en Onboarding como candidato por ingresar.
        /// </summary>
        Task<CartaOfertaAccionResultDto> Aprobar(int requerimientoId, int? userId);
    }
}
