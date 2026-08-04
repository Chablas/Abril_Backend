using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Acceso a datos del formulario de información del postulante (tabla <c>gth_postulante_formulario</c>
    /// + sus catálogos). Cubre las dos caras: la pública del postulante (por token) y la de GTH
    /// (enviar, revisar, aprobar/rechazar).
    /// </summary>
    public interface IPostulanteFormularioRepository
    {
        /// <summary>
        /// Formulario público por token (contexto + catálogos + respuestas guardadas + estado), en 1
        /// roundtrip lógico. Devuelve null si el token no corresponde a ningún formulario vigente.
        /// </summary>
        Task<PostulanteFormularioPublicoDto?> GetByToken(string token);

        /// <summary>
        /// Guarda las respuestas del postulante (por token) y avanza el formulario a COMPLETADO. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si el token no existe y
        /// 409 si el formulario ya fue revisado por GTH (aprobado/rechazado → solo lectura).
        /// </summary>
        Task GuardarRespuestasByToken(string token, PostulanteFormularioRespuestasDto respuestas);

        /// <summary>
        /// Prepara el envío del formulario a un candidato APROBADO: crea (o reactiva) el formulario con
        /// estado ENVIADO usando <paramref name="nuevoToken"/> si aún no existía (si existe se conserva su
        /// token), y devuelve el contexto para armar el correo. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si el candidato no existe,
        /// 400 si no está aprobado y 409 si su formulario ya fue aprobado.
        /// </summary>
        Task<EnviarFormularioContextoDto> PrepararEnvio(int candidatoId, string correo, string nuevoToken, int? userId);

        /// <summary>
        /// Vista de GTH del formulario de un candidato (modal "Ver formulario"): estado + trazabilidad +
        /// datos (catálogos resueltos a nombre) si el postulante ya completó. Nunca es null: si GTH aún no
        /// envió el formulario devuelve <c>Existe = false</c>.
        /// </summary>
        Task<FormularioRevisionDto> GetRevision(int candidatoId);

        /// <summary>
        /// Registra la decisión de GTH sobre el formulario completado (aprobar/rechazar), guardando el
        /// revisor (id + nombre snapshot) y la fecha. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe el formulario
        /// y 409 si aún no está COMPLETADO (o ya fue revisado). Devuelve el resumen para refrescar el modal.
        /// </summary>
        Task<CandidatoFormularioResumenDto> RegistrarDecision(int candidatoId, bool aprobado, string? motivo, int? userId);
    }
}
