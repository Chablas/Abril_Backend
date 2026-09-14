using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Infrastructure.Repositories
{
    /// <summary>
    /// Lectura/escritura del override de visibilidad por área (<c>ga_visibilidad_area</c>) por
    /// trabajador y por ámbito. Acá solo se administra la asignación manual de nodos; el algoritmo
    /// de jerarquía vive en <see cref="ISalidaVisibilityResolver"/> y se le pregunta —no se copia—
    /// para mostrar qué ve hoy un trabajador que no tiene override.
    ///
    /// Todo va filtrado por <c>ambitoId</c>: las asignaciones de Gestión de Salidas y las de
    /// Gestión de Rendiciones conviven en la misma tabla y no se pisan.
    /// </summary>
    public class VisibilidadAreaRepository : IVisibilidadAreaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ISalidaVisibilityResolver _visibilityResolver;

        public VisibilidadAreaRepository(
            IDbContextFactory<AppDbContext> factory,
            ISalidaVisibilityResolver visibilityResolver)
        {
            _factory = factory;
            _visibilityResolver = visibilityResolver;
        }

        public async Task<VisibilidadInicialDto> GetInitialDataAsync(int ambitoId)
        {
            // Filtros (árbol de áreas) + tabla (trabajadores) en una sola conexión.
            using var ctx = _factory.CreateDbContext();
            return new VisibilidadInicialDto
            {
                Workers = await LoadWorkersAsync(ctx, ambitoId),
                AreaTree = await GaAreaTreeLoader.LoadAsync(ctx),
            };
        }

        private static async Task<List<VisibilidadWorkerItemDto>> LoadWorkersAsync(AppDbContext ctx, int ambitoId)
        {
            // Conteo de asignaciones vivas por worker (para mostrar "N áreas" o "Automático").
            var counts = await ctx.GaVisibilidadArea
                .Where(v => v.State && v.AmbitoId == ambitoId)
                .GroupBy(v => v.WorkerId)
                .Select(g => new { WorkerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.WorkerId, x => x.Count);

            var workers = await (
                from w in ctx.Worker
                where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains("@abril.pe")
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join pu in ctx.Puesto on w.PuestoId equals pu.PuestoId into puj
                from pu in puj.DefaultIfEmpty()
                join c in ctx.Categoria on pu.CategoriaId equals c.CategoriaId into cj
                from c in cj.DefaultIfEmpty()
                orderby p != null ? p.FullName : ""
                select new VisibilidadWorkerItemDto
                {
                    WorkerId = w.Id,
                    FullName = p != null ? p.FullName : null,
                    Email = w.EmailCorporativo,
                    CategoryId = pu != null ? pu.CategoriaId : (int?)null,
                    Category = c != null ? c.Nombre : null,
                    // El área sale del puesto, que ya está joineado acá para la categoría.
                    AreaScopeId = pu != null ? pu.AreaDestinoScopeId : null,
                }
            ).ToListAsync();

            foreach (var w in workers)
                if (counts.TryGetValue(w.WorkerId, out var n)) w.AreasAsignadas = n;

            return workers;
        }

        public async Task<VisibilidadWorkerDetalleDto> GetWorkerDetalleAsync(int ambitoId, int workerId)
        {
            List<VisibilidadAsignacionDto> asignaciones;
            using (var ctx = _factory.CreateDbContext())
            {
                asignaciones = await ctx.GaVisibilidadArea
                    .Where(v => v.State && v.AmbitoId == ambitoId && v.WorkerId == workerId)
                    .Select(v => new VisibilidadAsignacionDto
                    {
                        AreaScopeId = v.AreaScopeId,
                        IncluyeDescendientes = v.IncluyeDescendientes,
                    })
                    .ToListAsync();
            }

            // Lo que ve hoy sale del MISMO resolver que recorta la bandeja, así que el modal no
            // puede mostrar un alcance que después no se cumpla.
            var efectiva = await _visibilityResolver.ResolveByWorkerAsync(workerId, ambitoId);

            return new VisibilidadWorkerDetalleDto
            {
                Asignaciones = asignaciones,
                Efectivas = efectiva.AreaScopeIds.ToList(),
                EsPersonalizado = asignaciones.Count > 0,
                VeTodo = efectiva.SeesAll,
            };
        }

        public async Task UpdateWorkerAsignacionesAsync(
            int ambitoId, int workerId, List<VisibilidadAsignacionDto> asignaciones)
        {
            using var ctx = _factory.CreateDbContext();

            var workerExists = await ctx.Worker.AnyAsync(w => w.Id == workerId);
            if (!workerExists)
                throw new AbrilException("El trabajador no existe.", 404);

            // Dedup por nodo (si viene duplicado, gana el que incluye descendientes).
            var desired = (asignaciones ?? new List<VisibilidadAsignacionDto>())
                .GroupBy(a => a.AreaScopeId)
                .ToDictionary(g => g.Key, g => g.Any(x => x.IncluyeDescendientes));

            if (desired.Count > 0)
            {
                var ids = desired.Keys.ToList();
                var validos = await ctx.AreaScope
                    .Where(s => s.State && ids.Contains(s.AreaScopeId))
                    .Select(s => s.AreaScopeId)
                    .ToListAsync();
                if (validos.Count != desired.Count)
                    throw new AbrilException("Una o más áreas seleccionadas no existen.", 400);
            }

            var now = DateTimeOffset.UtcNow;
            var vivos = await ctx.GaVisibilidadArea
                .Where(v => v.State && v.AmbitoId == ambitoId && v.WorkerId == workerId)
                .ToListAsync();
            var vivosByScope = vivos.ToDictionary(v => v.AreaScopeId);

            // Actualizar / agregar los deseados.
            foreach (var (scopeId, incluye) in desired)
            {
                if (vivosByScope.TryGetValue(scopeId, out var row))
                {
                    if (row.IncluyeDescendientes != incluye)
                    {
                        row.IncluyeDescendientes = incluye;
                        row.UpdatedAt = now;
                    }
                }
                else
                {
                    ctx.GaVisibilidadArea.Add(new GaVisibilidadArea
                    {
                        AmbitoId = ambitoId,
                        WorkerId = workerId,
                        AreaScopeId = scopeId,
                        IncluyeDescendientes = incluye,
                        CreatedAt = now,
                        State = true,
                    });
                }
            }

            // Soft-delete de los que ya no están.
            foreach (var row in vivos)
            {
                if (!desired.ContainsKey(row.AreaScopeId))
                {
                    row.State = false;
                    row.UpdatedAt = now;
                }
            }

            await ctx.SaveChangesAsync();
        }
    }
}
