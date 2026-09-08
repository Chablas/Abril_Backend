using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces
{
    public interface IOnboardingFormularioRepository
    {
        /// <summary>
        /// Deja listo el formulario del colaborador y devuelve todo lo que el correo de bienvenida
        /// necesita. Si ya existía (reenvío) conserva su token y sus respuestas: el enlace que ya
        /// recibió tiene que seguir funcionando.
        /// </summary>
        Task<BienvenidaContextoDto> PrepararBienvenida(int onboardingId, DateOnly? fechaLimite, int? userId);

        /// <summary>
        /// Deja registrado que el correo de bienvenida ya salió (y a qué buzón). Es lo único que
        /// marca esa actividad del checklist como cumplida. Devuelve la fila ya actualizada.
        /// </summary>
        Task<OnboardingListItemDto> MarcarBienvenidaEnviada(int onboardingId, string email, int? userId);

        /// <summary>Formulario público por token: contexto, catálogos y respuestas guardadas.</summary>
        Task<OnboardingFormularioPublicoDto?> GetPublico(string token);

        /// <summary>
        /// Guarda el envío del colaborador y marca el formulario como COMPLETADO. Devuelve el
        /// nombre del colaborador, para el log y el mensaje.
        /// </summary>
        Task<string> GuardarPublico(string token, OnboardingFormularioRespuestasDto respuestas);
    }
}
