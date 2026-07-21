using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Implementación de <see cref="ICorreoSalidaRecipientResolver"/>. Lee la configuración de
    /// destinatarios (ga_correo_evento / ga_correo_tipo_destinatario / ga_correo_regla) en un
    /// contexto propio y de corta vida. Ver <see cref="ResolveCcAsync"/> para la lógica.
    /// </summary>
    public class CorreoSalidaRecipientResolver : ICorreoSalidaRecipientResolver
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<CorreoSalidaRecipientResolver> _logger;

        // Códigos del catálogo ga_correo_tipo_destinatario (ver CorreoTipoCodigos).
        private const string TipoTrabajador = CorreoTipoCodigos.Trabajador;
        private const string TipoArea = CorreoTipoCodigos.Area;
        private const string TipoCorreo = CorreoTipoCodigos.Correo;

        public CorreoSalidaRecipientResolver(
            IDbContextFactory<AppDbContext> factory,
            ILogger<CorreoSalidaRecipientResolver> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<List<string>> ResolveCcAsync(string eventoCodigo, IEnumerable<string>? baseCc = null)
        {
            var baseList = (baseCc ?? Enumerable.Empty<string>()).ToList();

            try
            {
                using var ctx = _factory.CreateDbContext();

                // 1) Reglas vivas + activas del correo, con el código de su tipo (1 query).
                var reglas = await (
                    from r in ctx.GaCorreoRegla
                    join e in ctx.GaCorreoEvento on r.EventoId equals e.Id
                    join t in ctx.GaCorreoTipoDestinatario on r.TipoId equals t.Id
                    where e.Codigo == eventoCodigo && e.State && e.Active
                          && r.State && r.Active
                    select new
                    {
                        r.EsExclusion,
                        TipoCodigo = t.Codigo,
                        r.WorkerId,
                        r.AreaScopeId,
                        r.Correo,
                        r.IncluirDescendientes,
                    }
                ).ToListAsync();

                if (reglas.Count == 0)
                    return Merge(baseList, null, null);

                // 2) Correos de los trabajadores referenciados (1 query).
                var workerIds = reglas
                    .Where(r => r.TipoCodigo == TipoTrabajador && r.WorkerId.HasValue)
                    .Select(r => r.WorkerId!.Value)
                    .Distinct()
                    .ToList();

                var workerEmailById = workerIds.Count == 0
                    ? new Dictionary<int, string>()
                    : await ctx.Worker
                        .Where(w => workerIds.Contains(w.Id)
                                    && w.EmailCorporativo != null && w.EmailCorporativo != "")
                        .Select(w => new { w.Id, Email = w.EmailCorporativo! })
                        .ToDictionaryAsync(x => x.Id, x => x.Email);

                // 3) Expansión de áreas → correos de sus miembros (árbol + workers, 2 queries).
                var emailsByAreaId = new Dictionary<int, List<string>>();
                Dictionary<int, List<int>> childrenByParent = new();
                var areaRules = reglas.Where(r => r.TipoCodigo == TipoArea && r.AreaScopeId.HasValue).ToList();
                if (areaRules.Count > 0)
                {
                    var tree = await GaAreaTreeLoader.LoadAsync(ctx);
                    childrenByParent = tree
                        .Where(n => n.AreaScopeParentId.HasValue)
                        .GroupBy(n => n.AreaScopeParentId!.Value)
                        .ToDictionary(g => g.Key, g => g.Select(n => n.AreaScopeId).ToList());

                    var todosLosNodos = new HashSet<int>();
                    foreach (var r in areaRules)
                        foreach (var id in Expand(r.AreaScopeId!.Value, r.IncluirDescendientes, childrenByParent))
                            todosLosNodos.Add(id);

                    var miembros = await ctx.Worker
                        .Where(w => w.AreaScopeId != null && todosLosNodos.Contains(w.AreaScopeId.Value)
                                    && w.EmailCorporativo != null && w.EmailCorporativo != "")
                        .Select(w => new { AreaScopeId = w.AreaScopeId!.Value, Email = w.EmailCorporativo! })
                        .ToListAsync();

                    emailsByAreaId = miembros
                        .GroupBy(m => m.AreaScopeId)
                        .ToDictionary(g => g.Key, g => g.Select(m => m.Email).ToList());
                }

                // 4) Armar inclusiones / exclusiones.
                var includes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var r in reglas)
                {
                    var target = r.EsExclusion ? excludes : includes;
                    switch (r.TipoCodigo)
                    {
                        case TipoTrabajador:
                            if (r.WorkerId.HasValue && workerEmailById.TryGetValue(r.WorkerId.Value, out var wEmail))
                                target.Add(wEmail.Trim());
                            break;
                        case TipoArea:
                            if (r.AreaScopeId.HasValue)
                                foreach (var nodoId in Expand(r.AreaScopeId.Value, r.IncluirDescendientes, childrenByParent))
                                    if (emailsByAreaId.TryGetValue(nodoId, out var areaEmails))
                                        foreach (var em in areaEmails) target.Add(em.Trim());
                            break;
                        case TipoCorreo:
                            if (!string.IsNullOrWhiteSpace(r.Correo))
                                target.Add(r.Correo.Trim());
                            break;
                    }
                }

                return Merge(baseList, includes, excludes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error resolviendo destinatarios configurados del correo {Evento}; se usa solo la base.",
                    eventoCodigo);
                return Merge(baseList, null, null);
            }
        }

        /// <summary>(baseCc ∪ includes) − excludes, sin vacíos ni duplicados (case-insensitive).</summary>
        private static List<string> Merge(
            IEnumerable<string> baseCc,
            HashSet<string>? includes,
            HashSet<string>? excludes)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var todos = baseCc.Concat(includes ?? Enumerable.Empty<string>());

            foreach (var raw in todos)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var email = raw.Trim();
                if (excludes != null && excludes.Contains(email)) continue;
                if (seen.Add(email)) result.Add(email);
            }
            return result;
        }

        /// <summary>Devuelve el nodo y, si <paramref name="incluirDescendientes"/>, todos sus descendientes.</summary>
        private static IEnumerable<int> Expand(int areaScopeId, bool incluirDescendientes, Dictionary<int, List<int>> childrenByParent)
        {
            var resultado = new HashSet<int> { areaScopeId };
            if (!incluirDescendientes) return resultado;

            var cola = new Queue<int>();
            cola.Enqueue(areaScopeId);
            while (cola.Count > 0)
            {
                var actual = cola.Dequeue();
                if (childrenByParent.TryGetValue(actual, out var hijos))
                    foreach (var h in hijos)
                        if (resultado.Add(h)) cola.Enqueue(h);
            }
            return resultado;
        }
    }
}
