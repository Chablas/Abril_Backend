using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Jerarquia
{
    /// <summary>
    /// Lo que el ÁRBOL DE ÁREAS dice de la organización, sin opinar sobre para qué se usa: la
    /// cadena de cada nodo hacia la raíz, la jefatura que se fijó a mano para cada nodo en Revisores,
    /// el Jefe/Gerente que le corresponde a cada nodo y el residente de cada obra.
    ///
    /// Existe porque hay dos algoritmos que derivan una persona de un área y tienen que derivar la
    /// MISMA: <c>JefeRevisorResolver</c> (quién revisa/aprueba una salida) y
    /// <c>ConsolidadorResolver</c> (quién puede consolidar el S10 de un trabajador). Lo que cambia
    /// entre los dos es cómo eligen entre los candidatos —uno se queda con el primero, el otro con
    /// todos— y que los consolidadores tienen además su propia tabla de asignaciones, que va antes
    /// que todo esto. Quién es el jefe de un área no cambia, y copiarlo habría dejado que las dos
    /// pantallas discreparan sobre él: pasó hasta el 2026-09-16, cuando la jefatura de Revisores
    /// todavía no estaba acá y un área con revisor puesto a mano le seguía avisando (y dejando
    /// consolidar) al Jefe por categoría.
    ///
    /// Todo se carga por lotes: un número FIJO de consultas sea para 1 nodo o para todo el árbol.
    /// </summary>
    public static class EstructuraAreaLoader
    {
        /// <summary>Solo cuenta como candidato quien tiene correo corporativo.</summary>
        public const string EmailDomainCorp = "@abril.pe";

        /// <summary>
        /// Para QUE se esta resolviendo. Cambia dos cosas y solo dos: de que tabla salen los
        /// asignados a mano, y a quien de la obra senala el algoritmo cuando el nodo filtra por
        /// proyecto. El recorrido del arbol y la jefatura por categoria son identicos.
        ///
        /// Existe desde el 2026-09-21, cuando entro el administrador de obra: hasta entonces
        /// <c>area_revisores</c> decidia las tres cosas (aprobar la salida, revisar la planilla y
        /// firmar el consolidado) y el residente era el unico de la obra en el algoritmo.
        /// </summary>
        public enum AmbitoRevisor
        {
            /// <summary>Aprobar la SALIDA: <c>area_revisores</c> y el RESIDENTE de la obra.</summary>
            Salidas,

            /// <summary>
            /// Primera revision de la planilla y firma del consolidado:
            /// <c>area_revisores_rendicion</c> y el ADMINISTRADOR DE OBRA.
            /// </summary>
            Rendiciones,
        }

        /// <summary>
        /// Una persona que el árbol señala para un nodo o para una obra: el Jefe/Gerente del área,
        /// o el residente del proyecto. No dice para qué sirve — eso lo decide quien la consume.
        /// </summary>
        /// <param name="PersonId">
        /// Persona (<c>workers.person_id</c>). Hace falta para comparar por persona y no por ficha:
        /// un reingreso deja varias filas en <c>workers</c> para la misma persona.
        /// </param>
        /// <param name="CategoriaId">
        /// Categoría del puesto de esa persona (<c>workers.puesto_id → puesto.categoria_id</c>), null
        /// si su ficha no tiene puesto. Viaja con la persona porque hay reglas que miran QUÉ es el
        /// que salió elegido y no solo de dónde salió: el aviso informativo al jefe del área de
        /// Solicitud de Salidas se dispara cuando el revisor es <c>CategoriaIds.Residente</c>, venga
        /// del algoritmo o asignado a mano. Sacarla acá evita una consulta extra por cada resolución.
        /// </param>
        public sealed record PersonaDeArea(
            int WorkerId, int? PersonId, string Email, string? Nombre, int? CategoriaId = null);

        /// <summary>
        /// Una fila viva y activa de <c>area_revisores</c>: la jefatura que alguien fijó a mano para
        /// un nodo en Solicitud de Salidas → Configuración → Revisores (o que el propio revisor
        /// ajustó en Delegación de Revisión). Se sobrepone a lo que deduce el árbol.
        /// </summary>
        /// <param name="ProjectId">NULL = a nivel de área; con valor = solo para ese proyecto del área.</param>
        /// <param name="Id"><c>area_revisores_id</c>: desempate estable entre filas de igual prioridad.</param>
        /// <param name="ApruebaPrimeraRevision">
        /// Solo en <see cref="AmbitoRevisor.Rendiciones"/>: su visto bueno hace falta en la primera
        /// revision. En Salidas siempre true — esa tabla no tiene la bandera y su fila no se filtra.
        /// </param>
        /// <param name="ApruebaConsolidado">Idem para la firma del consolidado.</param>
        public sealed record RevisorAsignado(
            int AreaScopeId, int? ProjectId, int OrdenPrioridad, int Id, PersonaDeArea Persona,
            bool ApruebaPrimeraRevision = true, bool ApruebaConsolidado = true);

        /// <summary>La estructura ya resuelta para un conjunto de nodos.</summary>
        /// <param name="RevisoresPorNodo">
        /// area_scope_id → la jefatura fijada a mano en Revisores para ese nodo, por área y por
        /// proyecto. Va sin ordenar: en qué orden manda lo decide <see cref="RevisoresAsignados"/>.
        /// </param>
        /// <param name="JefePorNodo">
        /// area_scope_id → Jefe (en un "Área Estándar") o Gerente (en un "Área de Gerencia") cuyo
        /// puesto apunta a ese nodo. Puede haber más de uno; van ordenados por ficha más antigua,
        /// que es un desempate arbitrario pero estable entre llamadas.
        /// </param>
        /// <param name="ResidentePorProyecto">project_id → residente de la obra. OFICINA CENTRAL no está.</param>
        /// <param name="AdministradorPorProyecto">
        /// project_id → administrador de obra (<c>project.workers_coord_admin_id</c>). Es quien
        /// revisa la planilla y firma el consolidado en las áreas filtradas por proyecto, donde el
        /// residente solo aprueba la salida. OFICINA CENTRAL tampoco está.
        /// </param>
        public sealed record EstructuraArea(
            ILookup<int, RevisorAsignado> RevisoresPorNodo,
            ILookup<int, PersonaDeArea> JefePorNodo,
            IReadOnlyDictionary<int, PersonaDeArea> ResidentePorProyecto,
            IReadOnlyDictionary<int, PersonaDeArea> AdministradorPorProyecto,
            AmbitoRevisor Ambito = AmbitoRevisor.Salidas)
        {
            /// <summary>
            /// Quien de la obra senala el algoritmo, segun para que se resolvio: el residente
            /// aprueba las salidas y el administrador de obra revisa la planilla y firma el
            /// consolidado. Es el unico lugar donde se elige entre los dos, para que nadie tenga
            /// que acordarse de mirar el diccionario correcto.
            /// </summary>
            public bool TryPersonaDeLaObra(int projectId, out PersonaDeArea persona)
                => (Ambito == AmbitoRevisor.Rendiciones
                        ? AdministradorPorProyecto
                        : ResidentePorProyecto)
                    .TryGetValue(projectId, out persona!);
        }

        /// <summary>Padre de cada nodo vivo del árbol (tabla chica: se trae entera).</summary>
        public static async Task<Dictionary<int, int?>> CargarArbolAsync(AppDbContext ctx)
        {
            var scopes = await ctx.AreaScope.AsNoTracking()
                .Where(s => s.State)
                .Select(s => new { s.AreaScopeId, s.AreaScopeParentId })
                .ToListAsync();
            return scopes.ToDictionary(s => s.AreaScopeId, s => s.AreaScopeParentId);
        }

        /// <summary>
        /// Cadena nodo → raíz de cada nodo pedido (el propio nodo primero), cortando ciclos por si
        /// el árbol quedó mal cargado.
        /// </summary>
        public static Dictionary<int, List<int>> ConstruirCadenas(
            IEnumerable<int> desdeIds, IReadOnlyDictionary<int, int?> parentById)
        {
            var cadenas = new Dictionary<int, List<int>>();
            foreach (var desde in desdeIds)
            {
                if (cadenas.ContainsKey(desde)) continue;

                var cadena = new List<int>();
                var visitados = new HashSet<int>();
                int? actual = desde;
                while (actual != null && visitados.Add(actual.Value))
                {
                    cadena.Add(actual.Value);
                    actual = parentById.TryGetValue(actual.Value, out var padre) ? padre : null;
                }
                if (cadena.Count > 0) cadenas[desde] = cadena;
            }
            return cadenas;
        }

        /// <summary>
        /// La jefatura de cada nodo y los residentes de cada obra, en tres consultas fijas.
        ///
        ///   • Fijada a MANO: las filas vivas y activas de <c>area_revisores</c> del nodo cuyo
        ///     revisor tiene correo corporativo. No se mira el estado de la ficha: la designación es
        ///     la que manda.
        ///   • Por ÁREA: el trabajador cuyo puesto apunta a ese nodo con la categoría que le toca al
        ///     tipo de nodo — Jefe en un "Área Estándar", Gerente en un "Área de Gerencia". La
        ///     categoría sale del puesto (<c>workers</c> ya no la guarda) y el nodo también
        ///     (<c>puesto.area_destino_scope_id</c>).
        ///   • Por PROYECTO: <c>project.residente_workers_id</c>. OFICINA CENTRAL queda fuera: no es
        ///     una obra, así que ahí manda el jefe del área como en cualquier nodo sin filtro.
        /// </summary>
        public static async Task<EstructuraArea> CargarAsync(
            AppDbContext ctx, IReadOnlyCollection<int> nodos,
            AmbitoRevisor ambito = AmbitoRevisor.Salidas)
        {
            var ids = nodos as List<int> ?? nodos.ToList();

            // Cada ambito tiene su propia tabla de asignaciones. Se proyectan a la MISMA forma para
            // que todo lo que sigue —el recorrido, el orden, el ranking— no tenga que distinguirlas.
            var revisores = ambito == AmbitoRevisor.Rendiciones
                ? await (
                    from r in ctx.AreaRevisoresRendicion.AsNoTracking()
                    where r.State && r.Active && ids.Contains(r.AreaScopeId)
                    join w in ctx.Worker.AsNoTracking() on r.RevisorId equals w.Id
                    where w.EmailCorporativo != null
                          && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                    select new
                    {
                        r.AreaScopeId,
                        r.ProjectId,
                        r.OrdenPrioridad,
                        AsignacionId = r.AreaRevisoresRendicionId,
                        w.Id,
                        w.PersonId,
                        w.EmailCorporativo,
                        Nombre = w.Person != null ? w.Person.FullName : null,
                        CategoriaId = w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                        r.ApruebaPrimeraRevision,
                        r.ApruebaConsolidado,
                    }
                ).ToListAsync()
                : await (
                    from r in ctx.AreaRevisores.AsNoTracking()
                    where r.State && r.Active && ids.Contains(r.AreaScopeId)
                    join w in ctx.Worker.AsNoTracking() on r.RevisorId equals w.Id
                    where w.EmailCorporativo != null
                          && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                    select new
                    {
                        r.AreaScopeId,
                        r.ProjectId,
                        r.OrdenPrioridad,
                        AsignacionId = r.AreaRevisoresId,
                        w.Id,
                        w.PersonId,
                        w.EmailCorporativo,
                        Nombre = w.Person != null ? w.Person.FullName : null,
                        CategoriaId = w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                        ApruebaPrimeraRevision = true,
                        ApruebaConsolidado = true,
                    }
                ).ToListAsync();

            var revisoresPorNodo = revisores.ToLookup(
                r => r.AreaScopeId,
                r => new RevisorAsignado(
                    r.AreaScopeId, r.ProjectId, r.OrdenPrioridad, r.AsignacionId,
                    new PersonaDeArea(r.Id, r.PersonId, r.EmailCorporativo!, r.Nombre, r.CategoriaId),
                    r.ApruebaPrimeraRevision, r.ApruebaConsolidado));

            // Las dos categorías se traen juntas y se filtra por tipo de nodo al armar el lookup:
            // una sola consulta en vez de dos.
            var jefes = await (
                from w in ctx.Worker.AsNoTracking()
                join pu in ctx.Puesto.AsNoTracking() on w.PuestoId equals pu.PuestoId
                where w.State
                      && pu.AreaDestinoScopeId != null
                      && ids.Contains(pu.AreaDestinoScopeId.Value)
                      && (pu.CategoriaId == CategoriaIds.Gerente
                          || CategoriaIds.JefaturaDeAreaPorPrecedencia.Contains(pu.CategoriaId))
                      && w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                join s in ctx.AreaScope.AsNoTracking() on pu.AreaDestinoScopeId.Value equals s.AreaScopeId
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                where s.State
                select new
                {
                    AreaScopeId = pu.AreaDestinoScopeId.Value,
                    pu.CategoriaId,
                    ai.AreaTypeId,
                    w.Id,
                    w.PersonId,
                    w.EmailCorporativo,
                    Nombre = w.Person != null ? w.Person.FullName : null,
                }
            ).ToListAsync();

            // La categoría que manda depende del tipo de nodo, así que un Jefe que cuelga de un
            // "Área de Gerencia" no cuenta ahí, ni un Gerente en un "Área Estándar".
            var jefePorNodo = jefes
                .Where(j => j.AreaTypeId == AreaTypeIds.AreaDeGerencia
                    ? j.CategoriaId == CategoriaIds.Gerente
                    : CategoriaIds.JefaturaDeAreaPorPrecedencia.Contains(j.CategoriaId))
                // Primero por PRECEDENCIA de categoría (SUB GERENTE → JEFE → RESIDENTE): en un área
                // con sub gerente y jefe manda el sub gerente. Es regla de negocio, no un desempate.
                .OrderBy(j => PrecedenciaDeJefatura(j.CategoriaId))
                // Y recién ahí el desempate entre iguales: la ficha más antigua. Ese sí es
                // arbitrario a propósito — lo que importa es que no cambie entre llamadas; el área
                // que quiera otro orden lo fija a mano en su pantalla de asignaciones.
                .ThenBy(j => j.Id)
                .ToLookup(
                    j => j.AreaScopeId,
                    j => new PersonaDeArea(
                        j.Id, j.PersonId, j.EmailCorporativo!, j.Nombre, j.CategoriaId));

            // Las DOS personas de la obra, cada una con su consulta y el mismo filtro: el residente
            // (project.residente_workers_id), que aprueba las salidas, y el ADMINISTRADOR DE OBRA
            // (project.workers_coord_admin_id — el campo que la pantalla de Proyectos llama
            // "Administrador de obra"), que desde el 2026-09-21 revisa la planilla y firma el
            // consolidado. Se piden por join explícito y no por navegación: `project` solo tiene
            // mapeada la del coordinador, y agregar la otra traería una FK sombra. Qué es una obra
            // (OFICINA CENTRAL no) lo dice ObrasLoader, el mismo que usa la visibilidad.
            var obras = await ObrasLoader.Obras(ctx)
                .Select(p => new { p.ProjectId, p.ResidenteWorkersId, p.WorkersCoordAdminId })
                .ToListAsync();

            var personasDeObra = await PersonasAsync(
                ctx,
                obras.SelectMany(o => new[] { o.ResidenteWorkersId, o.WorkersCoordAdminId })
                     .Where(id => id != null)
                     .Select(id => id!.Value)
                     .Distinct()
                     .ToList());

            var residentePorProyecto     = new Dictionary<int, PersonaDeArea>();
            var administradorPorProyecto = new Dictionary<int, PersonaDeArea>();

            foreach (var o in obras)
            {
                if (o.ResidenteWorkersId != null
                    && personasDeObra.TryGetValue(o.ResidenteWorkersId.Value, out var res))
                    residentePorProyecto[o.ProjectId] = res;

                if (o.WorkersCoordAdminId != null
                    && personasDeObra.TryGetValue(o.WorkersCoordAdminId.Value, out var adm))
                    administradorPorProyecto[o.ProjectId] = adm;
            }

            return new EstructuraArea(
                revisoresPorNodo, jefePorNodo, residentePorProyecto, administradorPorProyecto, ambito);
        }

        /// <summary>
        /// La jefatura fijada a mano para un nodo, en el orden en que manda:
        ///
        ///   • con <paramref name="proyectoSiFiltra"/> (el nodo filtra por proyecto y hay obra),
        ///     primero lo asignado a ese proyecto y detrás lo del área — por eso lo cargado a nivel
        ///     de área vale para todos los proyectos sin asignación propia y enmascara al residente;
        ///   • sin él, solo lo del área: las filas por proyecto de un nodo que no filtra no cuentan;
        ///   • dentro de cada grupo, por <c>orden_prioridad</c> y después por id.
        ///
        /// Es la misma lista para los dos algoritmos: el revisor la pone delante de lo que deduce el
        /// árbol y se queda con el primero que no sea el propio trabajador; los consolidadores la
        /// toman entera cuando el nodo no tiene consolidadores propios.
        /// </summary>
        public static List<RevisorAsignado> RevisoresAsignados(
            EstructuraArea estructura, int nodo, int? proyectoSiFiltra)
        {
            var delNodo = estructura.RevisoresPorNodo[nodo];
            var delArea = PorPrioridad(delNodo.Where(r => r.ProjectId == null));

            return proyectoSiFiltra == null
                ? delArea.ToList()
                : PorPrioridad(delNodo.Where(r => r.ProjectId == proyectoSiFiltra))
                    .Concat(delArea)
                    .ToList();
        }

        /// <summary>
        /// Las fichas indicadas como candidatas, indexadas por <c>workers.id</c>. Solo entran las
        /// vivas y con correo corporativo — el mismo filtro que se le aplica a cualquier otro
        /// candidato, para que nadie llegue al ranking sin poder recibir el correo que ese papel
        /// implica.
        /// </summary>
        private static async Task<Dictionary<int, PersonaDeArea>> PersonasAsync(
            AppDbContext ctx, List<int> workerIds)
        {
            if (workerIds.Count == 0) return new Dictionary<int, PersonaDeArea>();

            var personas = await (
                from w in ctx.Worker.AsNoTracking()
                where workerIds.Contains(w.Id) && w.State
                      && w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                select new
                {
                    w.Id, w.PersonId, w.EmailCorporativo,
                    Nombre = w.Person != null ? w.Person.FullName : null,
                    CategoriaId = w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                }
            ).ToListAsync();

            return personas.ToDictionary(
                x => x.Id,
                x => new PersonaDeArea(x.Id, x.PersonId, x.EmailCorporativo!.Trim(), x.Nombre, x.CategoriaId));
        }

        private static IEnumerable<RevisorAsignado> PorPrioridad(IEnumerable<RevisorAsignado> filas)
            => filas.OrderBy(r => r.OrdenPrioridad).ThenBy(r => r.Id);

        /// <summary>
        /// Posición de una categoría en la precedencia de la jefatura de un nodo: cuanto más chico,
        /// más manda. El orden sale de <see cref="CategoriaIds.JefaturaDeAreaPorPrecedencia"/> para
        /// que la regla esté escrita en un solo lugar; el Gerente va primero porque es la única
        /// jefatura posible en un "Área de Gerencia" y ahí nunca compite con las otras.
        /// </summary>
        private static int PrecedenciaDeJefatura(int categoriaId)
        {
            if (categoriaId == CategoriaIds.Gerente) return -1;

            var pos = Array.IndexOf(CategoriaIds.JefaturaDeAreaPorPrecedencia, categoriaId);
            return pos < 0 ? int.MaxValue : pos;
        }
    }
}
