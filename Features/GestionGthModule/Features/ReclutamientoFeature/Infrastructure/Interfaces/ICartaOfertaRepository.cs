using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Acceso a datos de la carta oferta (<c>gth_carta_oferta</c>), el último paso del proceso de
    /// reclutamiento. Cada método valida la fase del requerimiento antes de escribir: la carta y la
    /// fase se mueven siempre juntas, así que no puede haber una carta enviada sobre un
    /// requerimiento que sigue en el EMO ni un requerimiento cerrado sin carta aprobada.
    /// </summary>
    public interface ICartaOfertaRepository
    {
        /// <summary>
        /// La carta oferta del seleccionado de un requerimiento, para el detalle de GTH. Null si el
        /// requerimiento todavía no tiene seleccionado; con seleccionado y sin carta enviada viene
        /// solo con los datos de destino (correo, documento y si la ficha maestra existe).
        /// </summary>
        Task<CartaOfertaRequerimientoDto?> GetPorRequerimiento(int requerimientoId);

        /// <summary>
        /// Valida que el requerimiento pueda mandar su carta oferta (fase EMO_APTO /
        /// EMO_APTO_RESTRICCIONES, con seleccionado y ficha maestra) y devuelve todo lo que hace
        /// falta para subirla y armar el correo. No escribe nada.
        /// </summary>
        Task<CartaOfertaContextoDto> PrepararEnvio(int requerimientoId, DateOnly? fechaIngreso, string? correo, string token);

        /// <summary>
        /// Registra la carta oferta ya subida y mueve el requerimiento a CARTA_OFERTA. Se llama
        /// DESPUÉS de guardar el archivo y ANTES de mandar el correo: un correo que falla deja una
        /// carta completa de la que GTH reenvía el enlace.
        /// </summary>
        Task<CartaOfertaAccionResultDto> Crear(
            CartaOfertaContextoDto contexto,
            FileDigitalDocumentoDto carta,
            FileDigitalCarpetaDto carpeta,
            int? userId);

        /// <summary>
        /// Valida un reenvío del enlace (carta enviada y todavía sin firmar ni aprobar) y devuelve el
        /// contexto del correo, con el token que ya está guardado. No escribe nada.
        /// </summary>
        Task<CartaOfertaContextoDto> PrepararReenvio(int requerimientoId, string? correo, string tokenSiFalta);

        /// <summary>Deja registrado el reenvío (correo y fecha) recién con el correo afuera.</summary>
        Task<CartaOfertaAccionResultDto> MarcarEnlaceEnviado(
            int requerimientoId, CartaOfertaContextoDto contexto, int? userId);

        /// <summary>
        /// Resuelve dónde subir el documento firmado de un requerimiento cuya carta ya se envió y
        /// todavía no se aprobó. No escribe nada.
        /// </summary>
        Task<CartaOfertaDocumentoContextoDto> PrepararDocumentoFirmado(int requerimientoId);

        /// <summary>
        /// Guarda la carta firmada que GTH subió a mano (vía de respaldo) y mueve el requerimiento a
        /// CARTA_OFERTA_FIRMADA.
        /// </summary>
        Task<CartaOfertaAccionResultDto> GuardarFirmada(
            int requerimientoId, FileDigitalDocumentoDto carta, FileDigitalCarpetaDto? carpeta, int? userId);

        /// <summary>
        /// Aprueba la carta firmada y cierra el requerimiento (CERRADO). Es el único cierre del
        /// proceso: es lo que hace aparecer al seleccionado en Onboarding.
        /// </summary>
        Task<CartaOfertaAccionResultDto> Aprobar(int requerimientoId, int? userId);
    }
}
