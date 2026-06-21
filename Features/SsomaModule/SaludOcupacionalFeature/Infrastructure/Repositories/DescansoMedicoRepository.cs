using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class DescansoMedicoRepository : IDescansoMedicoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public DescansoMedicoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }
    }
}
