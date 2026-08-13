using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    /// <summary>
    /// Formulario de información del postulante: cara pública (postulante, por token) y cara de GTH
    /// (enviar el enlace, revisar y aprobar/rechazar). Reemplaza al formulario externo de MS Forms.
    /// </summary>
    public interface IPostulanteFormularioService
    {
        /// <summary>Formulario público por token (contexto + catálogos + respuestas). Lanza 404 si el token no es válido.</summary>
        Task<PostulanteFormularioPublicoDto> GetPublico(string token);

        /// <summary>Guarda el envío del postulante (por token) y lo marca como COMPLETADO.</summary>
        Task GuardarPublico(string token, PostulanteFormularioRespuestasDto respuestas);

        /// <summary>Envía (o reenvía) el formulario al correo del postulante y devuelve el estado resultante.</summary>
        Task<FormularioAccionResultDto> Enviar(int candidatoId, EnviarFormularioDto dto, int? userId);

        /// <summary>Vista de GTH del formulario del candidato (modal "Ver formulario").</summary>
        Task<FormularioRevisionDto> GetRevision(int candidatoId);

        /// <summary>
        /// Registra la decisión de GTH sobre el formulario (aprobar/rechazar). Al rechazar le envía al
        /// postulante un correo con las observaciones y el mismo enlace del formulario, que se le abre
        /// con sus respuestas ya cargadas para que corrija solo lo observado y lo vuelva a enviar.
        /// </summary>
        Task<FormularioAccionResultDto> Decision(int candidatoId, FormularioDecisionDto dto, int? userId);
    }
}
