using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Repositories {
    public class ResidentReportIncidenceRepository : IResidentReportIncidenceRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        
        public ResidentReportIncidenceRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<PagedResult<ResidentReportIncidenceDTO>> GetPaged(int page)
        {
            const int pageSize = 10;

            var query = _context.ResidentReportIncidence
                .Where(r => r.Project.State)
                .OrderByDescending(x => x.ResidentReportIncidenceId)
                .Select(r => new ResidentReportIncidenceDTO
                {
                    ResidentReportIncidenceId = r.ResidentReportIncidenceId,
                    ResidentReportIncidenceDescription = r.ResidentReportIncidenceDescription,

                    ProjectId = r.Project.ProjectId,
                    ProjectDescription = r.Project.ProjectDescription,
                    StateId = r.StateId,
                    StateDescription = r.StateNavigation.StateDescription,

                    Images = r.Images
                        .Select(i => new ResidentReportIncidenceImageDTO
                        {
                            ImageUrl = i.ImageUrl
                        })
                        .ToList(),
                    ResidentReportResponseDescriptions = r.ResidentReportResponses.Select(x => new ResidentReportResponseDTO
                    {
                        ResidentReportResponseDescription = x.ResidentReportResponseDescription
                    }).ToList(),
                });

            var totalRecords = await query.CountAsync();

            var data = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<ResidentReportIncidenceDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        public async Task Create(ResidentReportIncidenceCreateDTO dto, List<string> uploadedUrls, int userId)
        {
            var registro = new ResidentReportIncidence
            {
                ResidentReportIncidenceDescription = dto.ResidentReportIncidenceDescription,
                ProjectId = dto.ProjectId,
                StateId = 6,
                CreatedUserId = userId,
                CreatedDateTime = DateTime.UtcNow,
                Active = true,
                State = true
            };

            foreach (var url in uploadedUrls)
            {
                registro.Images.Add(new ResidentReportIncidenceImage
                {
                    ImageUrl = url,
                    CreatedUserId = userId,
                    CreatedDateTime = DateTime.UtcNow,
                    Active = true,
                    State = true
                });
            }

            _context.Add(registro);

            await _context.SaveChangesAsync();
        }

        public async Task CreateResponse(ResidentReportResponseCreateDTO dto, List<string> uploadedUrls, int userId)
        {
            var registro = new ResidentReportResponse
            {
                ResidentReportIncidenceId = dto.ResidentReportIncidenceId,
                ResidentReportResponseDescription = dto.ResidentResponseDescription,
                CreatedUserId = userId,
                CreatedDateTime = DateTime.UtcNow,
                Active = true,
                State = true
            };
            foreach (var url in uploadedUrls)
            {
                registro.Images.Add(new ResidentReportResponseImage
                {
                    ImageUrl = url,
                    CreatedUserId = userId,
                    CreatedDateTime = DateTime.UtcNow,
                    Active = true,
                    State = true
                });
            }
            _context.Add(registro);
            
            await _context.SaveChangesAsync();
        }

        public async Task UpdateIncidenceState(UpdateIncidenceDTO incidence, int userId)
        {
            Console.WriteLine(incidence.incidenceId);
            var incidencia = new ResidentReportIncidence
            {
                ResidentReportIncidenceId = incidence.incidenceId
            };
            _context.ResidentReportIncidence.Attach(incidencia);
            incidencia.StateId = 5;
            incidencia.UpdatedUserId = userId;
            incidencia.UpdatedDateTime = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
        }
    }
}