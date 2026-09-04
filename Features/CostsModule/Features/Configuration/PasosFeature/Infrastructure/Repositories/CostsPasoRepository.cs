using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Infrastructure.Repositories
{
    public class CostsPasoRepository : ICostsPasoRepository
    {
        private readonly AppDbContext _context;

        public CostsPasoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<CostsPasoDto>> GetPasosAsync()
        {
            // Un solo roundtrip: se traen las opciones vigentes con la descripción de su paso
            // y el agrupado por paso se arma en memoria.
            var rows = await (
                from o in _context.ProjectSubContractorStepOption
                join s in _context.ProjectSubContractorStatus
                    on o.ProjectSubContractorStatusId equals s.ProjectSubContractorStatusId
                where o.State && o.Active
                orderby s.ProjectSubContractorStatusId, o.DisplayOrder, o.ProjectSubContractorStepOptionId
                select new
                {
                    s.ProjectSubContractorStatusId,
                    s.ProjectSubContractorStatusDescription,
                    o.ProjectSubContractorStepOptionId,
                    o.OptionKey,
                    o.OptionDescription,
                    o.Enabled,
                }
            ).ToListAsync();

            return rows
                .GroupBy(r => new { r.ProjectSubContractorStatusId, r.ProjectSubContractorStatusDescription })
                .Select(g => new CostsPasoDto
                {
                    StepNumber      = g.Key.ProjectSubContractorStatusId,
                    StepDescription = g.Key.ProjectSubContractorStatusDescription,
                    Options = g.Select(r => new CostsPasoOptionDto
                    {
                        ProjectSubContractorStepOptionId = r.ProjectSubContractorStepOptionId,
                        OptionKey         = r.OptionKey,
                        OptionDescription = r.OptionDescription,
                        Enabled           = r.Enabled,
                    }).ToList(),
                })
                .ToList();
        }

        public async Task<bool> UpdateOptionAsync(CostsPasoOptionUpdateDto dto, int userId)
        {
            var option = await _context.ProjectSubContractorStepOption
                .FirstOrDefaultAsync(o =>
                    o.ProjectSubContractorStepOptionId == dto.ProjectSubContractorStepOptionId
                    && o.State);

            if (option is null) return false;

            option.Enabled         = dto.Enabled;
            option.UpdatedDateTime = DateTimeOffset.UtcNow;
            option.UpdatedUserId   = userId;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
