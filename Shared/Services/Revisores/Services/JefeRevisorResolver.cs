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
            var fichas = await CargarFichasAsync(ctx, ids);

            // ── Paso 1: jefe personalizado (workers_revisores) ─────────────────────
            foreach (var (workerId, jefe) in await CargarPersonalizadosAsync(ctx, ids))
                resultado[workerId] = jefe;

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

        /// <summary>
        /// El jefe personalizado (<c>workers_revisores</c>) de cada trabajador que tenga uno vivo,
        /// activo y con correo corporativo. Es el paso 1 del resolver, extraído para que el
        /// firmante de un documento lea EXACTAMENTE lo mismo que la resolución por trabajador.
        ///
        /// Acá NO se descarta al propio trabajador: este jefe se eligió a mano en el formulario,
        /// que lo admite como opción (ver el comentario de la clase).
        /// </summary>
        private static async Task<Dictionary<int, JefeRevisorResolution>> CargarPersonalizadosAsync(
            AppDbContext ctx, List<int> ids)
        {
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
                    CategoriaId = w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                }
            ).ToListAsync();

            return directos
                .GroupBy(d => d.SolicitanteId)
                .Select(g => new
                {
                    WorkerId = g.Key,
                    Jefe = g.OrderBy(d => d.OrdenPrioridad).ThenBy(d => d.WorkersRevisoresId).First(),
                })
                .ToDictionary(
                    x => x.WorkerId,
                    x => new JefeRevisorResolution(
                        x.Jefe.RevisorWorkerId, null, x.Jefe.EmailCorporativo!.Trim(),
                        x.Jefe.Nombre, x.Jefe.RevisorPersonId,
                        CategoriaId: x.Jefe.CategoriaId));
        }

        public async Task<List<AprobadorDocumento>> ResolveAprobadoresDeDocumentoAsync(
            IReadOnlyCollection<int> workerIds, PasoAprobacion paso)
        {
            var uno = new Dictionary<int, IReadOnlyCollection<int>> { [0] = workerIds };
            var resueltos = await ResolveAprobadoresDeDocumentosAsync(uno, paso);
            return resueltos.GetValueOrDefault(0) ?? new List<AprobadorDocumento>();
        }

        public async Task<Dictionary<int, List<AprobadorDocumento>>> ResolveAprobadoresDeDocumentosAsync(
            IReadOnlyDictionary<int, IReadOnlyCollection<int>> workersPorDocumento,
            PasoAprobacion paso)
        {
            var resultado = new Dictionary<int, List<AprobadorDocumento>>();

            var docs = workersPorDocumento
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Where(id => id > 0).Distinct().ToList())
                .Where(kv => kv.Value.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (docs.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            // ── Todo el contexto de una vez (número FIJO de consultas) ─────────
            var todosLosWorkers = docs.Values.SelectMany(v => v).Distinct().ToList();

            var personalizados = await CargarPersonalizadosAsync(ctx, todosLosWorkers);
            var fichas         = await CargarFichasAsync(ctx, todosLosWorkers);
            var gth            = await GetFallbackGthAsync(ctx);

            var nodos = fichas.Values
                .Where(f => f.AreaScopeId != null)
                .Select(f => f.AreaScopeId!.Value)
                .Distinct()
                .ToList();

            var parentById    = await EstructuraAreaLoader.CargarArbolAsync(ctx);
            var cadenaPorNodo = EstructuraAreaLoader.ConstruirCadenas(nodos, parentById);
            var todosLosNodos = cadenaPorNodo.Values.SelectMany(c => c).Distinct().ToList();

            // Ambito Rendiciones: los asignados salen de area_revisores_rendicion y, en las areas
            // filtradas por proyecto, el algoritmo senala al ADMINISTRADOR DE OBRA y no al residente
            // —el residente aprueba la salida, no la planilla ni el consolidado—.
            var estructura = await EstructuraAreaLoader.CargarAsync(
                ctx, todosLosNodos, EstructuraAreaLoader.AmbitoRevisor.Rendiciones);

            var nodosFiltranProyecto = (await ctx.GaSalidasAreaConfig.AsNoTracking()
                .Where(f => f.State && f.FiltraPorProyecto && todosLosNodos.Contains(f.AreaScopeId))
                .Select(f => f.AreaScopeId)
                .ToListAsync()).ToHashSet();

            var proyectoPorWorker = await ProyectosVigentesAsync(ctx, todosLosWorkers);

            // ── Y cada documento se decide en memoria ──────────────────────────
            foreach (var (documentoId, ids) in docs)
                resultado[documentoId] = AprobadoresDe(
                    ids, paso, personalizados, fichas, cadenaPorNodo, estructura, gth,
                    nodosFiltranProyecto, proyectoPorWorker);

            return resultado;
        }

        /// <summary>
        /// La regla completa del firmante para UN documento, sin tocar la base: todo lo que
        /// necesita se lo pasa <see cref="ResolveFirmantesDeDocumentosAsync"/> ya cargado. Ver
        /// <see cref="IJefeRevisorResolver.ResolveFirmanteDeDocumentoAsync"/> para la regla narrada.
        /// </summary>
        private static List<AprobadorDocumento> AprobadoresDe(
            List<int> ids,
            PasoAprobacion paso,
            IReadOnlyDictionary<int, JefeRevisorResolution> personalizados,
            IReadOnlyDictionary<int, Ficha> fichas,
            IReadOnlyDictionary<int, List<int>> cadenaPorNodo,
            EstructuraAreaLoader.EstructuraArea estructura,
            JefeRevisorResolution? gth,
            IReadOnlySet<int> nodosFiltranProyecto,
            IReadOnlyDictionary<int, int?> proyectoPorWorker)
        {
            static List<AprobadorDocumento> Uno(JefeRevisorResolution? r) =>
                r == null ? new List<AprobadorDocumento>() : new() { new AprobadorDocumento(r, 1) };

            // 1) El jefe personalizado COMPARTIDO por todos. Solo manda si TODOS tienen uno y es la
            //    misma persona: con dos personalizados distintos no hay a quién darle el documento
            //    entero y se cae al área. Es el ÚNICO camino por el que el firmante puede quedar
            //    dentro del documento — se eligió a mano, ficha por ficha.
            var conPersonalizado = ids.Where(personalizados.ContainsKey).ToList();
            if (conPersonalizado.Count == ids.Count)
            {
                var elegidos = ids.Select(id => personalizados[id]).ToList();
                if (elegidos.Select(ClaveDe).Distinct().Count() == 1) return Uno(elegidos[0]);
            }

            // 2) El revisor del área, desde el nodo común del grupo hacia la raíz.
            var nodos = ids
                .Select(id => fichas.TryGetValue(id, out var f) ? f.AreaScopeId : null)
                .Where(n => n != null)
                .Select(n => n!.Value)
                .Distinct()
                .ToList();

            if (nodos.Count == 0) return Uno(gth);

            var cadena = CadenaComun(nodos, cadenaPorNodo);
            if (cadena == null) return Uno(gth);

            // La obra del documento, si es de UNA sola: en un área filtrada por proyecto la revisa y
            // la firma su gente (el administrador de obra y el residente), sin ningún otro permiso
            // de por medio. Con obras mezcladas no hay una sola a quién dársela y responde el área.
            var proyecto = ProyectoUnico(ids, proyectoPorWorker);

            // Si alguno del grupo es jefatura lo es todo el grupo —la regla de agrupación no deja
            // mezclarlos—, y a una jefatura la firma su gerencia.
            var soloGerencia = ids.Any(id => fichas.TryGetValue(id, out var f) && EsJefatura(f.CategoriaId));

            // Nadie que esté DENTRO del documento lo aprueba: si el nodo solo resuelve gente
            // incluida, se sigue subiendo. El único que sí puede es el personalizado del paso 1.
            var dentroWorkers = ids.ToHashSet();
            var dentroPersons = ids
                .Select(id => fichas.TryGetValue(id, out var f) ? f.PersonId : null)
                .Where(p => p != null)
                .Select(p => p!.Value)
                .ToHashSet();

            bool EstaDentro(int workerId, int? personId) =>
                dentroWorkers.Contains(workerId)
                || (personId != null && dentroPersons.Contains(personId.Value));

            return AprobadoresDeLaCadena(
                cadena, paso, estructura, nodosFiltranProyecto, proyecto, soloGerencia, EstaDentro, gth);
        }

        /// <summary>
        /// El recorrido de una rama área → raíz buscando a los que intervienen en
        /// <paramref name="paso"/>. Es el NÚCLEO de los aprobadores de rendiciones: lo usan tanto la
        /// resolución de un documento real (<see cref="AprobadoresDe"/>, que antes le arma el
        /// contexto del grupo) como la previsualización de la pantalla
        /// (<see cref="ResolveAprobadoresByAreaScopeManyAsync"/>, que pregunta por un área sin nadie
        /// dentro). Copiarlo afuera volvería a dejar que la pantalla muestre a alguien distinto de
        /// quien va a tener que aprobar.
        ///
        /// El primer nodo de la rama que resuelva ALGUIEN corta la búsqueda y devuelve a TODOS los
        /// suyos: acá no se elige un ganador como al aprobar una salida, porque en obra el
        /// consolidado lo firman dos personas.
        /// </summary>
        /// <param name="proyecto">La obra, o null para resolver a nivel de área.</param>
        /// <param name="soloGerencia">true = del algoritmo solo se aceptan GERENTES (el grupo es jefatura).</param>
        /// <param name="estaDentro">
        /// (workerId, personId) → true para quien no puede aprobar por estar incluido en el
        /// documento. En la previsualización por área no hay nadie dentro y nunca descarta.
        /// </param>
        private static List<AprobadorDocumento> AprobadoresDeLaCadena(
            List<int> cadena,
            PasoAprobacion paso,
            EstructuraAreaLoader.EstructuraArea estructura,
            IReadOnlySet<int> nodosFiltranProyecto,
            int? proyecto,
            bool soloGerencia,
            Func<int, int?, bool> estaDentro,
            JefeRevisorResolution? gth)
        {
            foreach (var nodo in cadena)
            {
                var filtra = nodosFiltranProyecto.Contains(nodo) && proyecto != null;
                var candidatos = new List<RevisorCandidato>();

                // a) Lo asignado a mano en Revisores de Áreas de Rendiciones, con la casilla de ESTE
                //    paso marcada. Manda sobre el algoritmo, como en todo el resto del resolver.
                var aMano = EstructuraAreaLoader
                    .RevisoresAsignados(estructura, nodo, filtra ? proyecto : null)
                    .Where(r => paso == PasoAprobacion.PrimeraRevision
                        ? r.ApruebaPrimeraRevision
                        : r.ApruebaConsolidado)
                    .ToList();

                if (aMano.Count > 0)
                {
                    candidatos.AddRange(aMano.Select(
                        r => Candidato(r.AreaScopeId, r.Persona, RevisorOrigen.Personalizado)));
                }
                else if (filtra)
                {
                    // b) El algoritmo de la obra: el ADMINISTRADOR DE OBRA revisa la planilla y
                    //    firma; el RESIDENTE solo firma. Ese es el reparto pedido, y el orden de la
                    //    lista es el orden de las firmas: primero el administrador.
                    if (estructura.AdministradorPorProyecto.TryGetValue(proyecto!.Value, out var adm))
                        candidatos.Add(Candidato(nodo, adm, RevisorOrigen.Algoritmo));

                    if (paso == PasoAprobacion.Consolidado
                        && estructura.ResidentePorProyecto.TryGetValue(proyecto!.Value, out var res))
                        candidatos.Add(Candidato(nodo, res, RevisorOrigen.Algoritmo));
                }

                // c) Y si el nodo no aportó nada por obra, la jefatura del área.
                if (candidatos.Count == 0)
                    candidatos.AddRange(estructura.JefePorNodo[nodo]
                        .Select(p => Candidato(nodo, p, RevisorOrigen.Algoritmo)));

                var validos = candidatos
                    .Where(c => !estaDentro(c.RevisorWorkerId, c.RevisorPersonId))
                    .Where(c => !soloGerencia || c.CategoriaId == CategoriaIds.Gerente)
                    .ToList();

                // El origen se mide desde el área por la que se preguntó, igual que en el ranking
                // del revisor: lo asignado más arriba del árbol le llega a esta área porque el
                // sistema fue a buscarlo, así que para ella es Algoritmo.
                if (validos.Count > 0)
                    return validos
                        .Select((c, i) => new AprobadorDocumento(AResolucion(c, cadena[0]), i + 1))
                        .ToList();
            }

            return gth == null
                ? new List<AprobadorDocumento>()
                : new List<AprobadorDocumento> { new(gth, 1) };
        }

        /// <summary>Identidad de un revisor para compararlo: la persona si la tiene, si no la ficha.</summary>
        private static string ClaveDe(JefeRevisorResolution r) =>
            r.PersonId != null ? $"p{r.PersonId}" : $"w{r.WorkerId}";

        /// <summary>
        /// La rama común del grupo: desde el nodo más profundo que TODOS comparten hacia la raíz.
        /// Con todos en la misma área es su propia cadena; con áreas distintas del mismo subárbol,
        /// la del ancestro que las une —y por eso el firmante sale de ahí para arriba y no de una
        /// de las subáreas, que no manda sobre las otras—.
        ///
        /// La regla de agrupación ya garantiza que ese ancestro existe y que no es una gerencia,
        /// pero si el documento es viejo (anterior a esa regla) puede no haberlo: null.
        /// </summary>
        private static List<int>? CadenaComun(
            List<int> nodos, IReadOnlyDictionary<int, List<int>> cadenaPorNodo)
        {
            var cadenas = nodos
                .Select(n => cadenaPorNodo.TryGetValue(n, out var c) ? c : new List<int> { n })
                .ToList();

            foreach (var (candidato, i) in cadenas[0].Select((c, i) => (c, i)))
                if (cadenas.All(c => c.Contains(candidato)))
                    return cadenas[0].Skip(i).ToList();

            return null;
        }

        /// <summary>
        /// La obra vigente de cada trabajador (vinculación con fecha_fin NULL). La regla vive en
        /// <see cref="ObrasLoader"/>, que es la misma que usa la visibilidad de las bandejas: quien
        /// firma por una obra tiene que poder ver los documentos de esa obra.
        /// </summary>
        private static Task<Dictionary<int, int?>> ProyectosVigentesAsync(
            AppDbContext ctx, List<int> ids)
            => ObrasLoader.ObraVigentePorTrabajadorAsync(ctx, ids);

        /// <summary>
        /// La obra del grupo, si es UNA sola. null si no comparten obra o si a alguno le falta: ahí
        /// no hay un revisor de proyecto único que pueda firmar el documento entero y la firma se
        /// queda en el área.
        /// </summary>
        private static int? ProyectoUnico(
            List<int> ids, IReadOnlyDictionary<int, int?> proyectoPorWorker)
        {
            var proyectos = ids
                .Select(id => proyectoPorWorker.TryGetValue(id, out var p) ? p : null)
                .ToList();

            if (proyectos.Any(p => p == null)) return null;

            var distintos = proyectos.Distinct().ToList();
            return distintos.Count == 1 ? distintos[0] : null;
        }

        public async Task<JefeRevisorResolution?> ResolveJefeDeAreaAsync(int workerId)
        {
            var resueltos = await ResolveJefeDeAreaManyAsync(new[] { workerId });
            return resueltos.TryGetValue(workerId, out var jefe) ? jefe : null;
        }

        public async Task<Dictionary<int, JefeRevisorResolution>> ResolveJefeDeAreaManyAsync(
            IReadOnlyCollection<int> workerIds)
        {
            var resultado = new Dictionary<int, JefeRevisorResolution>();

            var ids = workerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            // Solo el paso 2, y con el proyecto fuera de juego: el jefe del ÁREA es el que sale
            // cuando el nodo no filtra por obra. Sin paso 1 ni fallback de GTH — ver la interfaz.
            var fichas = await CargarFichasAsync(ctx, ids);
            await ResolveByAreaAsync(ctx, ids, fichas, resultado, ignorarProyecto: true);

            return resultado;
        }

        /// <summary>
        /// Ficha de cada trabajador pedido: su persona, su nodo de área y su categoría — las tres
        /// salen del puesto porque <c>workers</c> ya no las guarda.
        ///
        /// La categoría hace falta para saber si el trabajador ES jefatura: a una jefatura la
        /// aprueba un GERENTE y no la jefatura de su propio nodo (ver <see cref="Ranking"/>).
        /// </summary>
        private static async Task<Dictionary<int, Ficha>> CargarFichasAsync(
            AppDbContext ctx, List<int> ids)
            => (await ctx.Worker.AsNoTracking()
                    .Where(w => ids.Contains(w.Id))
                    .Select(w => new
                    {
                        w.Id,
                        w.PersonId,
                        AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                        CategoriaId = w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                    })
                    .ToListAsync())
                .ToDictionary(w => w.Id, w => new Ficha(w.PersonId, w.AreaScopeId, w.CategoriaId));

        /// <summary>Lo que el paso 2 necesita saber del trabajador que está resolviendo.</summary>
        private sealed record Ficha(int? PersonId, int? AreaScopeId, int? CategoriaId);

        /// <summary>
        /// True si el trabajador es jefatura de área (SUB GERENTE, JEFE o RESIDENTE). A una jefatura
        /// la aprueba su gerencia: el algoritmo salta las demás jefaturas del árbol y sube hasta
        /// encontrar un GERENTE. Ficha sin puesto = sin categoría = no es jefatura.
        /// </summary>
        private static bool EsJefatura(int? categoriaId) =>
            categoriaId.HasValue
            && CategoriaIds.JefaturaDeAreaPorPrecedencia.Contains(categoriaId.Value);

        public async Task<Dictionary<int, AreaScopeRevisorPreview>> ResolveByAreaScopeManyAsync(
            IReadOnlyCollection<int> areaScopeIds, int? workerId = null)
        {
            var resultado = new Dictionary<int, AreaScopeRevisorPreview>();

            var ids = areaScopeIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            // Ficha del trabajador que se está editando: su persona (para el aviso "es revisor de su
            // propia área") y su categoría, que decide si le toca la jefatura de su nodo o su
            // gerencia. Sin trabajador (alta nueva) se previsualiza la jefatura del área a secas.
            var ficha = workerId is > 0
                ? await ctx.Worker.AsNoTracking()
                    .Where(w => w.Id == workerId.Value)
                    .Select(w => new Ficha(
                        w.PersonId,
                        w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                        w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null))
                    .FirstOrDefaultAsync()
                : null;

            var personId     = ficha?.PersonId;
            var soloGerencia = EsJefatura(ficha?.CategoriaId);

            var parentById = await EstructuraAreaLoader.CargarArbolAsync(ctx);

            var cadenaPorNodo = EstructuraAreaLoader.ConstruirCadenas(ids, parentById);
            var nodos = cadenaPorNodo.Values.SelectMany(c => c).Distinct().ToList();

            var nodosFiltranProyecto = (await ctx.GaSalidasAreaConfig.AsNoTracking()
                .Where(f => f.State && f.FiltraPorProyecto && nodos.Contains(f.AreaScopeId))
                .Select(f => f.AreaScopeId)
                .ToListAsync()).ToHashSet();

            // La jefatura de cada nodo en juego (lo fijado en Revisores y lo que deduce el árbol) la
            // carga EstructuraAreaLoader, compartido con ConsolidadorResolver: los dos algoritmos
            // tienen que deducir a la MISMA persona de la misma área.
            var estructura = await EstructuraAreaLoader.CargarAsync(ctx, nodos);
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
                    Ranking(cadena, nodosFiltranProyecto, proyecto: null, estructura, gth, soloGerencia),
                    workerId, personId);

                // Por proyecto: solo tiene sentido si algún nodo de la cadena filtra por proyecto;
                // en el resto el revisor no depende de la obra y basta con el de área.
                var porProyecto = new Dictionary<int, RevisorElegido>();
                if (cadena.Any(nodosFiltranProyecto.Contains))
                    foreach (var projectId in proyectosActivos)
                        porProyecto[projectId] = Elegir(
                            Ranking(cadena, nodosFiltranProyecto, projectId, estructura, gth, soloGerencia),
                            workerId, personId);

                resultado[nodoId] = new AreaScopeRevisorPreview(area, porProyecto);
            }

            return resultado;
        }

        public async Task<Dictionary<int, AreaScopeAprobadoresPreview>> ResolveAprobadoresByAreaScopeManyAsync(
            IReadOnlyCollection<int> areaScopeIds)
        {
            var resultado = new Dictionary<int, AreaScopeAprobadoresPreview>();

            var ids = areaScopeIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            var parentById = await EstructuraAreaLoader.CargarArbolAsync(ctx);
            var cadenaPorNodo = EstructuraAreaLoader.ConstruirCadenas(ids, parentById);
            var nodos = cadenaPorNodo.Values.SelectMany(c => c).Distinct().ToList();

            var nodosFiltranProyecto = (await ctx.GaSalidasAreaConfig.AsNoTracking()
                .Where(f => f.State && f.FiltraPorProyecto && nodos.Contains(f.AreaScopeId))
                .Select(f => f.AreaScopeId)
                .ToListAsync()).ToHashSet();

            // Ámbito Rendiciones: los asignados salen de area_revisores_rendicion y, en las áreas
            // filtradas por proyecto, el algoritmo señala al administrador de obra.
            var estructura = await EstructuraAreaLoader.CargarAsync(
                ctx, nodos, EstructuraAreaLoader.AmbitoRevisor.Rendiciones);
            var gth = await GetFallbackGthAsync(ctx);

            // Los mismos proyectos que evalúa la previsualización de salidas: TODOS los activos y no
            // solo los que tienen algo asignado, porque el algoritmo también resuelve los que no.
            var proyectosActivos = nodosFiltranProyecto.Count == 0
                ? new List<int>()
                : await ctx.Project.AsNoTracking()
                    .Where(p => p.State && p.Active)
                    .Select(p => p.ProjectId)
                    .ToListAsync();

            // Los dos pasos se resuelven por separado —son dos preguntas distintas y pueden caer en
            // nodos distintos— y recién después se fusionan en una persona por fila.
            List<AprobadorDeArea> Aprobadores(List<int> cadena, int? proyecto) => Fusionar(
                AprobadoresDeLaCadena(
                    cadena, PasoAprobacion.PrimeraRevision, estructura, nodosFiltranProyecto,
                    proyecto, soloGerencia: false, estaDentro: (_, _) => false, gth),
                AprobadoresDeLaCadena(
                    cadena, PasoAprobacion.Consolidado, estructura, nodosFiltranProyecto,
                    proyecto, soloGerencia: false, estaDentro: (_, _) => false, gth));

            foreach (var (nodoId, cadena) in cadenaPorNodo)
            {
                var area = Aprobadores(cadena, proyecto: null);

                var porProyecto = new Dictionary<int, List<AprobadorDeArea>>();
                if (cadena.Any(nodosFiltranProyecto.Contains))
                    foreach (var projectId in proyectosActivos)
                        porProyecto[projectId] = Aprobadores(cadena, projectId);

                resultado[nodoId] = new AreaScopeAprobadoresPreview(area, porProyecto);
            }

            return resultado;
        }

        /// <summary>
        /// Las dos resoluciones en UNA lista de personas, cada una con los pasos en los que salió.
        /// Primero los de la primera revisión y detrás los que solo firman, que es el orden en que
        /// las dos cosas pasan (y, en una obra, el orden de las firmas: administrador y después
        /// residente). Alguien puede aparecer en uno solo de los dos pasos sin que nadie lo haya
        /// configurado: es lo que hace el algoritmo con el residente.
        /// </summary>
        private static List<AprobadorDeArea> Fusionar(
            List<AprobadorDocumento> primeraRevision, List<AprobadorDocumento> consolidado)
        {
            var orden = new List<JefeRevisorResolution>();
            var vistos = new HashSet<string>();
            var enPrimeraRevision = primeraRevision.Select(a => ClaveDe(a.Persona)).ToHashSet();
            var enConsolidado = consolidado.Select(a => ClaveDe(a.Persona)).ToHashSet();

            foreach (var a in primeraRevision.Concat(consolidado))
                if (vistos.Add(ClaveDe(a.Persona))) orden.Add(a.Persona);

            return orden
                .Select(p => new AprobadorDeArea(
                    p, enPrimeraRevision.Contains(ClaveDe(p)), enConsolidado.Contains(ClaveDe(p))))
                .ToList();
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

        private static int? PersonaDe(IReadOnlyDictionary<int, Ficha> fichas, int workerId)
            => fichas.TryGetValue(workerId, out var ficha) ? ficha.PersonId : null;

        private static int? CategoriaDe(IReadOnlyDictionary<int, Ficha> fichas, int workerId)
            => fichas.TryGetValue(workerId, out var ficha) ? ficha.CategoriaId : null;

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
        // También pasa por acá el "jefe del área" del aviso informativo de Solicitud de Salidas
        // (ResolveJefeDeAreaManyAsync): es la MISMA ruta, con el único cambio de no pasarle la
        // obra del trabajador. Que sea el mismo ranking es lo que garantiza que el jefe al que se
        // le informa sea exactamente el que la sección Revisores muestra en la fila del área.
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
        ///      tengan uno propio, y se sobrepone al que sacaría el algoritmo para ellos. Los puntos
        ///      3 y 4 los aplica <see cref="EstructuraAreaLoader.RevisoresAsignados"/>, que es la
        ///      misma lista con la que los consolidadores heredan la jefatura del área;
        ///   5. el algoritmo del nodo: el residente del proyecto si el nodo filtra y ese proyecto
        ///      es una obra con residente cargado; si no, el Jefe/Gerente del área (ver
        ///      <see cref="EstructuraAreaLoader.CargarAsync"/>);
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
        /// <param name="soloGerencia">
        /// true cuando el trabajador que se está resolviendo ES jefatura (SUB GERENTE, JEFE o
        /// RESIDENTE): a una jefatura la aprueba un GERENTE, así que del ALGORITMO solo se aceptan
        /// candidatos con esa categoría. Como un "Área Estándar" nunca aporta un gerente, el efecto
        /// es que se sigue subiendo por el árbol hasta la gerencia de la que cuelga.
        ///
        /// Lo asignado a mano NO se filtra: si alguien cargó un revisor para esa área en Revisores
        /// de Áreas, esa decisión manda sobre lo que el sistema deduzca, igual que en todo el resto
        /// del resolver.
        /// </param>
        private static List<JefeRevisorResolution> Ranking(
            List<int> cadena,
            IReadOnlySet<int> nodosFiltranProyecto,
            int? proyecto,
            EstructuraAreaLoader.EstructuraArea estructura,
            JefeRevisorResolution? fallbackGth,
            bool soloGerencia = false)
        {
            var lista = new List<JefeRevisorResolution>();
            var vistos = new HashSet<string>();

            foreach (var nodo in cadena)
            {
                var filtra = nodosFiltranProyecto.Contains(nodo) && proyecto != null;

                // Asignado a mano (con la herencia área → proyectos del punto 4). El orden lo pone
                // EstructuraAreaLoader porque los consolidadores leen exactamente la misma lista.
                var aMano = EstructuraAreaLoader
                    .RevisoresAsignados(estructura, nodo, filtra ? proyecto : null)
                    .Select(r => Candidato(r.AreaScopeId, r.Persona, RevisorOrigen.Personalizado));

                // Algoritmo: el residente de la obra manda donde el nodo filtra por proyecto; el
                // Jefe/Gerente del área queda detrás y cubre el resto (nodo sin filtro, proyecto
                // sin residente y OFICINA CENTRAL, que no está en el diccionario de residentes).
                // Quien de la obra entra al ranking lo decide el AMBITO de la estructura: el
                // residente cuando se esta resolviendo una salida, el administrador de obra cuando
                // se resuelve la planilla o el consolidado.
                var deducidos = filtra && estructura.TryPersonaDeLaObra(proyecto!.Value, out var deLaObra)
                    ? new[] { deLaObra }.Concat(estructura.JefePorNodo[nodo])
                    : estructura.JefePorNodo[nodo];

                // A una jefatura la aprueba su gerencia: del algoritmo solo pasan los gerentes, así
                // que los nodos no gerenciales no aportan nada y la búsqueda sube sola hasta la
                // gerencia. El residente de la obra tampoco pasa — es jefatura, no gerencia.
                if (soloGerencia)
                    deducidos = deducidos.Where(p => p.CategoriaId == CategoriaIds.Gerente);

                var porAlgoritmo = deducidos.Select(p => Candidato(nodo, p, RevisorOrigen.Algoritmo));

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
        /// El ganador de un <see cref="Ranking"/>: simplemente el primero.
        ///
        /// Ya NO se descarta al propio trabajador (2026-09-21). La autoaprobación la impide el mapa
        /// de categorías, que es donde la regla se lee de una sola vez: a quien no es jefatura lo
        /// aprueba la jefatura de su nodo —que por definición no puede ser él—, y a una jefatura la
        /// aprueba un GERENTE, categoría que tampoco puede ser la suya. Filtrar además por persona
        /// era una segunda regla que decía lo mismo con otras palabras y que, en un área con dos
        /// jefaturas, contradecía a la primera: dejaba que el JEFE aprobara al RESIDENTE de su
        /// propia área en vez de subir a la gerencia.
        ///
        /// La excepción deliberada sigue siendo el jefe personalizado del paso 1, que se elige a
        /// mano y puede ser el propio trabajador — ver el comentario de la clase.
        ///
        /// <paramref name="workerId"/> y <paramref name="personId"/> ya no eligen: solo sirven para
        /// <c>EsRevisorDeSuPropiaArea</c>, el aviso del formulario de trabajadores.
        /// </summary>
        private static RevisorElegido Elegir(
            List<JefeRevisorResolution> ranking, int? workerId, int? personId)
        {
            if (ranking.Count == 0) return new RevisorElegido(null, false);
            if (workerId is not > 0) return new RevisorElegido(ranking[0], false);

            // "Es revisor de su propia área": el trabajador figura entre los candidatos de su rama.
            // Es lo que el formulario avisa para que el revisor resuelto no se lea como un error de
            // configuración; con el filtro por persona fuera, se mira en todo el ranking y no solo
            // en el primero.
            var esPropia = ranking.Any(
                r => EsLaMismaPersona(r.WorkerId ?? 0, r.PersonId, workerId.Value, personId));

            return new RevisorElegido(ranking[0], esPropia);
        }

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
                c.RevisorWorkerId, null, c.EmailCorporativo.Trim(), c.Nombre, c.RevisorPersonId, origen,
                c.CategoriaId);
        }

        /// <summary>
        /// Un candidato a revisor de un nodo. Cubre las dos fuentes: una fila viva y activa de
        /// <c>area_revisores</c> (<see cref="RevisorOrigen.Personalizado"/>) y la que deduce el
        /// algoritmo de la estructura (<see cref="RevisorOrigen.Algoritmo"/>). Los dos llegan a
        /// <see cref="Ranking"/> con la misma forma para que las reglas de orden y descarte no
        /// tengan que distinguirlos. El orden entre los asignados a mano ya viene resuelto de
        /// <see cref="EstructuraAreaLoader.RevisoresAsignados"/>, así que acá no hace falta su
        /// prioridad.
        /// </summary>
        /// <param name="AreaScopeId">Nodo del que sale; solo importa para el origen relativo de lo asignado a mano.</param>
        private sealed record RevisorCandidato(
            int AreaScopeId, int RevisorWorkerId, int? RevisorPersonId, string EmailCorporativo, string? Nombre,
            RevisorOrigen Origen, int? CategoriaId);

        /// <summary>Una persona de la jefatura de un nodo, como candidato del ranking.</summary>
        private static RevisorCandidato Candidato(
            int areaScopeId, EstructuraAreaLoader.PersonaDeArea persona, RevisorOrigen origen)
            => new(areaScopeId, persona.WorkerId, persona.PersonId, persona.Email, persona.Nombre, origen,
                   persona.CategoriaId);

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
        //
        // Tanto lo asignado a mano como lo que deduce el algoritmo lo carga EstructuraAreaLoader,
        // una vez para todos los nodos en juego; Ranking lo consulta en memoria, sin volver a la
        // base, y solo lo viste de candidato para tratar igual las dos fuentes.

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
        ///
        /// Con <paramref name="ignorarProyecto"/> en true se resuelve el jefe del ÁREA y no el
        /// revisor: la obra del trabajador no se pide ni se pasa, así que los nodos que filtran por
        /// proyecto se comportan como cualquier otro —lo asignado a nivel de área y, detrás, el
        /// Jefe/Gerente— y el residente de la obra queda fuera. Es la MISMA elección de la fila sin
        /// proyecto de la sección Revisores, hecha con el mismo ranking: la única diferencia con el
        /// revisor es el contexto que se le arma.
        /// </summary>
        private static async Task ResolveByAreaAsync(
            AppDbContext ctx,
            List<int> workerIds,
            IReadOnlyDictionary<int, Ficha> fichas,
            Dictionary<int, JefeRevisorResolution> resultado,
            bool ignorarProyecto = false)
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
            // Con ignorarProyecto no se consulta: sin obra, Ranking trata a todos los nodos como
            // si no filtraran y el residente nunca entra al ranking.
            var proyectoDe = ignorarProyecto
                ? new Dictionary<int, int?>()
                : await ObrasLoader.ObraVigentePorTrabajadorAsync(ctx, workerIds);

            // Sin proyecto la bandera no cambia nada (Ranking solo filtra cuando hay obra), así que
            // esa consulta no se hace.
            var nodosFiltranProyecto = ignorarProyecto
                ? new HashSet<int>()
                : (await ctx.GaSalidasAreaConfig.AsNoTracking()
                    .Where(f => f.State && f.FiltraPorProyecto && nodos.Contains(f.AreaScopeId))
                    .Select(f => f.AreaScopeId)
                    .ToListAsync()).ToHashSet();

            // La jefatura de cualquier nodo involucrado: los revisores vivos + activos con correo
            // válido fijados en Revisores y la estructura de la que el algoritmo deduce el resto. Sin
            // filas en area_revisores ya no se puede cortar acá: desde que existe el algoritmo, un
            // área sin nada asignado igual resuelve por su Jefe/Gerente o por el residente de la obra.
            var estructura = await EstructuraAreaLoader.CargarAsync(ctx, nodos);

            foreach (var (workerId, cadena) in cadenaPorWorker)
            {
                proyectoDe.TryGetValue(workerId, out var proyectoTrabajador);

                // El mismo ranking y la misma elección que ve la ficha del trabajador (ver el
                // bloque "NÚCLEO ÚNICO DE DECISIÓN"). El fallback de GTH no entra acá: lo aplica
                // el paso 3 de ResolveManyAsync, que además cubre a los que ni siquiera tienen
                // área y por eso nunca llegan hasta este punto.
                var elegido = Elegir(
                    Ranking(cadena, nodosFiltranProyecto, proyectoTrabajador, estructura,
                        fallbackGth: null,
                        soloGerencia: EsJefatura(CategoriaDe(fichas, workerId))),
                    workerId, PersonaDe(fichas, workerId));

                if (elegido.Revisor != null) resultado[workerId] = elegido.Revisor;
            }
        }
    }
}
