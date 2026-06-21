using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class TopicoService : ITopicoService
    {
        private readonly ITopicoRepository _repo;

        public TopicoService(ITopicoRepository repo)
        {
            _repo = repo;
        }
    }
}
