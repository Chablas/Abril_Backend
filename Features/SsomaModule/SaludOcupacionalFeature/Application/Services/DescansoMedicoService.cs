using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class DescansoMedicoService : IDescansoMedicoService
    {
        private readonly IDescansoMedicoRepository _repo;

        public DescansoMedicoService(IDescansoMedicoRepository repo)
        {
            _repo = repo;
        }
    }
}
