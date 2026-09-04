using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces
{
    public interface IOnboardingService
    {
        /// <summary>Bandeja de Onboarding (resumen + fases + colaboradores + candidatos aptos).</summary>
        Task<BandejaOnboardingDto> GetBandeja();

        /// <summary>
        /// Inicia el onboarding de un candidato que ya terminó reclutamiento (firmó su carta oferta
        /// y GTH la aprobó). Hereda de esa carta la ficha maestra y el file digital del colaborador,
        /// así que acá no se sube ni se envía nada: solo se abre el proceso.
        /// </summary>
        Task<OnboardingCreateResultDto> Iniciar(OnboardingCreateDto dto, int? userId);

        /// <summary>Avanza el onboarding a la fase siguiente del checklist.</summary>
        Task<OnboardingAccionResultDto> Avanzar(int onboardingId, int? userId);
    }
}
