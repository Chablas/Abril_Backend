using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Consolidadores.Interfaces;
using Abril_Backend.Shared.Services.Jerarquia;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Consolidadores.Services
{
    /// <summary>
    /// Implementación de <see cref="IConsolidadorResolver"/>. Ver ahí la regla completa.
    ///
    /// El recorrido del árbol y la deducción del Jefe/Gerente/residente son los mismos que usa
    /// <c>JefeRevisorResolver</c> y salen del mismo <see cref="EstructuraAreaLoader"/>. Lo propio de
    /// este servicio es qué tabla de asignaciones lee (<c>area_consolidadores</c>) y cómo elige:
    /// <see cref="Candidatos"/> devuelve TODOS los del primer nodo que resuelve, no el primero.
    /// </summary>
    public class ConsolidadorResolver : IConsolidadorResolver
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ConsolidadorResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<Dictionary<int, List<ConsolidadorElegido>>> ResolveManyAsync(
            IReadOnlyCollection<int> workerIds)
        {
            var resultado = new Dictionary<int, List<ConsolidadorElegido>>();

            var ids = workerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            // Ficha de cada trabajador: su nodo de área (sale del puesto, workers ya no lo guarda),
            // su persona y su correo — el propio trabajador entra siempre en su lista.
            var fichas = await ctx.Worker.AsNoTracking()
                .Where(w => ids.Contains(w.Id))
                .Select(w => new
                {
                    w.Id,
                    w.PersonId,
                    w.EmailCorporativo,
                    Nombre = w.Person != null ? w.Person.FullName : null,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                })
                .ToListAsync();

            // Obra de cada trabajador: la de su vinculación vigente (fecha_fin NULL), mismo criterio
            // y mismo orden que usa la resolución del revisor. Un trabajador retirado no tiene
            // vinculación vigente y cae al consolidador a nivel de área, que es lo correcto.
            var vinculaciones = await ctx.WorkerVinculacion.AsNoTracking()
                .Where(v => ids.Contains(v.WorkerId) && v.FechaFin == null && v.ProyectoId != null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => new { v.WorkerId, v.ProyectoId })
                .ToListAsync();
            var proyectoDe = vinculaciones
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First().ProyectoId);

            var contexto = await CargarContextoAsync(
                ctx, fichas.Where(f => f.AreaScopeId != null).Select(f => f.AreaScopeId!.Value));

            foreach (var ficha in fichas)
            {
                var lista = new List<ConsolidadorElegido>();

                // El propio trabajador va primero: consolidar lo suyo es el caso normal.
                if (!string.IsNullOrWhiteSpace(ficha.EmailCorporativo))
                    lista.Add(new ConsolidadorElegido(
                        ficha.Id, ficha.PersonId, ficha.EmailCorporativo!.Trim(), ficha.Nombre,
                        ConsolidadorOrigen.Propio));

                if (ficha.AreaScopeId != null
                    && contexto.CadenaPorNodo.TryGetValue(ficha.AreaScopeId.Value, out var cadena))
                {
                    proyectoDe.TryGetValue(ficha.Id, out var proyecto);
                    Agregar(lista, Candidatos(cadena, proyecto, contexto));
                }

                resultado[ficha.Id] = lista;
            }

            return resultado;
        }

        public async Task<Dictionary<int, AreaScopeConsolidadoresPreview>> ResolveByAreaScopeManyAsync(
            IReadOnlyCollection<int> areaScopeIds)
        {
            var resultado = new Dictionary<int, AreaScopeConsolidadoresPreview>();

            var ids = areaScopeIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();
            var contexto = await CargarContextoAsync(ctx, ids);

            // Proyectos a evaluar en los nodos que filtran: TODOS los activos, no solo los que
            // tienen un consolidador cargado — el algoritmo también le da uno a los que no tienen
            // nada asignado (su residente, o el jefe del área).
            var proyectosActivos = contexto.NodosFiltranProyecto.Count == 0
                ? new List<int>()
                : await ctx.Project.AsNoTracking()
                    .Where(p => p.State && p.Active)
                    .Select(p => p.ProjectId)
                    .ToListAsync();

            foreach (var (nodoId, cadena) in contexto.CadenaPorNodo)
            {
                if (!ids.Contains(nodoId)) continue;

                var area = Candidatos(cadena, proyecto: null, contexto);

                var porProyecto = new Dictionary<int, List<ConsolidadorElegido>>();
                if (cadena.Any(contexto.NodosFiltranProyecto.Contains))
                    foreach (var projectId in proyectosActivos)
                        porProyecto[projectId] = Candidatos(cadena, projectId, contexto);

                resultado[nodoId] = new AreaScopeConsolidadoresPreview(area, porProyecto);
            }

            return resultado;
        }

        public async Task<HashSet<int>> FiltrarQuePuedeConsolidarAsync(
            int userId, IReadOnlyCollection<int> workerIds)
        {
            var permitidos = new HashSet<int>();

            var ids = workerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0 || userId <= 0) return permitidos;

            // Fichas del usuario: un user puede mapear a más de un worker (reingresos).
            List<(int WorkerId, int? PersonId)> mias;
            using (var ctx = _factory.CreateDbContext())
            {
                mias = (await ctx.Worker.AsNoTracking()
                        .Where(w => w.Person != null && w.Person.UserId == userId)
                        .Select(w => new { w.Id, w.PersonId })
                        .ToListAsync())
                    .Select(w => (w.Id, w.PersonId))
                    .ToList();
            }
            if (mias.Count == 0) return permitidos;

            var misWorkerIds = mias.Select(m => m.WorkerId).ToHashSet();
            var misPersonIds = mias.Where(m => m.PersonId != null).Select(m => m.PersonId!.Value).ToHashSet();

            foreach (var (workerId, consolidadores) in await ResolveManyAsync(ids))
                if (consolidadores.Any(c =>
                        misWorkerIds.Contains(c.WorkerId)
                        || (c.PersonId != null && misPersonIds.Contains(c.PersonId.Value))))
                    permitidos.Add(workerId);

            return permitidos;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // NÚCLEO DE DECISIÓN
        //
        // Candidatos() es el único lugar donde se decide quién puede consolidar. Su contraparte en
        // revisores es Ranking + Elegir (JefeRevisorResolver); la regla de recorrido es la misma y
        // lo único que cambia es que acá no se elige a uno: se devuelven todos los del primer nodo
        // que resuelve. Cualquier `if` de estos que se copie afuera abre la puerta a que la
        // pantalla de configuración y el botón de la planilla discrepen.
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Los consolidadores de una cadena nodo → raíz, para un proyecto dado (o sin proyecto):
        ///
        ///   1. se recorre la cadena desde el nodo más cercano al trabajador hacia la raíz;
        ///   2. en cada nodo, si hay asignaciones a mano vivas y activas esas SON la respuesta —
        ///      primero las del <paramref name="proyecto"/> y detrás las del área, que por eso
        ///      valen para todos los proyectos sin asignación propia;
        ///   3. si el nodo no tiene ninguna, responde el algoritmo: el residente de la obra cuando
        ///      el nodo filtra por proyecto y ese proyecto es una obra con residente, más el
        ///      Jefe/Gerente del área;
        ///   4. el primer nodo que devuelva alguien corta la búsqueda. Un área sin jefe no se queda
        ///      sin consolidador: se sigue subiendo y acaba en el Gerente de su gerencia.
        ///
        /// Lo asignado a mano REEMPLAZA al algoritmo en su nodo (no se suma): si alguien se tomó el
        /// trabajo de cargar la lista de un área, esa lista es la que vale.
        /// </summary>
        private static List<ConsolidadorElegido> Candidatos(
            List<int> cadena, int? proyecto, Contexto contexto)
        {
            foreach (var nodo in cadena)
            {
                var filtra = contexto.NodosFiltranProyecto.Contains(nodo) && proyecto != null;
                var delNodo = contexto.AsignadosPorNodo[nodo];

                var aMano = (filtra
                        ? PorPrioridad(delNodo.Where(c => c.ProjectId == proyecto))
                            .Concat(PorPrioridad(delNodo.Where(c => c.ProjectId == null)))
                        : PorPrioridad(delNodo.Where(c => c.ProjectId == null)))
                    .ToList();

                if (aMano.Count > 0)
                {
                    var lista = new List<ConsolidadorElegido>();
                    Agregar(lista, aMano.Select(c => new ConsolidadorElegido(
                        c.WorkerId, c.PersonId, c.Email.Trim(), c.Nombre,
                        // Solo es "personalizado" para el área por la que se preguntó: lo que está
                        // cargado más arriba le llega a esta porque el sistema fue a buscarlo.
                        nodo == cadena[0] ? ConsolidadorOrigen.Personalizado : ConsolidadorOrigen.Algoritmo)));
                    return lista;
                }

                var porAlgoritmo = new List<EstructuraAreaLoader.PersonaDeArea>();
                if (filtra && contexto.Estructura.ResidentePorProyecto.TryGetValue(proyecto!.Value, out var residente))
                    porAlgoritmo.Add(residente);
                porAlgoritmo.AddRange(contexto.Estructura.JefePorNodo[nodo]);

                if (porAlgoritmo.Count > 0)
                {
                    var lista = new List<ConsolidadorElegido>();
                    Agregar(lista, porAlgoritmo.Select(p => new ConsolidadorElegido(
                        p.WorkerId, p.PersonId, p.Email.Trim(), p.Nombre, ConsolidadorOrigen.Algoritmo)));
                    return lista;
                }
            }

            return new List<ConsolidadorElegido>();
        }

        /// <summary>Los asignados de un nodo en orden estable (el que muestra la pantalla).</summary>
        private static IEnumerable<AsignadoAMano> PorPrioridad(IEnumerable<AsignadoAMano> asignados)
            => asignados.OrderBy(c => c.OrdenPrioridad).ThenBy(c => c.Id);

        /// <summary>
        /// Agrega sin repetir personas: la misma persona puede aparecer por más de un camino (una
        /// ficha vieja y una nueva, o asignada y además jefe del área).
        /// </summary>
        private static void Agregar(List<ConsolidadorElegido> destino, IEnumerable<ConsolidadorElegido> nuevos)
        {
            var vistos = destino
                .Select(c => c.PersonId != null ? $"p{c.PersonId}" : $"w{c.WorkerId}")
                .ToHashSet();

            foreach (var c in nuevos)
            {
                var clave = c.PersonId != null ? $"p{c.PersonId}" : $"w{c.WorkerId}";
                if (vistos.Add(clave)) destino.Add(c);
            }
        }

        // ── Carga del contexto ──────────────────────────────────────────────────

        /// <summary>Una fila viva y activa de <c>area_consolidadores</c>.</summary>
        private sealed record AsignadoAMano(
            int AreaScopeId, int? ProjectId, int OrdenPrioridad, int Id,
            int WorkerId, int? PersonId, string Email, string? Nombre);

        /// <summary>Todo lo que necesita <see cref="Candidatos"/>, cargado de una vez.</summary>
        private sealed record Contexto(
            Dictionary<int, List<int>> CadenaPorNodo,
            ILookup<int, AsignadoAMano> AsignadosPorNodo,
            IReadOnlySet<int> NodosFiltranProyecto,
            EstructuraAreaLoader.EstructuraArea Estructura);

        private static async Task<Contexto> CargarContextoAsync(AppDbContext ctx, IEnumerable<int> desdeIds)
        {
            var parentById = await EstructuraAreaLoader.CargarArbolAsync(ctx);
            var cadenaPorNodo = EstructuraAreaLoader.ConstruirCadenas(desdeIds, parentById);
            var nodos = cadenaPorNodo.Values.SelectMany(c => c).Distinct().ToList();

            var asignados = await (
                from a in ctx.AreaConsolidadores.AsNoTracking()
                where a.State && a.Active && nodos.Contains(a.AreaScopeId)
                join w in ctx.Worker.AsNoTracking() on a.ConsolidadorId equals w.Id
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EstructuraAreaLoader.EmailDomainCorp)
                select new AsignadoAMano(
                    a.AreaScopeId, a.ProjectId, a.OrdenPrioridad, a.AreaConsolidadoresId,
                    w.Id, w.PersonId, w.EmailCorporativo!, w.Person != null ? w.Person.FullName : null)
            ).ToListAsync();

            var filtran = (await ctx.GaSalidasAreaConfig.AsNoTracking()
                .Where(f => f.State && f.FiltraPorProyecto && nodos.Contains(f.AreaScopeId))
                .Select(f => f.AreaScopeId)
                .ToListAsync()).ToHashSet();

            var estructura = await EstructuraAreaLoader.CargarAsync(ctx, nodos);

            return new Contexto(cadenaPorNodo, asignados.ToLookup(a => a.AreaScopeId), filtran, estructura);
        }
    }
}
