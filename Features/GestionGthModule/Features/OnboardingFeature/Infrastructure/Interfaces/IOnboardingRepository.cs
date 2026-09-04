using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces
{
    public interface IOnboardingRepository
    {
        /// <summary>
        /// Todo lo de la pantalla de Onboarding en una sola petición: resumen, embudo de fases, tabla
        /// de colaboradores y los candidatos aptos para el modal «Nuevo ingreso».
        /// </summary>
        Task<BandejaOnboardingDto> GetBandeja();

        /// <summary>
        /// Abre el onboarding de un candidato que ya terminó reclutamiento: valida que pueda entrar
        /// (seleccionado, requerimiento CERRADO y sin otro onboarding abierto), hereda de su carta
        /// oferta la ficha maestra y el file digital, y lo deja en la primera fase del checklist.
        /// Devuelve la fila lista para la tabla.
        /// </summary>
        Task<OnboardingListItemDto> Crear(OnboardingCreateDto dto, int? userId);

        /// <summary>
        /// Avanza el onboarding a la fase siguiente del catálogo, validando lo que esa fase exige.
        /// </summary>
        Task<OnboardingListItemDto> Avanzar(int onboardingId, int? userId);
    }
}
