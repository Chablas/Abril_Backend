using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Services
{
    /// <summary>
    /// Onboarding de nuevos colaboradores: la fase que sigue a Reclutamiento.
    ///
    /// Desde que la carta oferta pasó a ser el último paso de Reclutamiento, acá ya no se sube ni se
    /// envía nada: el colaborador entra con su ficha maestra y su file digital ya resueltos por esa
    /// carta, y lo que queda es recorrer el checklist. Las fases del checklist todavía no están
    /// implementadas — se irán habilitando una por una.
    /// </summary>
    public class OnboardingService : IOnboardingService
    {
        private readonly IOnboardingRepository _repo;

        public OnboardingService(IOnboardingRepository repo)
        {
            _repo = repo;
        }

        public Task<BandejaOnboardingDto> GetBandeja() => _repo.GetBandeja();

        public async Task<OnboardingCreateResultDto> Iniciar(OnboardingCreateDto dto, int? userId)
        {
            if (dto == null || dto.CandidatoId <= 0)
                throw new AbrilException("Selecciona al colaborador que inicia el onboarding.", 400);

            var colaborador = await _repo.Crear(dto, userId);

            return new OnboardingCreateResultDto
            {
                OnboardingId = colaborador.OnboardingId,
                Colaborador  = colaborador,
                Message      = $"Onboarding iniciado en la fase «{colaborador.FaseNombre}».",
            };
        }

        public async Task<OnboardingAccionResultDto> Avanzar(int onboardingId, int? userId)
        {
            var colaborador = await _repo.Avanzar(onboardingId, userId);
            return new OnboardingAccionResultDto
            {
                Colaborador = colaborador,
                Message     = $"Onboarding avanzado a la fase «{colaborador.FaseNombre}».",
            };
        }
    }
}
