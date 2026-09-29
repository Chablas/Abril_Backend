using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Jerarquia
{
    /// <summary>
    /// Lo que el ÁRBOL DE ÁREAS y las obras dicen de la organización, sin opinar sobre para qué se
    /// usa: la cadena de cada nodo hacia la raíz, qué nodos son gerencias, la jefatura de cada nodo
    /// y el residente y el administrador de cada obra.
    ///
    /// Es la materia prima del algoritmo de los actores (<c>IActoresResolver</c>): de acá sale a
    /// quién señala el sistema cuando nadie personalizó nada. Lo personalizado (por área o por
    /// trabajador) NO se carga acá: lo lee el propio resolver, que es quien decide qué gana.
    ///
    /// Todo se carga por lotes: un número FIJO de consultas sea para 1 nodo o para todo el árbol.
    /// </summary>
    public static class EstructuraAreaLoader
    {
        /// <summary>Solo cuenta como candidato quien tiene correo corporativo.</summary>
        public const string EmailDomainCorp = "@abril.pe";

        /// <summary>
        /// Una persona que el árbol señala para un nodo o para una obra: una jefatura del área, el
        /// residente o el administrador de una obra. No dice para qué sirve — eso lo decide quien la
        /// consume.
        /// </summary>
        /// <param name="PersonId">
        /// Persona (<c>workers.person_id</c>). Hace falta para comparar por persona y no por ficha:
        /// un reingreso deja varias filas en <c>workers</c> para la misma persona.
        /// </param>
        /// <param name="CategoriaId">
        /// Categoría del puesto de esa persona (<c>workers.puesto_id → puesto.categoria_id</c>), null
        /// si su ficha no tiene puesto. Viaja con la persona porque hay reglas que miran QUÉ es el que
        /// salió elegido: los consolidadores de una jefatura son sus pares de la MISMA categoría.
        /// </param>
        public sealed record PersonaDeArea(
            int WorkerId, int? PersonId, string Email, string? Nombre, int? CategoriaId = null);

        /// <summary>El árbol vivo: el padre de cada nodo y cuáles son "Área de Gerencia".</summary>
        public sealed record Arbol(
            IReadOnlyDictionary<int, int?> PadreDe,
            IReadOnlySet<int> Gerencias);

        /// <summary>La estructura ya resuelta para un conjunto de nodos.</summary>
        /// <param name="JefaturaPorNodo">
        /// area_scope_id → la jefatura cuyo puesto apunta a ese nodo: el GERENTE en un "Área de
        /// Gerencia"; el SUB GERENTE, el JEFE y el RESIDENTE en cualquier otro nodo, en ese orden de
        /// precedencia (<see cref="CategoriaIds.JefaturaDeAreaPorPrecedencia"/>) y, entre iguales,
        /// por ficha más antigua.
        /// </param>
        /// <param name="ResidentePorProyecto">project_id → residente de la obra. OFICINA CENTRAL no está.</param>
        /// <param name="AdministradorPorProyecto">
        /// project_id → administrador de obra (<c>project.workers_coord_admin_id</c>). OFICINA CENTRAL
        /// tampoco está.
        /// </param>
        /// <param name="Obras">Los proyectos que son obras (todos menos OFICINA CENTRAL).</param>
        /// <param name="ObraDeJefatura">
        /// workers.id de cada jefatura → su obra vigente. Hace falta porque a un residente lo
        /// consolidan los residentes de su MISMA obra.
        /// </param>
        /// <param name="AdministradoresEnFunciones">
        /// Quienes administran alguna obra ACTIVA: son el caso «Administrador de obra». Una obra
        /// cerrada no cuenta, porque su administrador puede estar hoy en otro puesto.
        /// </param>
        public sealed record EstructuraArea(
            ILookup<int, PersonaDeArea> JefaturaPorNodo,
            IReadOnlyDictionary<int, PersonaDeArea> ResidentePorProyecto,
            IReadOnlyDictionary<int, PersonaDeArea> AdministradorPorProyecto,
            IReadOnlySet<int> Obras,
            IReadOnlyDictionary<int, int?> ObraDeJefatura,
            IReadOnlyList<PersonaDeArea> AdministradoresEnFunciones)
        {
            /// <summary>true = la ficha, o la misma persona con otra ficha, administra una obra activa.</summary>
            public bool EsAdministradorDeObra(int workerId, int? personId) =>
                AdministradoresEnFunciones.Any(a =>
                    a.WorkerId == workerId || (personId != null && a.PersonId == personId));
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

        /// <summary>El árbol vivo con el tipo de cada nodo, en una sola consulta.</summary>
        public static async Task<Arbol> CargarArbolConTiposAsync(AppDbContext ctx)
        {
            var scopes = await (
                from s in ctx.AreaScope.AsNoTracking()
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                where s.State
                select new { s.AreaScopeId, s.AreaScopeParentId, ai.AreaTypeId }
            ).ToListAsync();

            return new Arbol(
                scopes.ToDictionary(s => s.AreaScopeId, s => s.AreaScopeParentId),
                scopes.Where(s => s.AreaTypeId == AreaTypeIds.AreaDeGerencia)
                      .Select(s => s.AreaScopeId)
                      .ToHashSet());
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
        /// La jefatura de cada nodo y las dos personas de cada obra, en un número fijo de consultas.
        ///
        ///   • Por ÁREA: el trabajador cuyo puesto apunta a ese nodo con la categoría que le toca al
        ///     tipo de nodo. La categoría y el nodo salen del puesto (<c>workers</c> ya no los guarda).
        ///   • Por OBRA: <c>project.residente_workers_id</c> (aprueba las salidas del staff) y
        ///     <c>project.workers_coord_admin_id</c> (revisa la planilla, consolida y firma primero).
        ///     OFICINA CENTRAL no es una obra: ahí manda la jefatura del área.
        ///
        /// Solo entra gente que hoy está adentro (<see cref="WorkersEstadoIds.EstanAdentro"/>) y con
        /// correo corporativo: es a quien el sistema le va a pedir algo por correo. Lo que se asigna A
        /// MANO no pasa por este filtro — esa designación es explícita y la lee el resolver.
        /// </summary>
        public static async Task<EstructuraArea> CargarAsync(AppDbContext ctx, IReadOnlyCollection<int> nodos)
        {
            var ids = nodos as List<int> ?? nodos.ToList();

            // Las cuatro categorías se traen juntas y se filtra por tipo de nodo al armar el lookup:
            // una sola consulta en vez de una por categoría.
            var jefes = await (
                from w in ctx.Worker.AsNoTracking()
                join pu in ctx.Puesto.AsNoTracking() on w.PuestoId equals pu.PuestoId
                where w.State
                      && WorkersEstadoIds.EstanAdentro.Contains(w.WorkersEstadoId)
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
            var jefaturaPorNodo = jefes
                .Where(j => j.AreaTypeId == AreaTypeIds.AreaDeGerencia
                    ? j.CategoriaId == CategoriaIds.Gerente
                    : CategoriaIds.JefaturaDeAreaPorPrecedencia.Contains(j.CategoriaId))
                // Primero por PRECEDENCIA de categoría (SUB GERENTE → JEFE → RESIDENTE): en un área
                // con sub gerente y jefe manda el sub gerente. Es regla de negocio, no un desempate.
                .OrderBy(j => PrecedenciaDeJefatura(j.CategoriaId))
                // Y recién ahí el desempate entre iguales: la ficha más antigua. Ese sí es
                // arbitrario a propósito — lo que importa es que no cambie entre llamadas; el área
                // que quiera otro orden lo fija a mano en Revisores de Áreas.
                .ThenBy(j => j.Id)
                .ToLookup(
                    j => j.AreaScopeId,
                    j => new PersonaDeArea(j.Id, j.PersonId, j.EmailCorporativo!.Trim(), j.Nombre, j.CategoriaId));

            // Las DOS personas de la obra, cada una con su consulta y el mismo filtro. Se piden por
            // join explícito y no por navegación: `project` solo tiene mapeada la del coordinador, y
            // agregar la otra traería una FK sombra. Qué es una obra lo dice ObrasLoader, el mismo que
            // usa la visibilidad.
            var obras = await ObrasLoader.Obras(ctx)
                .Select(p => new { p.ProjectId, p.Active, p.ResidenteWorkersId, p.WorkersCoordAdminId })
                .ToListAsync();

            var personasDeObra = await PersonasAsync(
                ctx,
                obras.SelectMany(o => new[] { o.ResidenteWorkersId, o.WorkersCoordAdminId })
                     .Where(id => id != null)
                     .Select(id => id!.Value)
                     .Distinct()
                     .ToList());

            var residentePorProyecto       = new Dictionary<int, PersonaDeArea>();
            var administradorPorProyecto   = new Dictionary<int, PersonaDeArea>();
            var administradoresEnFunciones = new List<PersonaDeArea>();

            foreach (var o in obras)
            {
                if (o.ResidenteWorkersId != null
                    && personasDeObra.TryGetValue(o.ResidenteWorkersId.Value, out var res))
                    residentePorProyecto[o.ProjectId] = res;

                if (o.WorkersCoordAdminId != null
                    && personasDeObra.TryGetValue(o.WorkersCoordAdminId.Value, out var adm))
                {
                    administradorPorProyecto[o.ProjectId] = adm;
                    if (o.Active) administradoresEnFunciones.Add(adm);
                }
            }

            // La obra de cada jefatura: solo la mira la regla de los consolidadores de un residente
            // (sus pares de la misma obra), pero es una consulta y no depende de cuántos sean.
            var obraDeJefatura = await ObrasLoader.ObraVigentePorTrabajadorAsync(
                ctx, jefes.Select(j => j.Id).Distinct().ToList());

            return new EstructuraArea(
                jefaturaPorNodo,
                residentePorProyecto,
                administradorPorProyecto,
                obras.Select(o => o.ProjectId).ToHashSet(),
                obraDeJefatura,
                administradoresEnFunciones);
        }

        /// <summary>
        /// Las fichas indicadas como candidatas, indexadas por <c>workers.id</c>. Solo entran las
        /// vivas, de gente que está adentro y con correo corporativo — el mismo filtro que se le aplica
        /// a cualquier otro candidato que deduce el árbol.
        /// </summary>
        private static async Task<Dictionary<int, PersonaDeArea>> PersonasAsync(
            AppDbContext ctx, List<int> workerIds)
        {
            if (workerIds.Count == 0) return new Dictionary<int, PersonaDeArea>();

            var personas = await (
                from w in ctx.Worker.AsNoTracking()
                where workerIds.Contains(w.Id) && w.State
                      && WorkersEstadoIds.EstanAdentro.Contains(w.WorkersEstadoId)
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
