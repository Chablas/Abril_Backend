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

        /// <summary>
        /// Adjunta la carta oferta que el colaborador devolvió firmada. Se sube al file digital del
        /// onboarding — la misma carpeta de SharePoint donde quedó la carta oferta enviada — y la
        /// deja pendiente de aprobación por GTH.
        /// </summary>
        Task<OnboardingAccionResultDto> SubirCartaFirmada(
            int onboardingId,
            string fileName,
            string contentType,
            byte[] content,
            int? userId);

        /// <summary>Aprueba la carta oferta firmada adjunta (RF-ONB-02).</summary>
        Task<OnboardingAccionResultDto> AprobarCartaFirmada(int onboardingId, int? userId);

        /// <summary>Avanza el onboarding a la fase siguiente del checklist.</summary>
        Task<OnboardingAccionResultDto> Avanzar(int onboardingId, int? userId);
    }
}
