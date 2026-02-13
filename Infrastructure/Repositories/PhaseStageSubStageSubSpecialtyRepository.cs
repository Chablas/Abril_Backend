using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Infrastructure.Repositories
{
    public class PhaseStageSubStageSubSpecialtyRepository
    {
        AppDbContext _context;
        public PhaseStageSubStageSubSpecialtyRepository(AppDbContext contexto)
        {
            _context = contexto;
        }

        public async Task<List<PhaseStageSubStageSubSpecialtyDTO>> GetAll()
        {
            var data = await (
                from link in _context.PhaseStageSubStageSubSpecialty

                join p in _context.Phase on link.PhaseId equals p.PhaseId

                join s in _context.Stage on link.StageId equals s.StageId into sj
                from s in sj.DefaultIfEmpty()

                join l in _context.Layer on link.LayerId equals l.LayerId into lj
                from l in lj.DefaultIfEmpty()

                join ss in _context.SubStage on link.SubStageId equals ss.SubStageId into ssj
                from ss in ssj.DefaultIfEmpty()

                join sp in _context.SubSpecialty on link.SubSpecialtyId equals sp.SubSpecialtyId into spj
                from sp in spj.DefaultIfEmpty()

                where link.Active && link.State
                      && p.Active && p.State
                      && (s == null || (s.Active && s.State))
                      && (l == null || (l.Active && l.State))
                      && (ss == null || (ss.Active && ss.State))
                      && (sp == null || (sp.Active && sp.State))

                select new
                {
                    link.PhaseStageSubStageSubSpecialtyId,

                    p.PhaseId,
                    p.PhaseDescription,
                    PhaseOrder = p.Order,

                    StageId = (int?)s.StageId,
                    StageDescription = s != null ? s.StageDescription : null,

                    LayerId = (int?)l.LayerId,
                    LayerDescription = l != null ? l.LayerDescription : null,

                    SubStageId = (int?)ss.SubStageId,
                    SubStageDescription = ss != null ? ss.SubStageDescription : null,

                    SubSpecialtyId = (int?)sp.SubSpecialtyId,
                    SubSpecialtyDescription = sp != null ? sp.SubSpecialtyDescription : null
                }
            )
            .OrderBy(x => x.PhaseOrder ?? int.MaxValue)
            .OrderBy(x => x.PhaseDescription)
            .ThenBy(x => x.StageDescription)
            .ThenBy(x => x.LayerDescription)
            .ThenBy(x => x.SubStageDescription)
            .ThenBy(x => x.SubSpecialtyDescription)
            .ToListAsync();

            var result = data
                .GroupBy(x => new { x.PhaseId, x.PhaseDescription, x.PhaseOrder })
                .OrderBy(p => p.Key.PhaseOrder ?? int.MaxValue)
                .Select(p => new PhaseStageSubStageSubSpecialtyDTO
                {
                    PhaseId = p.Key.PhaseId,
                    PhaseDescription = p.Key.PhaseDescription,

            // Link directo Phase
            LinkId = p
                        .Where(x => x.StageId == null)
                        .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                        .FirstOrDefault(),

                    Stages = p
                        .Where(x => x.StageId != null)
                        .GroupBy(x => new { x.StageId, x.StageDescription })
                        .Select(s => new StageFilterDTO
                        {
                            StageId = s.Key.StageId!.Value,
                            StageDescription = s.Key.StageDescription!,

                    // Link directo Stage
                    LinkId = s
                                .Where(x => x.LayerId == null && x.SubStageId == null)
                                .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                .FirstOrDefault(),

                    // 🔹 SubStages SIN Layer (modelo antiguo)
                    SubStages = s
                                .Where(x => x.LayerId == null && x.SubStageId != null)
                                .GroupBy(x => new { x.SubStageId, x.SubStageDescription })
                                .Select(ss => new SubStageFilterDTO
                                {
                                    SubStageId = ss.Key.SubStageId!.Value,
                                    SubStageDescription = ss.Key.SubStageDescription!,

                                    LinkId = ss
                                        .Where(x => x.SubSpecialtyId == null)
                                        .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                        .FirstOrDefault(),

                                    SubSpecialties = ss
                                        .Where(x => x.SubSpecialtyId != null)
                                        .GroupBy(x => new { x.SubSpecialtyId, x.SubSpecialtyDescription })
                                        .Select(sp => new SubSpecialtyFilterDTO
                                        {
                                            SubSpecialtyId = sp.Key.SubSpecialtyId!.Value,
                                            SubSpecialtyDescription = sp.Key.SubSpecialtyDescription!,
                                            LinkId = sp
                                                .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                                .First()
                                        })
                                        .ToList()
                                })
                                .ToList(),

                    // 🔹 Layers
                    Layers = s
                                .Where(x => x.LayerId != null)
                                .GroupBy(x => new { x.LayerId, x.LayerDescription })
                                .Select(l => new LayerFilterDTO
                                {
                                    LayerId = l.Key.LayerId!.Value,
                                    LayerDescription = l.Key.LayerDescription!,

                            // Link directo Layer
                            LinkId = l
                                        .Where(x => x.SubStageId == null)
                                        .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                        .FirstOrDefault(),

                                    SubStages = l
                                        .Where(x => x.SubStageId != null)
                                        .GroupBy(x => new { x.SubStageId, x.SubStageDescription })
                                        .Select(ss => new SubStageFilterDTO
                                        {
                                            SubStageId = ss.Key.SubStageId!.Value,
                                            SubStageDescription = ss.Key.SubStageDescription!,

                                            LinkId = ss
                                                .Where(x => x.SubSpecialtyId == null)
                                                .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                                .FirstOrDefault(),

                                            SubSpecialties = ss
                                                .Where(x => x.SubSpecialtyId != null)
                                                .GroupBy(x => new { x.SubSpecialtyId, x.SubSpecialtyDescription })
                                                .Select(sp => new SubSpecialtyFilterDTO
                                                {
                                                    SubSpecialtyId = sp.Key.SubSpecialtyId!.Value,
                                                    SubSpecialtyDescription = sp.Key.SubSpecialtyDescription!,
                                                    LinkId = sp
                                                        .Select(x => x.PhaseStageSubStageSubSpecialtyId)
                                                        .First()
                                                })
                                                .ToList()
                                        })
                                        .ToList()
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();

            return result;
        }

        public async Task<object> GetPaged(int page)
        {
            const int pageSize = 10;

            var query =
                from link in _context.PhaseStageSubStageSubSpecialty
                join p in _context.Phase on link.PhaseId equals p.PhaseId
                join s in _context.Stage on link.StageId equals s.StageId into sj
                from s in sj.DefaultIfEmpty()
                join l in _context.Layer on link.LayerId equals l.LayerId into lj
                from l in lj.DefaultIfEmpty()
                join ss in _context.SubStage on link.SubStageId equals ss.SubStageId into ssj
                from ss in ssj.DefaultIfEmpty()
                join sp in _context.SubSpecialty on link.SubSpecialtyId equals sp.SubSpecialtyId into spj
                from sp in spj.DefaultIfEmpty()

                where link.Active && link.State
                      && p.Active && p.State

                select new PhaseStageSubStageSubSpecialtyFlatDTO
                {
                    LinkId = link.PhaseStageSubStageSubSpecialtyId,

                    PhaseId = p.PhaseId,
                    PhaseDescription = p.PhaseDescription,

                    StageId = s.StageId,
                    StageDescription = s.StageDescription,

                    LayerId = l.LayerId,
                    LayerDescription = l.LayerDescription,

                    SubStageId = ss.SubStageId,
                    SubStageDescription = ss.SubStageDescription,

                    SubSpecialtyId = sp.SubSpecialtyId,
                    SubSpecialtyDescription = sp.SubSpecialtyDescription
                };

            var totalRecords = await query.CountAsync();

            var data = await query
                .OrderBy(x => x.PhaseDescription)
                .ThenBy(x => x.StageDescription)
                .ThenBy(x => x.LayerDescription)
                .ThenBy(x => x.SubStageDescription)
                .ThenBy(x => x.SubSpecialtyDescription)
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

        public async Task<object> Create(PhaseStageSubStageSubSpecialtyCreateDTO dto, int userId)
        {

            var exists = await _context.PhaseStageSubStageSubSpecialty.AnyAsync(x =>
                x.PhaseId == dto.PhaseId &&
                x.StageId == dto.StageId &&
                x.LayerId == dto.LayerId &&
                x.SubStageId == dto.SubStageId &&
                x.SubSpecialtyId == dto.SubSpecialtyId
            );

            if (exists)
                return null;

            var entity = new PhaseStageSubStageSubSpecialty
            {
                PhaseId = dto.PhaseId,
                StageId = dto.StageId,
                LayerId = dto.LayerId,
                SubStageId = dto.SubStageId,
                SubSpecialtyId = dto.SubSpecialtyId,

                Active = dto.Active,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            _context.PhaseStageSubStageSubSpecialty.Add(entity);
            await _context.SaveChangesAsync();

            return entity;

        }
        public async Task<bool> DeleteSoftAsync(int phaseStageSubStageSubSpecialtyId, int userId)
        {
            var phaseStageSubStageSubSpecialty = await _context.PhaseStageSubStageSubSpecialty.FirstOrDefaultAsync(u => u.PhaseStageSubStageSubSpecialtyId == phaseStageSubStageSubSpecialtyId && u.State == true);

            if (phaseStageSubStageSubSpecialty == null)
                return false;

            phaseStageSubStageSubSpecialty.State = false;
            phaseStageSubStageSubSpecialty.Active = false;
            phaseStageSubStageSubSpecialty.UpdatedDateTime = DateTime.UtcNow;
            phaseStageSubStageSubSpecialty.UpdatedUserId = userId;

            _context.PhaseStageSubStageSubSpecialty.Update(phaseStageSubStageSubSpecialty);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}