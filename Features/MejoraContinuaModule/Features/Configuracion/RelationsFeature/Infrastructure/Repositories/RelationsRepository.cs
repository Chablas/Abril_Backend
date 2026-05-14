using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Infrastructure.Repositories
{
    public class RelationsRepository : IRelationsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public RelationsRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<object> GetPagedAsync(
            int page,
            int? phaseId,
            int? stageId,
            int? layerId,
            int? subStageId,
            int? subSpecialtyId,
            int? partidaId)
        {
            const int pageSize = 10;
            using var ctx = _factory.CreateDbContext();

            var query =
                from link in ctx.PhaseStageSubStageSubSpecialty
                join p in ctx.Phase on link.PhaseId equals p.PhaseId
                join s in ctx.Stage on link.StageId equals s.StageId into sj
                from s in sj.DefaultIfEmpty()
                join l in ctx.Layer on link.LayerId equals l.LayerId into lj
                from l in lj.DefaultIfEmpty()
                join ss in ctx.SubStage on link.SubStageId equals ss.SubStageId into ssj
                from ss in ssj.DefaultIfEmpty()
                join sp in ctx.SubSpecialty on link.SubSpecialtyId equals sp.SubSpecialtyId into spj
                from sp in spj.DefaultIfEmpty()
                join pa in ctx.Partida on link.PartidaId equals pa.PartidaId into paj
                from pa in paj.DefaultIfEmpty()

                where link.Active && link.State
                      && p.Active && p.State
                      && (!phaseId.HasValue || link.PhaseId == phaseId.Value)
                      && (!stageId.HasValue || link.StageId == stageId.Value)
                      && (!layerId.HasValue || link.LayerId == layerId.Value)
                      && (!subStageId.HasValue || link.SubStageId == subStageId.Value)
                      && (!subSpecialtyId.HasValue || link.SubSpecialtyId == subSpecialtyId.Value)
                      && (!partidaId.HasValue || link.PartidaId == partidaId.Value)

                select new RelationFlatDTO
                {
                    LinkId = link.PhaseStageSubStageSubSpecialtyId,

                    PhaseId = p.PhaseId,
                    PhaseDescription = p.PhaseDescription,

                    StageId = (int?)s.StageId,
                    StageDescription = s.StageDescription,

                    LayerId = (int?)l.LayerId,
                    LayerDescription = l.LayerDescription,

                    SubStageId = (int?)ss.SubStageId,
                    SubStageDescription = ss.SubStageDescription,

                    SubSpecialtyId = (int?)sp.SubSpecialtyId,
                    SubSpecialtyDescription = sp.SubSpecialtyDescription,

                    PartidaId = (int?)pa.PartidaId,
                    PartidaDescription = pa.PartidaDescription
                };

            var totalRecords = await query.CountAsync();
            var data = await query
                .OrderBy(x => x.PhaseDescription)
                .ThenBy(x => x.StageDescription)
                .ThenBy(x => x.LayerDescription)
                .ThenBy(x => x.SubStageDescription)
                .ThenBy(x => x.SubSpecialtyDescription)
                .ThenBy(x => x.PartidaDescription)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new
            {
                page,
                pageSize,
                totalRecords,
                totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                data
            };
        }

        public async Task<RelationFiltersDTO> GetFiltersAsync()
        {
            using var ctx1 = _factory.CreateDbContext();
            using var ctx2 = _factory.CreateDbContext();
            using var ctx3 = _factory.CreateDbContext();
            using var ctx4 = _factory.CreateDbContext();
            using var ctx5 = _factory.CreateDbContext();
            using var ctx6 = _factory.CreateDbContext();

            var phasesTask = ctx1.Phase
                .Where(p => p.Active && p.State)
                .OrderBy(p => p.PhaseDescription)
                .Select(p => new RelationPhaseDTO { PhaseId = p.PhaseId, PhaseDescription = p.PhaseDescription })
                .ToListAsync();

            var stagesTask = ctx2.Stage
                .Where(s => s.Active && s.State)
                .OrderBy(s => s.StageDescription)
                .Select(s => new RelationStageDTO { StageId = s.StageId, StageDescription = s.StageDescription })
                .ToListAsync();

            var layersTask = ctx3.Layer
                .Where(l => l.Active && l.State)
                .OrderBy(l => l.LayerDescription)
                .Select(l => new RelationLayerDTO { LayerId = l.LayerId, LayerDescription = l.LayerDescription })
                .ToListAsync();

            var subStagesTask = ctx4.SubStage
                .Where(ss => ss.Active && ss.State)
                .OrderBy(ss => ss.SubStageDescription)
                .Select(ss => new RelationSubStageDTO { SubStageId = ss.SubStageId, SubStageDescription = ss.SubStageDescription })
                .ToListAsync();

            var subSpecialtiesTask = ctx5.SubSpecialty
                .Where(sp => sp.Active && sp.State)
                .OrderBy(sp => sp.SubSpecialtyDescription)
                .Select(sp => new RelationSubSpecialtyDTO { SubSpecialtyId = sp.SubSpecialtyId, SubSpecialtyDescription = sp.SubSpecialtyDescription })
                .ToListAsync();

            var partidasTask = ctx6.Partida
                .Where(pa => pa.Active && pa.State)
                .OrderBy(pa => pa.PartidaDescription)
                .Select(pa => new PartidaSimpleDTO { PartidaId = pa.PartidaId, PartidaDescription = pa.PartidaDescription })
                .ToListAsync();

            await Task.WhenAll(phasesTask, stagesTask, layersTask, subStagesTask, subSpecialtiesTask, partidasTask);

            return new RelationFiltersDTO
            {
                Phases = phasesTask.Result,
                Stages = stagesTask.Result,
                Layers = layersTask.Result,
                SubStages = subStagesTask.Result,
                SubSpecialties = subSpecialtiesTask.Result,
                Partidas = partidasTask.Result
            };
        }

        public async Task<object?> CreateAsync(CreateRelationDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var exists = await ctx.PhaseStageSubStageSubSpecialty.AnyAsync(x =>
                x.PhaseId == dto.PhaseId &&
                x.StageId == dto.StageId &&
                x.LayerId == dto.LayerId &&
                x.SubStageId == dto.SubStageId &&
                x.SubSpecialtyId == dto.SubSpecialtyId &&
                x.PartidaId == dto.PartidaId &&
                x.State
            );

            if (exists) return null;

            var entity = new PhaseStageSubStageSubSpecialty
            {
                PhaseId = dto.PhaseId,
                StageId = dto.StageId,
                LayerId = dto.LayerId,
                SubStageId = dto.SubStageId,
                SubSpecialtyId = dto.SubSpecialtyId,
                PartidaId = dto.PartidaId,
                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            ctx.PhaseStageSubStageSubSpecialty.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.PhaseStageSubStageSubSpecialty
                .FirstOrDefaultAsync(x => x.PhaseStageSubStageSubSpecialtyId == id && x.State);

            if (entity == null) return false;

            entity.State = false;
            entity.Active = false;
            entity.UpdatedDateTime = DateTime.UtcNow;
            entity.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
            return true;
        }
    }
}
