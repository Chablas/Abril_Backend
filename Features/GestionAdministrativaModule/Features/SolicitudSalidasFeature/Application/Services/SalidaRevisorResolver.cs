using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Services
{
    /// <summary>
    /// Resolución del revisor de salidas en tres pasos:
    ///   1) <c>workers_revisores</c>: n revisores por trabajador, por prioridad.
    ///   2) <c>area_revisores</c>: n revisores por área, por prioridad, partiendo del
    ///      nodo area_scope del solicitante (workers.area_scope_id) y subiendo por el
    ///      árbol hasta el primer nodo con revisores (los revisores se configuran solo
    ///      en el primer nodo "Área Estándar" de cada rama).
    ///   3) Fallback: el área de GTH (area_scope.email).
    /// Sustituye al algoritmo de jerarquía ApproverResolver (JefeResolver).
    /// </summary>
    public class SalidaRevisorResolver : ISalidaRevisorResolver
    {
        private const string EmailDomainCorp = "@abril.pe";
        /// <summary>Nombre exacto del área en area_item cuyo area_scope.email es el fallback.</summary>
        private const string AreaGthNombre = "Gestión del Talento Humano";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public SalidaRevisorResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<SalidaRevisorResolution?> ResolveAsync(int solicitanteWorkerId)
        {
            using var ctx = _factory.CreateDbContext();

            // 1) Primer revisor vivo + activo por prioridad, con correo corporativo válido.
            var revisor = await (
                from r in ctx.WorkersRevisores
                where r.State && r.Active && r.SolicitanteId == solicitanteWorkerId
                join w in ctx.Worker on r.RevisorId equals w.Id
                where w.Id != solicitanteWorkerId
                      && w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                orderby r.OrdenPrioridad, r.WorkersRevisoresId
                select new { w.Id, w.EmailCorporativo }
            ).FirstOrDefaultAsync();

            if (revisor != null)
                return new SalidaRevisorResolution(revisor.Id, null, revisor.EmailCorporativo!.Trim());

            // 2) Revisores del área del solicitante (area_revisores). Se parte de su
            //    nodo workers.area_scope_id y se sube por el árbol: gana el nodo más
            //    cercano con al menos un revisor vivo + activo con correo válido.
            var areaRevisor = await ResolveByAreaAsync(ctx, solicitanteWorkerId);
            if (areaRevisor != null)
                return areaRevisor;

            // 3) Fallback: área de GTH (correo configurable en area_scope.email para no
            //    hardcodear gthnm@abril.pe).
            var gth = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                where s.State && ai.State
                      && ai.AreaItemName == AreaGthNombre
                      && s.Email != null && s.Email != ""
                orderby s.AreaScopeId
                select new { s.AreaScopeId, s.Email }
            ).FirstOrDefaultAsync();

            return gth == null
                ? null
                : new SalidaRevisorResolution(null, gth.AreaScopeId, gth.Email!.Trim());
        }

        /// <summary>
        /// Busca revisores de área para el solicitante: cadena de nodos desde su
        /// area_scope hacia la raíz; en el primer nodo con revisores válidos devuelve
        /// el de mayor prioridad. El propio solicitante no puede ser su revisor.
        /// </summary>
        private async Task<SalidaRevisorResolution?> ResolveByAreaAsync(AppDbContext ctx, int solicitanteWorkerId)
        {
            var areaScopeId = await ctx.Worker
                .Where(w => w.Id == solicitanteWorkerId)
                .Select(w => w.AreaScopeId)
                .FirstOrDefaultAsync();
            if (areaScopeId == null) return null;

            // Árbol vivo (tabla pequeña) para armar la cadena solicitante → raíz en memoria.
            var scopes = await ctx.AreaScope
                .Where(s => s.State)
                .Select(s => new { s.AreaScopeId, s.AreaScopeParentId })
                .ToListAsync();
            var parentById = scopes.ToDictionary(s => s.AreaScopeId, s => s.AreaScopeParentId);

            var cadena = new List<int>();
            var visitados = new HashSet<int>();
            int? actual = areaScopeId;
            while (actual != null && visitados.Add(actual.Value))
            {
                cadena.Add(actual.Value);
                parentById.TryGetValue(actual.Value, out actual);
            }
            if (cadena.Count == 0) return null;

            // Revisores vivos + activos con correo válido de cualquier nodo de la cadena.
            var candidatos = await (
                from r in ctx.AreaRevisores
                where r.State && r.Active && cadena.Contains(r.AreaScopeId)
                join w in ctx.Worker on r.RevisorId equals w.Id
                where w.Id != solicitanteWorkerId
                      && w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                select new { r.AreaScopeId, r.OrdenPrioridad, r.AreaRevisoresId, w.Id, w.EmailCorporativo }
            ).ToListAsync();

            var elegido = candidatos
                .OrderBy(c => cadena.IndexOf(c.AreaScopeId)) // nodo más cercano al solicitante primero
                .ThenBy(c => c.OrdenPrioridad)
                .ThenBy(c => c.AreaRevisoresId)
                .FirstOrDefault();

            return elegido == null
                ? null
                : new SalidaRevisorResolution(elegido.Id, null, elegido.EmailCorporativo!.Trim());
        }
    }
}
