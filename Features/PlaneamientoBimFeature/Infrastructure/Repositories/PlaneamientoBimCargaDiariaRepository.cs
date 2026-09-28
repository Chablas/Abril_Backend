using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Repositories
{
    public class PlaneamientoBimCargaDiariaRepository : IPlaneamientoBimCargaDiariaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PlaneamientoBimCargaDiariaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<CargaDiariaDto?> GetCargaDiaria(int projectId, DateOnly fecha, string categoria)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                return null;

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

            var actividades = await ctx.BimActividad
                .OrderBy(a => a.MacroActividad.Orden).ThenBy(a => a.Orden)
                .Select(a => new ActividadCatalogoDto
                {
                    Id = a.Id,
                    MacroActividadId = a.MacroActividadId,
                    MacroActividadNombre = a.MacroActividad.Nombre,
                    Nombre = a.Nombre,
                    Tipo = a.Tipo,
                    Orden = a.Orden,
                })
                .ToListAsync();

            var causas = await ctx.BimCausaNoCumplimiento
                .OrderBy(c => c.Orden)
                .Select(c => new CausaCatalogoDto { Id = c.Id, Nombre = c.Nombre, Orden = c.Orden })
                .ToListAsync();

            var celdas = await ctx.BimRegistroDiario
                .Where(r => r.ProjectId == projectId && r.Fecha == fecha)
                .Select(r => new CeldaDto
                {
                    TorreId = r.TorreId,
                    NivelId = r.NivelId,
                    SectorId = r.SectorId,
                    ActividadId = r.ActividadId,
                    PorcentajeAvance = r.PorcentajeAvance,
                    Cumplida = r.PorcentajeAvance == 100m,
                    CausaId = r.CausaId,
                    CausaNombre = r.Causa != null ? r.Causa.Nombre : null,
                    CausaDetalle = r.CausaDetalle,
                })
                .ToListAsync();

            var evidencias = await ctx.BimEvidenciaFoto
                .Where(e => e.ProjectId == projectId && e.Fecha == fecha && e.Categoria == categoria)
                .OrderBy(e => e.CreatedDateTime)
                .Select(e => new EvidenciaFotoDto { Id = e.Id, Url = e.Url, CreatedDateTime = e.CreatedDateTime })
                .ToListAsync();

            var restriccionesActivas = await ctx.BimRestriccion
                .Where(b => b.ProjectId == projectId && b.FechaCierre == null)
                .OrderByDescending(b => b.FechaCreacion)
                .Select(b => new RestriccionDto
                {
                    Id = b.Id,
                    ProjectId = b.ProjectId,
                    Descripcion = b.Descripcion,
                    Estado = b.Estado,
                    FechaCreacion = b.FechaCreacion,
                    FechaActualizacion = b.FechaActualizacion,
                    FechaCierre = b.FechaCierre,
                    FechaLevantamientoPrevista = b.FechaLevantamientoPrevista,
                    TorreId = b.TorreId,
                    TorreNombre = b.Torre != null ? b.Torre.Nombre : null,
                    NivelId = b.NivelId,
                    NivelNombre = b.Nivel != null ? b.Nivel.Nombre : null,
                    Sector = b.Sector,
                    ActividadId = b.ActividadId,
                    ActividadNombre = b.Actividad != null ? b.Actividad.Nombre : null,
                })
                .ToListAsync();

            return new CargaDiariaDto
            {
                Fecha = fecha,
                Categoria = categoria,
                Torres = torres,
                Actividades = actividades,
                Causas = causas,
                Celdas = celdas,
                Evidencias = evidencias,
                RestriccionesActivas = restriccionesActivas,
            };
        }

        public async Task<List<NivelRangoSectorDto>> GetRangosSectorPorNivel(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.BimTorreNivel
                .Where(n => n.Torre.ProjectId == projectId)
                .Select(n => new NivelRangoSectorDto
                {
                    NivelId = n.Id,
                    TipoEstructura = n.TipoEstructura,
                    CantidadSectoresSubestructura = n.Torre.CantidadSectoresSubestructura,
                    CantidadSectoresSuperestructura = n.Torre.CantidadSectoresSuperestructura,
                })
                .ToListAsync();
        }

        public async Task GuardarCargaDiaria(int projectId, DateOnly fecha, CargaDiariaUpdateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                throw new AbrilException("El proyecto no existe.", 404);

            var existentes = await ctx.BimRegistroDiario
                .Where(r => r.ProjectId == projectId && r.Fecha == fecha)
                .ToListAsync();

            var ahora = DateTimeOffset.UtcNow;

            foreach (var celda in dto.Celdas)
            {
                var targetTorreId = celda.TorreId != 0 ? celda.TorreId : celda.ZonaId;

                var registro = existentes.FirstOrDefault(r =>
                    r.TorreId == targetTorreId && r.NivelId == celda.NivelId &&
                    r.SectorId == celda.SectorId && r.ActividadId == celda.ActividadId);

                if (!celda.Cumplida.HasValue)
                {
                    // Cumplida == null: Celda neutra / no evaluada / sin programar.
                    // Si existía un registro previo, se remueve para retornar la celda a estado neutro.
                    if (registro != null)
                    {
                        ctx.BimRegistroDiario.Remove(registro);
                    }
                    continue;
                }

                bool esCumplida = celda.Cumplida.Value;

                if (registro == null)
                {
                    registro = new BimRegistroDiario
                    {
                        ProjectId = projectId,
                        TorreId = targetTorreId,
                        NivelId = celda.NivelId,
                        SectorId = celda.SectorId,
                        ActividadId = celda.ActividadId,
                        Fecha = fecha,
                        CreatedUserId = userId,
                        CreatedDateTime = ahora,
                    };
                    ctx.BimRegistroDiario.Add(registro);
                }
                else
                {
                    registro.UpdatedUserId = userId;
                    registro.UpdatedDateTime = ahora;
                }

                registro.PorcentajeAvance = esCumplida ? 100m : 0m;
                registro.CausaId = esCumplida ? null : celda.CausaId;
                registro.CausaDetalle = esCumplida ? null : celda.CausaDetalle;
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<List<EvidenciaFotoDto>> AgregarEvidencias(int projectId, DateOnly fecha, List<string> urls, int userId, string categoria)
        {
            using var ctx = _factory.CreateDbContext();

            var ahora = DateTimeOffset.UtcNow;
            var nuevas = urls.Select(url => new BimEvidenciaFoto
            {
                ProjectId = projectId,
                Fecha = fecha,
                Categoria = categoria,
                Url = url,
                CreatedUserId = userId,
                CreatedDateTime = ahora,
            }).ToList();

            ctx.BimEvidenciaFoto.AddRange(nuevas);
            await ctx.SaveChangesAsync();

            return nuevas.Select(e => new EvidenciaFotoDto { Id = e.Id, Url = e.Url, CreatedDateTime = e.CreatedDateTime }).ToList();
        }
    }
}
