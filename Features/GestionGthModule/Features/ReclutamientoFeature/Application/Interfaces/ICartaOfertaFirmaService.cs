using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    /// <summary>
    /// Página PÚBLICA donde el postulante ve su carta oferta, registra su firma y la firma. Se entra
    /// solo con el token del enlace que le llegó por correo: no hay usuario autenticado detrás de
    /// ninguna de estas operaciones.
    /// </summary>
    public interface ICartaOfertaFirmaService
    {
        /// <summary>Contexto de la página por token (puesto, firma registrada y estado del documento).</summary>
        Task<CartaOfertaFirmaPublicoDto> GetPublico(string token);

        /// <summary>
        /// Carta oferta que subió GTH, para mostrarla en el visor. Se descarga de SharePoint con los
        /// permisos de la aplicación: el postulante nunca recibe la URL de SharePoint ni necesita
        /// acceso a la biblioteca.
        /// </summary>
        Task<(byte[] Content, string ContentType, string FileName)> GetDocumento(string token);

        /// <summary>
        /// Guarda la firma que el postulante dibujó, en su ficha de la base maestra
        /// (<c>person.signature_*</c>). Es lo que habilita el botón «Firmar».
        /// </summary>
        Task<CartaOfertaFirmaGuardarResultDto> GuardarFirma(string token, CartaOfertaFirmaGuardarDto dto);

        /// <summary>
        /// Firma la carta oferta: estampa la firma registrada en la última página del PDF, sube el
        /// resultado a la carpeta «Carta Oferta Firmada» del file digital del colaborador, lo deja
        /// como carta firmada y mueve el requerimiento a CARTA_OFERTA_FIRMADA, pendiente de la
        /// revisión de GTH. Al final le avisa a GTH por correo, porque nadie de la empresa dispara
        /// esto: sin ese aviso el documento firmado esperaría a que alguien pase por la bandeja.
        /// </summary>
        Task<CartaOfertaFirmarResultDto> Firmar(string token);

        /// <summary>
        /// Cierra el trámite del lado del colaborador: es el paso que sigue a firmar y el que le dice
        /// que ya no tiene nada más que hacer. Deja la carta marcada como finalizada —desde ahí el
        /// documento firmado es el definitivo— y le avisa por correo al SOLICITANTE de la vacante,
        /// que hasta ahora solo supo del finalista y necesita saber que el ingreso está confirmado.
        ///
        /// Es idempotente: volver a llamarlo sobre una carta ya finalizada devuelve el mismo
        /// resultado sin mandar el correo de nuevo.
        /// </summary>
        Task<CartaOfertaFinalizarResultDto> Finalizar(string token);
    }
}
