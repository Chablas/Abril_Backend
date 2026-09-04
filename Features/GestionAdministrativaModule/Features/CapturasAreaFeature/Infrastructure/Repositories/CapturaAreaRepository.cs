using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.CapturasArea.Infrastructure.Repositories
{
    /// <summary>
    /// Configuración → Capturas: por cada área de la data maestra (<c>area_scope</c>) dice si sus
    /// trabajadores están obligados a subir capturas de movilidad para poder rendir una salida.
    ///
    /// El flag vive en <c>ga_salidas_area_config.capturas_obligatorias</c> (la tabla de
    /// configuración de salidas por área, que ya llevaba "filtrar por proyecto") y NO en
    /// <c>area_scope</c>: la matriz de áreas es data maestra compartida por todos los módulos y no
    /// debe cargar flags de salidas. Dos consecuencias, que son justo lo que se pidió:
    /// <list type="bullet">
    ///   <item>Un área nueva no necesita fila: sin fila, el default es OBLIGATORIO. Así el código
    ///   que crea áreas no se toca y nada queda "sin configurar".</item>
    ///   <item>Solo marcar un área como opcional escribe en BD (upsert de su fila).</item>
    /// </list>
    ///
    /// Se listan TODAS las áreas vivas y activas, una fila por nodo y sin colapsar ramas (a
    /// diferencia de Revisores de Áreas, que solo lista el primer nodo de su tipo en cada rama):
    /// cada nodo se configura de forma independiente, así que "Unidad de Proyectos" puede tener las
    /// capturas en opcional e "Ingeniería BIM", su hija, en obligatorio.
    /// </summary>
    public class CapturaAreaRepository : ICapturaAreaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CapturaAreaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<CapturaAreaInicialDto> GetInitialDataAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // Se trae el árbol vivo COMPLETO (una sola consulta) y el filtro por `active` se aplica
            // en memoria: el nombre del padre tiene que resolverse igual cuando el padre quedó
            // inactivo, así que la fila hija siempre puede decir de dónde cuelga. El `state` sí se
            // exige en area_item/area_type: una fila eliminada de la data maestra no es un área.
            var arbol = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                select new
                {
                    s.AreaScopeId,
                    s.AreaScopeParentId,
                    s.Active,
                    ai.AreaItemName,
                    at.AreaTypeId,
                    at.AreaTypeName,
                }
            ).ToListAsync();

            var nombrePorScope = arbol.ToDictionary(n => n.AreaScopeId, n => n.AreaItemName);
            var nodos = arbol.Where(n => n.Active).ToList();

            // Flags configurados. Sin fila, el área es obligatoria (default).
            var flags = await ctx.GaSalidasAreaConfig
                .Where(c => c.State)
                .Select(c => new { c.AreaScopeId, c.CapturasObligatorias })
                .ToListAsync();
            var flagPorArea = flags
                .GroupBy(f => f.AreaScopeId)
                .ToDictionary(g => g.Key, g => g.First().CapturasObligatorias);

            var areas = nodos
                .OrderBy(n => n.AreaTypeId)
                .ThenBy(n => n.AreaItemName)
                .Select(n => new CapturaAreaItemDto
                {
                    AreaScopeId = n.AreaScopeId,
                    AreaName = n.AreaItemName,
                    AreaTypeId = n.AreaTypeId,
                    AreaTypeName = n.AreaTypeName,
                    ParentName = n.AreaScopeParentId != null
                        ? nombrePorScope.GetValueOrDefault(n.AreaScopeParentId.Value)
                        : null,
                    CapturasObligatorias = !flagPorArea.TryGetValue(n.AreaScopeId, out var f) || f,
                })
                .ToList();

            // Tipos del filtro: solo los que aparecen en la tabla (un tipo sin áreas listadas
            // dejaría la tabla vacía a propósito).
            var tipos = nodos
                .GroupBy(n => new { n.AreaTypeId, n.AreaTypeName })
                .OrderBy(g => g.Key.AreaTypeName)
                .Select(g => new CapturaAreaTipoOptionDto
                {
                    AreaTypeId = g.Key.AreaTypeId,
                    AreaTypeName = g.Key.AreaTypeName,
                })
                .ToList();

            return new CapturaAreaInicialDto { Areas = areas, Tipos = tipos };
        }

        public async Task SetCapturasObligatoriasAsync(int areaScopeId, bool capturasObligatorias)
        {
            using var ctx = _factory.CreateDbContext();

            var areaValida = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.AreaScopeId == areaScopeId && s.State && s.Active && ai.State && at.State
                select s.AreaScopeId
            ).AnyAsync();
            if (!areaValida)
                throw new AbrilException("El área no existe o no está activa.", 404);

            var now = DateTimeOffset.UtcNow;
            var config = await ctx.GaSalidasAreaConfig
                .FirstOrDefaultAsync(c => c.State && c.AreaScopeId == areaScopeId);

            if (config == null)
            {
                // Volver a "obligatorio" un área que nunca tuvo fila es dejarla en su default: no
                // hay nada que escribir (y sembrar la fila solo agregaría ruido a la tabla).
                if (capturasObligatorias) return;

                ctx.GaSalidasAreaConfig.Add(new GaSalidasAreaConfig
                {
                    AreaScopeId = areaScopeId,
                    FiltraPorProyecto = false,
                    CapturasObligatorias = false,
                    State = true,
                    Active = true,
                    CreatedAt = now,
                });
            }
            else if (config.CapturasObligatorias != capturasObligatorias)
            {
                config.CapturasObligatorias = capturasObligatorias;
                config.UpdatedAt = now;
            }
            else
            {
                return;
            }

            await ctx.SaveChangesAsync();
        }
    }
}
