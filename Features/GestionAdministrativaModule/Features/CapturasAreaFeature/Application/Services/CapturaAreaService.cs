using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Services
{
    public class CapturaAreaService : ICapturaAreaService
    {
        private readonly ICapturaAreaRepository _repo;

        public CapturaAreaService(ICapturaAreaRepository repo)
        {
            _repo = repo;
        }

        public Task<CapturaAreaInicialDto> GetInitialDataAsync() => _repo.GetInitialDataAsync();

        public Task SetCapturasObligatoriasAsync(int areaScopeId, bool capturasObligatorias)
            => _repo.SetCapturasObligatoriasAsync(areaScopeId, capturasObligatorias);
    }
}
