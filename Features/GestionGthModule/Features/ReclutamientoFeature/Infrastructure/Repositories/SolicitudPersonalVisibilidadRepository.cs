using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    /// <inheritdoc cref="ISolicitudPersonalVisibilidadRepository"/>
    public class SolicitudPersonalVisibilidadRepository : ISolicitudPersonalVisibilidadRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public SolicitudPersonalVisibilidadRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<SolicitudPersonalVisibilidadInicialDto> GetInitialData()
        {
            using var ctx = _factory.CreateDbContext();

            // Solo fichas ACTIVAS: son las únicas que el resolver lee, así que configurar otra no
            // cambiaría nada. El conteo de áreas va como subconsulta: una sola ida a la base.
            var workers = await (
                from w in ctx.Worker.AsNoTracking()
                where w.WorkersEstadoId == WorkersEstadoIds.Activo
                      && w.EmailCorporativo != null
                      && w.EmailCorporativo.ToLower().Contains("@abril.pe")
                join p in ctx.Person on w.PersonId equals p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join pu in ctx.Puesto on w.PuestoId equals pu.PuestoId into puj
                from pu in puj.DefaultIfEmpty()
                join c in ctx.Categoria on pu.CategoriaId equals c.CategoriaId into cj
                from c in cj.DefaultIfEmpty()
                orderby p != null ? p.FullName : ""
                select new SolicitudPersonalVisibilidadWorkerDto
                {
                    WorkerId       = w.Id,
                    FullName       = p != null ? p.FullName : null,
                    Email          = w.EmailCorporativo,
                    // La categoría y el área salen del puesto: workers ya no las guarda.
                    CategoryId     = pu != null ? pu.CategoriaId : (int?)null,
                    Category       = c != null ? c.Nombre : null,
                    AreaScopeId    = pu != null ? pu.AreaDestinoScopeId : null,
                    AreasAsignadas = ctx.GthSolicitudPersonalVisibilidadArea
                                        .Count(v => v.State && v.WorkerId == w.Id),
                }
            ).ToListAsync();

            var areaTree = await (
                from s in ctx.AreaScope.AsNoTracking()
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                orderby s.DisplayOrder
                select new SolicitudPersonalVisibilidadAreaNodoDto
                {
                    AreaScopeId       = s.AreaScopeId,
                    AreaItemId        = s.AreaItemId,
                    AreaItemName      = ai.AreaItemName,
                    AreaTypeId        = ai.AreaTypeId,
                    AreaTypeName      = at.AreaTypeName,
                    AreaScopeParentId = s.AreaScopeParentId,
                    DisplayOrder      = s.DisplayOrder,
                }
            ).ToListAsync();

            return new SolicitudPersonalVisibilidadInicialDto { Workers = workers, AreaTree = areaTree };
        }

        public async Task UpdateWorkerAsignaciones(int workerId, List<int> areaScopeIds, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var deseadas = areaScopeIds.ToHashSet();
            var ids = deseadas.ToList();

            // La ficha, sus filas vivas y cuántas de las áreas pedidas existen, en un roundtrip. Las
            // filas salen rastreadas (sin AsNoTracking) porque son las que se van a dar de baja.
            var datos = await ctx.Worker
                .Where(w => w.Id == workerId && w.WorkersEstadoId == WorkersEstadoIds.Activo)
                .Select(w => new
                {
                    Vivas = ctx.GthSolicitudPersonalVisibilidadArea
                               .Where(v => v.State && v.WorkerId == w.Id)
                               .ToList(),
                    AreasValidas = ctx.AreaScope.Count(s => s.State && ids.Contains(s.AreaScopeId)),
                })
                .FirstOrDefaultAsync();

            if (datos == null)
                throw new AbrilException("El trabajador no existe o ya no está activo.", 404);

            if (datos.AreasValidas != deseadas.Count)
                throw new AbrilException("Una o más áreas seleccionadas no existen.", 400);

            var now = DateTimeOffset.UtcNow;

            // Baja de las que ya no están marcadas.
            foreach (var fila in datos.Vivas.Where(v => !deseadas.Contains(v.AreaScopeId)))
            {
                fila.State           = false;
                fila.UpdatedDateTime = now;
                fila.UpdatedUserId   = userId;
            }

            // Alta de las nuevas. Las que ya estaban vivas no se tocan.
            var yaVivas = datos.Vivas.Select(v => v.AreaScopeId).ToHashSet();
            foreach (var areaScopeId in deseadas.Where(id => !yaVivas.Contains(id)))
            {
                ctx.GthSolicitudPersonalVisibilidadArea.Add(new GthSolicitudPersonalVisibilidadArea
                {
                    WorkerId        = workerId,
                    AreaScopeId     = areaScopeId,
                    CreatedDateTime = now,
                    CreatedUserId   = userId,
                    State           = true,
                });
            }

            await ctx.SaveChangesAsync();
        }
    }
}
