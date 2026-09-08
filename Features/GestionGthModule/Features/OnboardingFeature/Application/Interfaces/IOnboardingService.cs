using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces
{
    public interface IOnboardingService
    {
        /// <summary>Bandeja de Onboarding (resumen + fases + colaboradores).</summary>
        Task<BandejaOnboardingDto> GetBandeja();

        /// <summary>Avanza el onboarding a la fase siguiente del checklist.</summary>
        Task<OnboardingAccionResultDto> Avanzar(int onboardingId, int? userId);

        /// <summary>
        /// Envía el aviso al coordinador administrativo de la obra donde entra el colaborador, para
        /// que prevea espacio y condiciones de ingreso, y marca esa actividad como cumplida.
        /// </summary>
        Task<OnboardingAccionResultDto> EnviarAvisoObra(int onboardingId, int? userId);
    }
}
