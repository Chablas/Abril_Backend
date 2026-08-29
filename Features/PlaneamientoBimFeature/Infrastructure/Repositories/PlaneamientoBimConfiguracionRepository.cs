using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Abril_Backend.Shared.Constants;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Repositories
{
    public class PlaneamientoBimConfiguracionRepository : IPlaneamientoBimConfiguracionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PlaneamientoBimConfiguracionRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ConfiguracionInicialDto?> GetConfiguracion(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                return null;

            var tieneFases = await ctx.BimProyectoFase.AnyAsync(f => f.ProjectId == projectId);
            if (!tieneFases)
            {
                var fasesCatalogo = await ctx.BimFase.ToListAsync();
                ctx.BimProyectoFase.AddRange(fasesCatalogo.Select(fase => new BimProyectoFase
                {
                    ProjectId = projectId,
                    FaseId = fase.Id,
                }));
                await ctx.SaveChangesAsync();
            }

            var proyecto = await ctx.Project
                .Where(p => p.ProjectId == projectId)
                .Select(p => new
                {
                    p.ResponsablePlaneamientoBimId,
                    p.ResponsablePlaneamientoBim,
                    p.MetaPpc,
                })
                .FirstAsync();

            var torres = await ctx.BimProyectoTorre
                .Where(t => t.ProjectId == projectId)
                .Include(t => t.Niveles)
                .OrderBy(t => t.Orden)
                .Select(t => new TorreDto
                {
                    Id = t.Id,
                    Nombre = t.Nombre,
                    Orden = t.Orden,
                    CantidadSectoresSubestructura = t.CantidadSectoresSubestructura,
                    CantidadSectoresSuperestructura = t.CantidadSectoresSuperestructura,
                    Niveles = t.Niveles
                        .OrderBy(n => n.Orden)
                        .Select(n => new NivelDto
                        {
                            Id = n.Id,
                            Nombre = n.Nombre,
                            Orden = n.Orden,
                            TipoEstructura = n.TipoEstructura,
                        })
                        .ToList(),
                })
                .ToListAsync();

            var fases = await ctx.BimProyectoFase
                .Where(f => f.ProjectId == projectId)
                .OrderBy(f => f.Fase.Orden)
                .Select(f => new FaseDto
                {
                    Id = f.Id,
                    Nombre = f.Fase.Nombre,
                    FechaInicio = f.FechaInicio,
                    FechaFinMeta = f.FechaFinMeta,
                })
                .ToListAsync();

            return new ConfiguracionInicialDto
            {
                ResponsableId = proyecto.ResponsablePlaneamientoBimId,
                ResponsableNombre = proyecto.ResponsablePlaneamientoBim,
                MetaPpc = proyecto.MetaPpc,
                Torres = torres,
                Fases = fases,
            };
        }

        public async Task<List<ResponsableBimLookupDto>> GetResponsables()
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.Worker
                .Where(w => w.WorkersEstadoId == WorkersEstadoIds.Activo && w.Subarea == "Planeamiento BIM")
                .OrderBy(w => w.Person != null ? w.Person.FullName : null)
                .Select(w => new ResponsableBimLookupDto
                {
                    Id = w.Id,
                    ApellidoNombre = (w.Person != null ? w.Person.FullName : null) ?? string.Empty,
                })
                .ToListAsync();
        }

        public async Task GuardarConfiguracion(int projectId, ConfiguracionInicialUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var project = await ctx.Project.FirstOrDefaultAsync(p => p.ProjectId == projectId);
            if (project == null)
                throw new AbrilException("El proyecto no existe.", 404);

            var torresExistentes = await ctx.BimProyectoTorre
                .Where(t => t.ProjectId == projectId)
                .Include(t => t.Niveles)
                .ToListAsync();

            var idsTorresEnviadas = dto.Torres.Where(t => t.Id.HasValue).Select(t => t.Id!.Value).ToHashSet();
            foreach (var torreEliminada in torresExistentes.Where(t => !idsTorresEnviadas.Contains(t.Id)))
                ctx.BimProyectoTorre.Remove(torreEliminada);

            foreach (var torreDto in dto.Torres)
            {
                var torre = torreDto.Id.HasValue
                    ? torresExistentes.FirstOrDefault(t => t.Id == torreDto.Id.Value)
                    : null;

                if (torre == null)
                {
                    torre = new BimProyectoTorre { ProjectId = projectId };
                    ctx.BimProyectoTorre.Add(torre);
                }

                torre.Nombre = (torreDto.Nombre ?? string.Empty).Trim();
                torre.Orden = torreDto.Orden;
                torre.CantidadSectoresSubestructura = torreDto.CantidadSectoresSubestructura;
                torre.CantidadSectoresSuperestructura = torreDto.CantidadSectoresSuperestructura;

                SincronizarNiveles(ctx, torre, torreDto.Niveles);
            }

            if (dto.Fases.Count > 0)
            {
                var fasesExistentes = await ctx.BimProyectoFase
                    .Where(f => f.ProjectId == projectId)
                    .ToDictionaryAsync(f => f.Id);

                foreach (var faseDto in dto.Fases)
                {
                    if (!fasesExistentes.TryGetValue(faseDto.Id, out var fase))
                        throw new AbrilException($"La fase {faseDto.Id} no pertenece a este proyecto.", 400);

                    fase.FechaInicio = faseDto.FechaInicio;
                    fase.FechaFinMeta = faseDto.FechaFinMeta;
                }
            }

            project.ResponsablePlaneamientoBimId = dto.ResponsableId;
            project.ResponsablePlaneamientoBim = string.IsNullOrWhiteSpace(dto.ResponsableNombre) ? null : dto.ResponsableNombre.Trim();
            project.MetaPpc = dto.MetaPpc;
            project.UpdatedDateTime = DateTime.UtcNow;

            try
            {
                await ctx.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23503")
            {
                throw new AbrilException("No se puede eliminar una torre o nivel que ya tiene registros de avance asociados.", 409);
            }
        }

        private static void SincronizarNiveles(AppDbContext ctx, BimProyectoTorre torre, List<NivelUpdateDto> niveles)
        {
            var idsEnviados = niveles.Where(n => n.Id.HasValue).Select(n => n.Id!.Value).ToHashSet();
            foreach (var nivelEliminado in torre.Niveles.Where(n => !idsEnviados.Contains(n.Id)).ToList())
                ctx.BimTorreNivel.Remove(nivelEliminado);

            foreach (var nivelDto in niveles)
            {
                var nivel = nivelDto.Id.HasValue
                    ? torre.Niveles.FirstOrDefault(n => n.Id == nivelDto.Id.Value)
                    : null;

                if (nivel == null)
                {
                    nivel = new BimTorreNivel();
                    torre.Niveles.Add(nivel);
                }

                nivel.Nombre = (nivelDto.Nombre ?? string.Empty).Trim();
                nivel.Orden = nivelDto.Orden;
                nivel.TipoEstructura = nivelDto.TipoEstructura;
            }
        }
    }
}
