using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    /// <inheritdoc cref="ISolicitudPersonalScopeResolver"/>
    ///
    /// <remarks>
    /// Reglas, en este orden:
    ///   1. <b>Sin ficha vigente</b> → solo lo que él mismo registró, sin poder moverlo.
    ///   2. <b>Configuración propia</b> (<c>gth_solicitud_personal_visibilidad_area</c>, desde
    ///      Configuración → Visibilidad) → las áreas marcadas y nada del algoritmo: reemplaza a
    ///      los pasos 3 y 4, incluido el «ve todo» de GTH y del Gerente General. Tener marcado el
    ///      árbol entero es ver todo.
    ///   3. <b>Gerente General</b> (<see cref="CategoriaIds.GerenteGeneral"/>) o <b>GTH</b>
    ///      (ficha en <see cref="AreaScopeIds.GestionDelTalentoHumano"/>) → ven todo. El GG porque
    ///      autoriza las vacantes de toda la empresa (y su ficha bien puede no tener área con la
    ///      cual filtrar); GTH porque es el área dueña del proceso.
    ///   4. <b>Cualquier otro</b> → el <c>area_scope</c> de su ficha y todo el subárbol que cuelga
    ///      de él. Es lo que hace que la gerencia alcance lo que pidieron sus áreas hijas: quien
    ///      está arriba ve hacia abajo, nunca al revés.
    /// Lo que el usuario registró él mismo lo ve siempre, con cualquiera de las reglas: eso lo
    /// agrega el filtro del repositorio con <c>SolicitudPersonalScope.UserId</c>.
    ///
    /// La visibilidad NO mira la categoría a propósito: el requerimiento es del área, así que
    /// cualquiera de ella tiene que poder seguirlo aunque quien lo registró ya no esté en la
    /// empresa. Lo que sí mira la categoría es <c>PuedeGestionar</c> — registrar y avanzar el
    /// proceso son de la jefatura, con una excepción: GTH lo hace sin importar su categoría,
    /// porque es el área dueña del proceso y no puede depender de que su gente sea jefatura. La
    /// configuración propia tampoco toca eso: amplía o recorta lo que se VE, no quién mueve.
    ///
    /// Una persona puede tener más de una ficha (reingreso): se suman los alcances de todas las
    /// vigentes, igual que en <see cref="AprobacionScopeResolver"/>. Y como allá, la categoría se
    /// compara por id y las áreas por árbol, nunca por nombre: renombrar una categoría desde
    /// Configuración no puede apagar esta regla en silencio.
    /// </remarks>
    public class SolicitudPersonalScopeResolver : ISolicitudPersonalScopeResolver
    {
        /// <summary>
        /// Categorías que pueden registrar una solicitud y avanzar sus requerimientos. Es la
        /// jefatura del área: quien pide personal y quien decide a quién se contrata.
        /// </summary>
        private static readonly int[] CategoriasQueGestionan =
            { CategoriaIds.Jefe, CategoriaIds.Gerente, CategoriaIds.GerenteGeneral };

        private readonly IDbContextFactory<AppDbContext> _factory;

        public SolicitudPersonalScopeResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<SolicitudPersonalScope> ResolveAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var fichas = await FichasActivas(
                ctx,
                ctx.Worker.AsNoTracking().Where(w => w.Person != null && w.Person.UserId == userId)
            ).ToListAsync();

            if (fichas.Count == 0) return SolicitudPersonalScope.SoloLoSuyo(userId);

            // GTH: el área dueña del proceso. Pide y mueve requerimientos sin importar la categoría
            // de su puesto —un asistente de GTH registra solicitudes igual que un jefe de otra
            // área— y es la única que puede pedir un ingreso directo FFT.
            var esGth = EsGth(fichas);

            var puedeGestionar = esGth
                                 || fichas.Any(f => f.CategoriaId.HasValue
                                                    && CategoriasQueGestionan.Contains(f.CategoriaId.Value));

            var (veTodo, areas) = await ResolverAlcanceAsync(new ArbolVivo(ctx), fichas, esGth);

            // Quien ve todo viaja sin áreas: no hay nada que filtrar.
            return new SolicitudPersonalScope(
                userId, veTodo, veTodo ? new HashSet<int>() : areas, puedeGestionar, esGth);
        }

        public async Task<SolicitudPersonalVisibilidadFicha?> ResolveByWorkerAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var fichas = await FichasActivas(ctx, ctx.Worker.AsNoTracking().Where(w => w.Id == workerId))
                .ToListAsync();

            if (fichas.Count == 0) return null;

            var arbol = new ArbolVivo(ctx);
            var (veTodo, areas) = await ResolverAlcanceAsync(arbol, fichas, EsGth(fichas));

            // En el modal, ver todo es el árbol entero marcado.
            var efectivas = veTodo ? (await arbol.PadresAsync()).Keys.ToHashSet() : areas;

            return new SolicitudPersonalVisibilidadFicha(
                fichas.SelectMany(f => f.Asignadas).Distinct().ToList(), veTodo, efectivas);
        }

        /// <summary>
        /// Las fichas ACTIVAS de la consulta con lo que las reglas necesitan: la categoría y el área
        /// del puesto (workers ya no las guarda) y las áreas de su configuración propia, todo en un
        /// solo roundtrip. Se exige el estado por lo mismo que en AprobacionScopeResolver: una
        /// ficha cesada no arrastra el alcance de su área, ni tampoco el que se le configuró.
        /// </summary>
        private static IQueryable<Ficha> FichasActivas(AppDbContext ctx, IQueryable<Worker> workers) =>
            from w in workers
            where w.WorkersEstadoId == WorkersEstadoIds.Activo
            select new Ficha
            {
                CategoriaId = w.PuestoCatalogo != null ? w.PuestoCatalogo.CategoriaId : (int?)null,
                AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                Asignadas   = ctx.GthSolicitudPersonalVisibilidadArea
                                 .Where(v => v.State && v.WorkerId == w.Id)
                                 .Select(v => v.AreaScopeId)
                                 .ToList(),
            };

        private static bool EsGth(List<Ficha> fichas) =>
            fichas.Any(f => f.AreaScopeId == AreaScopeIds.GestionDelTalentoHumano);

        /// <summary>
        /// Qué áreas ven las fichas: su configuración propia si alguna la tiene y, si no, el
        /// algoritmo. Las áreas vuelven vacías cuando <c>VeTodo</c> sale del algoritmo (GTH y el
        /// GG no necesitan ni cargar el árbol).
        /// </summary>
        private static async Task<(bool VeTodo, HashSet<int> Areas)> ResolverAlcanceAsync(
            ArbolVivo arbol, List<Ficha> fichas, bool esGth)
        {
            var asignadas = fichas.SelectMany(f => f.Asignadas).ToHashSet();
            if (asignadas.Count > 0)
            {
                var padres = await arbol.PadresAsync();

                // Solo nodos vivos: un área dada de baja no aporta alcance.
                asignadas.IntersectWith(padres.Keys);

                // Marcar el árbol entero es ver todo, también lo que se registró sin área.
                return (padres.Count > 0 && asignadas.Count == padres.Count, asignadas);
            }

            // Los que ven todo: no hace falta ni cargar el árbol.
            if (esGth || fichas.Any(f => f.CategoriaId == CategoriaIds.GerenteGeneral))
                return (true, new HashSet<int>());

            var nodosPropios = fichas
                .Where(f => f.AreaScopeId.HasValue)
                .Select(f => f.AreaScopeId!.Value)
                .Distinct()
                .ToList();

            // Sin área asignada no se hereda el alcance de nadie: ve solo lo que registró él.
            if (nodosPropios.Count == 0) return (false, new HashSet<int>());

            var padresPorNodo = await arbol.PadresAsync();
            var hijosPorPadre = padresPorNodo
                .Where(n => n.Value.HasValue)
                .GroupBy(n => n.Value!.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

            var visibles = new HashSet<int>();
            foreach (var nodo in nodosPropios)
            {
                // Solo nodos vivos: un área dada de baja no aporta alcance.
                if (!padresPorNodo.ContainsKey(nodo)) continue;
                visibles.Add(nodo);
                AgregarDescendientes(nodo, hijosPorPadre, visibles);
            }

            return (false, visibles);
        }

        /// <summary>Agrega recursivamente todos los descendientes de un nodo al conjunto.</summary>
        private static void AgregarDescendientes(
            int scopeId, IDictionary<int, List<int>> hijosPorPadre, HashSet<int> set)
        {
            if (!hijosPorPadre.TryGetValue(scopeId, out var hijos)) return;
            foreach (var hijo in hijos)
            {
                if (set.Add(hijo))
                    AgregarDescendientes(hijo, hijosPorPadre, set);
            }
        }

        /// <summary>Lo que las reglas necesitan saber de una ficha.</summary>
        private sealed class Ficha
        {
            public int? CategoriaId { get; set; }
            public int? AreaScopeId { get; set; }

            /// <summary>Áreas de su configuración propia (filas vivas). Vacío = algoritmo.</summary>
            public List<int> Asignadas { get; set; } = new();
        }

        /// <summary>
        /// Los nodos vivos de <c>area_scope</c> con su padre. Es una tabla chica y se arma en
        /// memoria, igual que en AprobacionScopeResolver y en SalidaVisibilityResolver, pero se lee
        /// a lo sumo una vez por resolución y solo si alguna regla la necesita.
        /// </summary>
        private sealed class ArbolVivo
        {
            private readonly AppDbContext _ctx;
            private Dictionary<int, int?>? _padres;

            public ArbolVivo(AppDbContext ctx)
            {
                _ctx = ctx;
            }

            /// <summary><c>area_scope_id</c> → <c>area_scope_parent_id</c> de cada nodo vivo.</summary>
            public async Task<Dictionary<int, int?>> PadresAsync() =>
                _padres ??= await _ctx.AreaScope.AsNoTracking()
                    .Where(s => s.State)
                    .Select(s => new { s.AreaScopeId, s.AreaScopeParentId })
                    .ToDictionaryAsync(s => s.AreaScopeId, s => s.AreaScopeParentId);
        }
    }
}
