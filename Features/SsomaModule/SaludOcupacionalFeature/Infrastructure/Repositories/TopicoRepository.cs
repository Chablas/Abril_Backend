using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class TopicoRepository : ITopicoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public TopicoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }
    }
}
