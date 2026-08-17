using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces
{
    public interface IOnboardingService
    {
        /// <summary>Bandeja de Onboarding (resumen + fases + colaboradores + candidatos aptos).</summary>
        Task<BandejaOnboardingDto> GetBandeja();

        /// <summary>
        /// Inicia el onboarding de un candidato seleccionado: sube la carta oferta a SharePoint, se la
        /// envía por correo al colaborador y registra el proceso en la fase «Carta oferta firmada».
        /// </summary>
        Task<OnboardingCreateResultDto> Iniciar(
            OnboardingCreateDto dto,
            string cartaFileName,
            string cartaContentType,
            byte[] cartaContent,
            int? userId);
    }
}
