using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

            return await ctx.Project
                .Where(p => p.ProjectId == projectId)
                .Select(p => new ConfiguracionInicialDto
                {
                    ResponsableId = p.ResponsablePlaneamientoBimId,
                    ResponsableNombre = p.ResponsablePlaneamientoBim,
                    MetaPpc = p.MetaPpc,
                    Zonas = ctx.BimProyectoZona
                        .Where(z => z.ProjectId == p.ProjectId)
                        .OrderBy(z => z.Orden)
                        .Select(z => new ZonaDto
                        {
                            Id = z.Id,
                            Nombre = z.Nombre,
                            Orden = z.Orden,
                            Niveles = z.Niveles
                                .OrderBy(n => n.Orden)
                                .Select(n => new NivelDto { Id = n.Id, Nombre = n.Nombre, Orden = n.Orden })
                                .ToList(),
                            Sectores = z.Sectores
                                .OrderBy(s => s.Orden)
                                .Select(s => new SectorDto { Id = s.Id, Nombre = s.Nombre, Orden = s.Orden })
                                .ToList(),
                        })
                        .ToList(),
                    Fases = ctx.BimProyectoFase
                        .Where(f => f.ProjectId == p.ProjectId)
                        .OrderBy(f => f.Fase.Orden)
                        .Select(f => new FaseDto
                        {
                            Id = f.Id,
                            Nombre = f.Fase.Nombre,
                            FechaInicio = f.FechaInicio,
                            FechaFinMeta = f.FechaFinMeta,
                        })
                        .ToList(),
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<ResponsableBimLookupDto>> GetResponsables()
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.Worker
                .Where(w => w.Estado == "ACTIVO" && w.Subarea == "Planeamiento BIM")
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

            var zonasExistentes = await ctx.BimProyectoZona
                .Where(z => z.ProjectId == projectId)
                .Include(z => z.Niveles)
                .Include(z => z.Sectores)
                .ToListAsync();

            var idsZonasEnviadas = dto.Zonas.Where(z => z.Id.HasValue).Select(z => z.Id!.Value).ToHashSet();
            foreach (var zonaEliminada in zonasExistentes.Where(z => !idsZonasEnviadas.Contains(z.Id)))
                ctx.BimProyectoZona.Remove(zonaEliminada);

            foreach (var zonaDto in dto.Zonas)
            {
                var zona = zonaDto.Id.HasValue
                    ? zonasExistentes.FirstOrDefault(z => z.Id == zonaDto.Id.Value)
                    : null;

                if (zona == null)
                {
                    zona = new BimProyectoZona { ProjectId = projectId };
                    ctx.BimProyectoZona.Add(zona);
                }

                zona.Nombre = (zonaDto.Nombre ?? string.Empty).Trim();
                zona.Orden = zonaDto.Orden;

                SincronizarNiveles(ctx, zona, zonaDto.Niveles);
                SincronizarSectores(ctx, zona, zonaDto.Sectores);
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
                throw new AbrilException("No se puede eliminar una zona, nivel o sector que ya tiene registros de avance asociados.", 409);
            }
        }

        private static void SincronizarNiveles(AppDbContext ctx, BimProyectoZona zona, List<NivelUpdateDto> niveles)
        {
            var idsEnviados = niveles.Where(n => n.Id.HasValue).Select(n => n.Id!.Value).ToHashSet();
            foreach (var nivelEliminado in zona.Niveles.Where(n => !idsEnviados.Contains(n.Id)).ToList())
                ctx.BimZonaNivel.Remove(nivelEliminado);

            foreach (var nivelDto in niveles)
            {
                var nivel = nivelDto.Id.HasValue
                    ? zona.Niveles.FirstOrDefault(n => n.Id == nivelDto.Id.Value)
                    : null;

                if (nivel == null)
                {
                    nivel = new BimZonaNivel();
                    zona.Niveles.Add(nivel);
                }

                nivel.Nombre = (nivelDto.Nombre ?? string.Empty).Trim();
                nivel.Orden = nivelDto.Orden;
            }
        }

        private static void SincronizarSectores(AppDbContext ctx, BimProyectoZona zona, List<SectorUpdateDto> sectores)
        {
            var idsEnviados = sectores.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToHashSet();
            foreach (var sectorEliminado in zona.Sectores.Where(s => !idsEnviados.Contains(s.Id)).ToList())
                ctx.BimZonaSector.Remove(sectorEliminado);

            foreach (var sectorDto in sectores)
            {
                var sector = sectorDto.Id.HasValue
                    ? zona.Sectores.FirstOrDefault(s => s.Id == sectorDto.Id.Value)
                    : null;

                if (sector == null)
                {
                    sector = new BimZonaSector();
                    zona.Sectores.Add(sector);
                }

                sector.Nombre = (sectorDto.Nombre ?? string.Empty).Trim();
                sector.Orden = sectorDto.Orden;
            }
        }
    }
}
