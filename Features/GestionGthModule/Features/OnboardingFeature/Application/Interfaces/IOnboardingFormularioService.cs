using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces
{
    public interface IOnboardingFormularioService
    {
        /// <summary>
        /// Le abre al colaborador su formulario «Nuevos Talentos» y le manda el correo de
        /// bienvenida con el enlace, la documentación que tiene que enviar y la fecha límite.
        /// Marca esa actividad del checklist como cumplida.
        /// </summary>
        /// <param name="archivos">
        /// Documentos normativos que GTH decide adjuntar (manual de onboarding, RIT, reglamento
        /// SST, formatos de cargo). Opcionales y acotados: ver el tope en el servicio.
        /// </param>
        Task<OnboardingAccionResultDto> EnviarBienvenida(
            int onboardingId, EnviarBienvenidaDto? dto, IReadOnlyList<IFormFile>? archivos, int? userId);

        /// <summary>Formulario público por token (página del colaborador, sin login).</summary>
        Task<OnboardingFormularioPublicoDto> GetPublico(string token);

        /// <summary>Recibe el envío del colaborador y marca el formulario como COMPLETADO.</summary>
        Task GuardarPublico(string token, OnboardingFormularioRespuestasDto respuestas);
    }
}
