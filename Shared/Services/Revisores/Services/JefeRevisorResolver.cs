using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Jerarquia;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Revisores.Services
{
    /// <summary>
    /// Resolución del jefe/revisor de un trabajador en tres pasos:
    ///   1) <c>workers_revisores</c>: el jefe personalizado del trabajador (el checkbox del
    ///      formulario de trabajadores guarda uno; la tabla admite n por prioridad).
    ///   2) El revisor de su área: se parte del nodo area_scope del trabajador
    ///      (puesto.area_destino_scope_id) y se sube por el árbol hasta el primer nodo que
    ///      resuelva. En cada nodo, primero lo asignado a mano en <c>area_revisores</c>
    ///      (por proyecto y después por área) y detrás el ALGORITMO: el residente de la obra
    ///      (project.residente_workers_id) si el nodo filtra por proyecto, y si no el
    ///      Jefe del área — o el Gerente, si el nodo es "Área de Gerencia".
    ///   3) Fallback: el área de GTH (area_scope.email).
    ///
    /// El paso 2 ya no necesita que alguien cargue un revisor para resolver: el algoritmo lo deduce
    /// de la estructura, y Revisores de Áreas queda para sobreponerse a él. Un área sin Jefe no se
    /// queda sin revisor, sigue subiendo y acaba en el Gerente de su gerencia.
    ///
    /// "Nadie puede ser su propio jefe" rige en el paso 2 (ver <see cref="EsLaMismaPersona"/>),
    /// donde al revisor no lo elige nadie sino que se deriva del área. En el paso 1 NO rige: el
    /// jefe personalizado se elige a mano en el formulario de trabajadores y ahí el propio
    /// trabajador es una opción válida, así que si quedó guardado se respeta.
    ///
    /// Todo se resuelve por lotes: <see cref="ResolveManyAsync"/> hace un número FIJO de
    /// consultas sea para 1 o para 500 trabajadores, y <see cref="ResolveAsync"/> es un
    /// atajo sobre ella (una sola ruta de código, sin lógica duplicada).
    ///
    /// Lo mismo vale entre la resolución por trabajador y la previsualización por área
    /// (<see cref="ResolveByAreaScopeManyAsync"/>, la que pinta el campo "Jefe / revisor del área"
    /// en la ficha): el paso 2 de las dos sale de <see cref="Ranking"/> + <see cref="Elegir"/>,
    /// que son el único lugar donde se decide. Cada una solo arma el contexto (de quién estamos
    /// hablando, en qué nodo está y con qué obra) — ver el bloque "NÚCLEO ÚNICO DE DECISIÓN".
    /// Ninguna regla se reimplementa fuera de ahí, tampoco en el frontend, para que la ficha no
    /// pueda mostrar un jefe distinto del que va a recibir el correo.
    /// </summary>
    public class JefeRevisorResolver : IJefeRevisorResolver
    {
        private const string EmailDomainCorp = EstructuraAreaLoader.EmailDomainCorp;
        /// <summary>Nombre exacto del área en area_item cuyo area_scope.email es el fallback.</summary>
        private const string AreaGthNombre = "Gestión del Talento Humano";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public JefeRevisorResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<JefeRevisorResolution?> ResolveAsync(int workerId)
        {
            var resueltos = await ResolveManyAsync(new[] { workerId });
            return resueltos.TryGetValue(workerId, out var jefe) ? jefe : null;
        }

        public async Task<Dictionary<int, JefeRevisorResolution>> ResolveManyAsync(IReadOnlyCollection<int> workerIds)
        {
            var resultado = new Dictionary<int, JefeRevisorResolution>();

            var ids = workerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            // Ficha de cada trabajador pedido: su persona (para descartarse a sí mismo como jefe)
            // y su nodo de área (paso 2). Se trae una sola vez y la usan los dos pasos.
            var fichas = (await ctx.Worker.AsNoTracking()
                    .Where(w => ids.Contains(w.Id))
                    // El nodo de área sale del puesto: workers ya no lo guarda.
                    .Select(w => new
                    {
                        w.Id,
                        w.PersonId,
                        AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null
                    })
                    .ToListAsync())
                .ToDictionary(w => w.Id, w => (w.PersonId, w.AreaScopeId));

            // ── Paso 1: jefe personalizado (workers_revisores) ─────────────────────
            var directos = await (
                from r in ctx.WorkersRevisores.AsNoTracking()
                where r.State && r.Active && ids.Contains(r.SolicitanteId)
                join w in ctx.Worker.AsNoTracking() on r.RevisorId equals w.Id
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                select new
                {
                    r.SolicitanteId,
                    r.OrdenPrioridad,
                    r.WorkersRevisoresId,
                    RevisorWorkerId = w.Id,
                    RevisorPersonId = w.PersonId,
                    w.EmailCorporativo,
                    Nombre = w.Person != null ? w.Person.FullName : null,
                }
            ).ToListAsync();

            foreach (var grupo in directos.GroupBy(d => d.SolicitanteId))
            {
                // Acá NO se descarta al propio trabajador: este jefe se eligió a mano en el
                // formulario, que lo admite como opción (ver el comentario de la clase).
                var elegido = grupo
                    .OrderBy(d => d.OrdenPrioridad)
                    .ThenBy(d => d.WorkersRevisoresId)
                    .FirstOrDefault();

                if (elegido != null)
                    resultado[grupo.Key] = new JefeRevisorResolution(
                        elegido.RevisorWorkerId, null, elegido.EmailCorporativo!.Trim(),
                        elegido.Nombre, elegido.RevisorPersonId);
            }

            var pendientes = ids.Where(id => !resultado.ContainsKey(id)).ToList();
            if (pendientes.Count == 0) return resultado;

            // ── Paso 2: revisores del área (area_revisores, subiendo por el árbol) ──
            await ResolveByAreaAsync(ctx, pendientes, fichas, resultado);

            pendientes = ids.Where(id => !resultado.ContainsKey(id)).ToList();
            if (pendientes.Count == 0) return resultado;

            // ── Paso 3: fallback al área de GTH ───────────────────────────────────
            var gth = await GetFallbackGthAsync(ctx);

            if (gth != null)
                foreach (var id in pendientes)
                    resultado[id] = gth;

            return resultado;
        }

        public async Task<Dictionary<int, AreaScopeRevisorPreview>> ResolveByAreaScopeManyAsync(
            IReadOnlyCollection<int> areaScopeIds, int? workerId = null)
        {
            var resultado = new Dictionary<int, AreaScopeRevisorPreview>();

            var ids = areaScopeIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            // Persona del trabajador que se está editando, para descartarlo de sus propios
            // candidatos. Sin trabajador (alta nueva) no hay a quién descartar.
            int? personId = workerId is > 0
                ? await ctx.Worker.AsNoTracking()
                    .Where(w => w.Id == workerId.Value)
                    .Select(w => w.PersonId)
                    .FirstOrDefaultAsync()
                : null;

            var parentById = await EstructuraAreaLoader.CargarArbolAsync(ctx);

            var cadenaPorNodo = EstructuraAreaLoader.ConstruirCadenas(ids, parentById);
            var nodos = cadenaPorNodo.Values.SelectMany(c => c).Distinct().ToList();

            var nodosFiltranProyecto = (await ctx.GaSalidasAreaConfig.AsNoTracking()
                .Where(f => f.State && f.FiltraPorProyecto && nodos.Contains(f.AreaScopeId))
                .Select(f => f.AreaScopeId)
                .ToListAsync()).ToHashSet();

            var candidatos = await CargarCandidatosAsync(ctx, nodos);
            var porNodo = candidatos.ToLookup(c => c.AreaScopeId);
            var algoritmo = await CargarAlgoritmoAsync(ctx, nodos);
            var gth = await GetFallbackGthAsync(ctx);

            // Proyectos a evaluar en los nodos que filtran: TODOS los activos, no solo los que
            // tienen un revisor cargado. Desde que existe el algoritmo, un proyecto sin nada
            // asignado igual tiene revisor (su residente), así que limitarse a los configurados
            // dejaría fuera justo los que resuelve el sistema.
            var proyectosActivos = nodosFiltranProyecto.Count == 0
                ? new List<int>()
                : await ctx.Project.AsNoTracking()
                    .Where(p => p.State && p.Active)
                    .Select(p => p.ProjectId)
                    .ToListAsync();

            foreach (var (nodoId, cadena) in cadenaPorNodo)
            {
                // Sin proyecto: en todo nodo (filtrado o no) aplica el revisor a nivel de área,
                // igual que hace la resolución por trabajador cuando el trabajador no tiene proyecto.
                var area = Elegir(
                    Ranking(cadena, porNodo, nodosFiltranProyecto, proyecto: null, algoritmo, gth),
                    workerId, personId);

                // Por proyecto: solo tiene sentido si algún nodo de la cadena filtra por proyecto;
                // en el resto el revisor no depende de la obra y basta con el de área.
                var porProyecto = new Dictionary<int, RevisorElegido>();
                if (cadena.Any(nodosFiltranProyecto.Contains))
                    foreach (var projectId in proyectosActivos)
                        porProyecto[projectId] = Elegir(
                            Ranking(cadena, porNodo, nodosFiltranProyecto, projectId, algoritmo, gth),
                            workerId, personId);

                resultado[nodoId] = new AreaScopeRevisorPreview(area, porProyecto);
            }

            return resultado;
        }

        /// <summary>
        /// Nadie puede ser su propio jefe. Aplica al revisor que se deriva del área (paso 2) y a
        /// la previsualización por área, no al jefe personalizado del paso 1: ese se elige a mano
        /// y puede ser el propio trabajador.
        ///
        /// La comparación es por PERSONA cuando ambos lados la tienen, y por ficha
        /// (<c>workers.id</c>) como respaldo. Comparar solo por ficha dejaría pasar el caso de un
        /// reingreso: la misma persona tiene entonces varias filas en <c>workers</c> y el revisor
        /// del área puede estar configurado en una ficha distinta de la que se está resolviendo.
        /// </summary>
        private static bool EsLaMismaPersona(
            int candidatoWorkerId, int? candidatoPersonId, int workerId, int? personId)
            => candidatoWorkerId == workerId
               || (candidatoPersonId != null && personId != null && candidatoPersonId == personId);

        private static int? PersonaDe(
            IReadOnlyDictionary<int, (int? PersonId, int? AreaScopeId)> fichas, int workerId)
            => fichas.TryGetValue(workerId, out var ficha) ? ficha.PersonId : null;

        // ══════════════════════════════════════════════════════════════════════════
        // NÚCLEO ÚNICO DE DECISIÓN
        //
        // Ranking + Elegir son el algoritmo del paso 2 y no hay otra copia: por acá pasan
        // TANTO la resolución por trabajador (ResolveByAreaAsync, la que decide a quién se le
        // manda a aprobar una salida) COMO la previsualización por área
        // (ResolveByAreaScopeManyAsync, la que pinta el campo "Jefe / revisor del área" en la
        // ficha del trabajador). Antes eran dos implementaciones paralelas de las mismas
        // reglas y se desfasaron en silencio.
        //
        // Si hay que cambiar una regla —el orden de preferencia, el desempate, el trato de los
        // nodos que filtran por proyecto, quién queda descartado— se cambia acá y las dos
        // pantallas se mueven juntas. Cualquier `if` de estos que se copie afuera vuelve a
        // abrir la puerta al mismo bug: no hacerlo.
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Los candidatos del nodo y de sus ancestros EN ORDEN de resolución, con el fallback de
        /// GTH al final. El orden es la regla completa:
        ///
        ///   1. nodo más cercano al trabajador primero, después sus ancestros hasta la raíz;
        ///   2. dentro de cada nodo, primero TODO lo asignado a mano y después el algoritmo: lo que
        ///      alguien cargó en Revisores de Áreas se sobrepone siempre a lo que el sistema deduce;
        ///   3. lo asignado a mano de un nodo que NO filtra por proyecto son los revisores a nivel
        ///      de área (project_id NULL), por orden_prioridad;
        ///   4. lo asignado a mano de un nodo que SÍ filtra (ga_salidas_area_config) son primero
        ///      los revisores del <paramref name="proyecto"/> y detrás los de área del mismo nodo
        ///      — por eso un revisor cargado a nivel de área vale para todos los proyectos que no
        ///      tengan uno propio, y se sobrepone al que sacaría el algoritmo para ellos;
        ///   5. el algoritmo del nodo: el residente del proyecto si el nodo filtra y ese proyecto
        ///      es una obra con residente cargado; si no, el Jefe/Gerente del área (ver
        ///      <see cref="CargarAlgoritmoAsync"/>);
        ///   6. una sola entrada por persona: quien ya apareció no vuelve a aparecer más arriba
        ///      (hay jefes que son revisores de su área y también de la gerencia de la que cuelga);
        ///   7. el área de GTH al final, como último recurso.
        ///
        /// Que el algoritmo se evalúe POR NODO y no una sola vez es lo que hace que un área sin
        /// Jefe acabe en el Gerente de la gerencia de la que cuelga: el nodo del área no resuelve,
        /// se sigue subiendo, y arriba el tipo de nodo cambia la categoría que se busca.
        ///
        /// No descarta a nadie: eso lo hace <see cref="Elegir"/>, que es lo único que necesita
        /// saber de quién estamos hablando.
        /// </summary>
        private static List<JefeRevisorResolution> Ranking(
            List<int> cadena,
            ILookup<int, RevisorCandidato> porNodo,
            IReadOnlySet<int> nodosFiltranProyecto,
            int? proyecto,
            AlgoritmoContexto algoritmo,
            JefeRevisorResolution? fallbackGth)
        {
            var lista = new List<JefeRevisorResolution>();
            var vistos = new HashSet<string>();

            foreach (var nodo in cadena)
            {
                var delNodo = porNodo[nodo];
                var filtra = nodosFiltranProyecto.Contains(nodo) && proyecto != null;

                // Asignado a mano (con la herencia área → proyectos del punto 4).
                var aMano = filtra
                    ? PorPrioridad(delNodo.Where(c => c.ProjectId == proyecto))
                        .Concat(PorPrioridad(delNodo.Where(c => c.ProjectId == null)))
                    : PorPrioridad(delNodo.Where(c => c.ProjectId == null));

                // Algoritmo: el residente de la obra manda donde el nodo filtra por proyecto; el
                // Jefe/Gerente del área queda detrás y cubre el resto (nodo sin filtro, proyecto
                // sin residente y OFICINA CENTRAL, que no está en el diccionario de residentes).
                var porAlgoritmo = filtra
                    && algoritmo.ResidentePorProyecto.TryGetValue(proyecto!.Value, out var residente)
                        ? new[] { residente }.Concat(algoritmo.JefePorNodo[nodo])
                        : algoritmo.JefePorNodo[nodo];

                foreach (var c in aMano.Concat(porAlgoritmo))
                {
                    var clave = c.RevisorPersonId != null ? $"p{c.RevisorPersonId}" : $"w{c.RevisorWorkerId}";
                    if (vistos.Add(clave)) lista.Add(AResolucion(c, cadena[0]));
                }
            }

            if (fallbackGth != null) lista.Add(fallbackGth);
            return lista;
        }

        /// <summary>
        /// El ganador de un <see cref="Ranking"/>: el primer candidato que no sea el propio
        /// trabajador. Con <paramref name="workerId"/> en null no se descarta a nadie (alta nueva o
        /// previsualización sin trabajador).
        /// </summary>
        private static RevisorElegido Elegir(
            List<JefeRevisorResolution> ranking, int? workerId, int? personId)
        {
            if (ranking.Count == 0) return new RevisorElegido(null, false);
            if (workerId is not > 0) return new RevisorElegido(ranking[0], false);

            var elegido = ranking.FirstOrDefault(
                r => !EsLaMismaPersona(r.WorkerId ?? 0, r.PersonId, workerId.Value, personId));

            var esPropia = EsLaMismaPersona(
                ranking[0].WorkerId ?? 0, ranking[0].PersonId, workerId.Value, personId);

            return new RevisorElegido(elegido, esPropia);
        }

        /// <summary>Los candidatos de un nodo en el orden con el que se elige entre ellos.</summary>
        private static IEnumerable<RevisorCandidato> PorPrioridad(IEnumerable<RevisorCandidato> candidatos)
            => candidatos.OrderBy(c => c.OrdenPrioridad).ThenBy(c => c.AreaRevisoresId);

        /// <summary>
        /// El candidato como resultado, con el origen visto desde <paramref name="nodoConsultado"/>
        /// (el área por la que se preguntó, o sea el primero de la cadena).
        ///
        /// Solo cuenta como <see cref="RevisorOrigen.Personalizado"/> lo que alguien asignó en ESA
        /// área. Un revisor asignado a mano más arriba del árbol le llega a esta área porque el
        /// sistema fue a buscarlo, así que para ella es <see cref="RevisorOrigen.Algoritmo"/>: la
        /// fila no tiene revisor propio y no debe leerse como si lo tuviera.
        /// </summary>
        private static JefeRevisorResolution AResolucion(RevisorCandidato c, int nodoConsultado)
        {
            var origen = c.Origen == RevisorOrigen.Personalizado && c.AreaScopeId == nodoConsultado
                ? RevisorOrigen.Personalizado
                : RevisorOrigen.Algoritmo;

            return new JefeRevisorResolution(
                c.RevisorWorkerId, null, c.EmailCorporativo.Trim(), c.Nombre, c.RevisorPersonId, origen);
        }

        /// <summary>
        /// Un candidato a revisor de un nodo. Cubre las dos fuentes: una fila viva y activa de
        /// <c>area_revisores</c> (<see cref="RevisorOrigen.Personalizado"/>) y la que deduce el
        /// algoritmo de la estructura (<see cref="RevisorOrigen.Algoritmo"/>). Los dos llegan a
        /// <see cref="Ranking"/> con la misma forma para que las reglas de orden y descarte no
        /// tengan que distinguirlos.
        /// </summary>
        private sealed record RevisorCandidato(
            int AreaScopeId, int? ProjectId, int OrdenPrioridad, int AreaRevisoresId,
            int RevisorWorkerId, int? RevisorPersonId, string EmailCorporativo, string? Nombre,
            RevisorOrigen Origen);

        private static async Task<List<RevisorCandidato>> CargarCandidatosAsync(AppDbContext ctx, List<int> nodos)
        {
            return await (
                from r in ctx.AreaRevisores.AsNoTracking()
                where r.State && r.Active && nodos.Contains(r.AreaScopeId)
                join w in ctx.Worker.AsNoTracking() on r.RevisorId equals w.Id
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                select new RevisorCandidato(
                    r.AreaScopeId, r.ProjectId, r.OrdenPrioridad, r.AreaRevisoresId,
                    w.Id, w.PersonId, w.EmailCorporativo!, w.Person != null ? w.Person.FullName : null,
                    RevisorOrigen.Personalizado)
            ).ToListAsync();
        }

        // ── El algoritmo ────────────────────────────────────────────────────────
        //
        // Deduce el revisor de la estructura para que no haya que cargar uno por área ni por
        // área+proyecto. Dos formas, según qué se esté resolviendo:
        //
        //   • Por PROYECTO (nodo marcado "filtrar por proyecto" y proyecto que es una obra):
        //     el residente del proyecto, project.residente_workers_id.
        //   • Por ÁREA (todo lo demás — nodo sin filtro, proyecto sin residente, u OFICINA
        //     CENTRAL, que no es una obra y no tiene residente en el sentido de este algoritmo):
        //     el trabajador cuyo puesto apunta a ese nodo con la categoría que le toca al tipo de
        //     nodo — Jefe en un "Área Estándar", Gerente en un "Área de Gerencia".
        //
        // Si un nodo no resuelve, Ranking sigue subiendo por el árbol y vuelve a intentar arriba,
        // que es como un área sin jefe acaba en el gerente de su gerencia.

        /// <summary>
        /// Lo que el algoritmo necesita saber de la estructura, cargado de una vez para todos los
        /// nodos en juego. <see cref="Ranking"/> lo consulta en memoria, sin volver a la base.
        /// </summary>
        private sealed record AlgoritmoContexto(
            ILookup<int, RevisorCandidato> JefePorNodo,
            IReadOnlyDictionary<int, RevisorCandidato> ResidentePorProyecto);

        private static async Task<AlgoritmoContexto> CargarAlgoritmoAsync(AppDbContext ctx, List<int> nodos)
        {
            // La estructura (quién es el Jefe/Gerente de cada nodo y el residente de cada obra) la
            // carga EstructuraAreaLoader, compartido con ConsolidadorResolver: los dos algoritmos
            // tienen que deducir a la MISMA persona de la misma área. Acá solo se la viste de
            // candidato para que Ranking trate igual lo asignado a mano y lo deducido.
            var estructura = await EstructuraAreaLoader.CargarAsync(ctx, nodos);

            var jefePorNodo = estructura.JefePorNodo
                .SelectMany(g => g.Select(j => (Nodo: g.Key, Persona: j)))
                .ToLookup(x => x.Nodo, x => Candidato(x.Nodo, null, x.Persona));

            var residentePorProyecto = estructura.ResidentePorProyecto.ToDictionary(
                kv => kv.Key,
                kv => Candidato(0, kv.Key, kv.Value));

            return new AlgoritmoContexto(jefePorNodo, residentePorProyecto);
        }

        /// <summary>Una persona que dedujo el árbol, como candidato del ranking.</summary>
        private static RevisorCandidato Candidato(
            int areaScopeId, int? projectId, EstructuraAreaLoader.PersonaDeArea persona)
            => new(areaScopeId, projectId, 0, 0, persona.WorkerId, persona.PersonId,
                   persona.Email, persona.Nombre, RevisorOrigen.Algoritmo);

        private static async Task<JefeRevisorResolution?> GetFallbackGthAsync(AppDbContext ctx)
        {
            var gth = await (
                from s in ctx.AreaScope.AsNoTracking()
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                where s.State && ai.State
                      && ai.AreaItemName == AreaGthNombre
                      && s.Email != null && s.Email != ""
                orderby s.AreaScopeId
                select new { s.AreaScopeId, s.Email, ai.AreaItemName }
            ).FirstOrDefaultAsync();

            return gth == null
                ? null
                : new JefeRevisorResolution(
                    null, gth.AreaScopeId, gth.Email!.Trim(), gth.AreaItemName,
                    Origen: RevisorOrigen.Gth);
        }

        /// <summary>
        /// Busca revisores de área para los trabajadores indicados. Su trabajo es armar el
        /// CONTEXTO de cada uno —la cadena de nodos desde su area_scope hacia la raíz, la obra de
        /// su vinculación vigente y su persona— y pasárselo a <see cref="Ranking"/> /
        /// <see cref="Elegir"/>, que son quienes deciden. Las reglas (preferencia por proyecto,
        /// prioridad, "nadie es su propio jefe", subir por el árbol) están descritas ahí y no se
        /// repiten acá: es el mismo código que usa la previsualización de la ficha del trabajador.
        /// Escribe en <paramref name="resultado"/> solo los que resuelve.
        /// </summary>
        private static async Task ResolveByAreaAsync(
            AppDbContext ctx,
            List<int> workerIds,
            IReadOnlyDictionary<int, (int? PersonId, int? AreaScopeId)> fichas,
            Dictionary<int, JefeRevisorResolution> resultado)
        {
            var areaScopePorWorker = workerIds
                .Where(id => fichas.TryGetValue(id, out var ficha) && ficha.AreaScopeId != null)
                .ToDictionary(id => id, id => fichas[id].AreaScopeId!.Value);
            if (areaScopePorWorker.Count == 0) return;

            // Árbol vivo (tabla pequeña) para armar las cadenas trabajador → raíz en memoria.
            var parentById = await EstructuraAreaLoader.CargarArbolAsync(ctx);

            // Cadena por nodo (misma rutina que usa la previsualización por área) y de ahí por worker.
            var cadenaPorScope = EstructuraAreaLoader.ConstruirCadenas(
                areaScopePorWorker.Values.Distinct(), parentById);
            var cadenaPorWorker = areaScopePorWorker
                .Where(kv => cadenaPorScope.ContainsKey(kv.Value))
                .ToDictionary(kv => kv.Key, kv => cadenaPorScope[kv.Value]);
            if (cadenaPorWorker.Count == 0) return;

            var nodos = cadenaPorWorker.Values.SelectMany(c => c).Distinct().ToList();

            // Proyecto de cada trabajador (si pertenece a alguno) y nodos que filtran por proyecto.
            // La obra sale de su vinculación vigente (worker_vinculaciones con fecha_fin NULL), que
            // es la que mantiene GTH con "Cambiar obra / puesto de trabajo": mismo criterio y mismo
            // orden que usa la ficha del trabajador para previsualizar su revisor, para que las dos
            // pantallas no puedan mostrar jefes distintos. Un trabajador retirado no tiene
            // vinculación vigente y cae al revisor a nivel de área, que es lo correcto.
            var proyectoPorWorker = await ctx.WorkerVinculacion.AsNoTracking()
                .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null && v.ProyectoId != null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => new { v.WorkerId, v.ProyectoId })
                .ToListAsync();
            var proyectoDe = proyectoPorWorker
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First().ProyectoId);

            var nodosFiltranProyecto = (await ctx.GaSalidasAreaConfig.AsNoTracking()
                .Where(f => f.State && f.FiltraPorProyecto && nodos.Contains(f.AreaScopeId))
                .Select(f => f.AreaScopeId)
                .ToListAsync()).ToHashSet();

            // Revisores vivos + activos con correo válido de cualquier nodo involucrado, y la
            // estructura de la que el algoritmo deduce el resto. Sin filas en area_revisores ya no
            // se puede cortar acá: desde que existe el algoritmo, un área sin nada asignado igual
            // resuelve por su Jefe/Gerente o por el residente de la obra.
            var candidatos = await CargarCandidatosAsync(ctx, nodos);
            var porNodo = candidatos.ToLookup(c => c.AreaScopeId);
            var algoritmo = await CargarAlgoritmoAsync(ctx, nodos);

            foreach (var (workerId, cadena) in cadenaPorWorker)
            {
                proyectoDe.TryGetValue(workerId, out var proyectoTrabajador);

                // El mismo ranking y la misma elección que ve la ficha del trabajador (ver el
                // bloque "NÚCLEO ÚNICO DE DECISIÓN"). El fallback de GTH no entra acá: lo aplica
                // el paso 3 de ResolveManyAsync, que además cubre a los que ni siquiera tienen
                // área y por eso nunca llegan hasta este punto.
                var elegido = Elegir(
                    Ranking(cadena, porNodo, nodosFiltranProyecto, proyectoTrabajador, algoritmo,
                        fallbackGth: null),
                    workerId, PersonaDe(fichas, workerId));

                if (elegido.Revisor != null) resultado[workerId] = elegido.Revisor;
            }
        }
    }
}
