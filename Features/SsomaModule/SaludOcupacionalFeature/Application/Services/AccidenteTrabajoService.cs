using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class AccidenteTrabajoService : IAccidenteTrabajoService
    {
        private readonly IAccidenteTrabajoRepository _repo;

        public AccidenteTrabajoService(IAccidenteTrabajoRepository repo)
        {
            _repo = repo;
        }
    }
}
