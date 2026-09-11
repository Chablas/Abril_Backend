using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Jerarquia
{
    /// <summary>
    /// Lo que el ÁRBOL DE ÁREAS dice de la organización, sin opinar sobre para qué se usa: la
    /// cadena de cada nodo hacia la raíz, el Jefe/Gerente que le corresponde a cada nodo y el
    /// residente de cada obra.
    ///
    /// Existe porque hay dos algoritmos que derivan una persona de un área y tienen que derivar la
    /// MISMA: <c>JefeRevisorResolver</c> (quién revisa/aprueba una salida) y
    /// <c>ConsolidadorResolver</c> (quién puede consolidar el S10 de un trabajador). Lo que cambia
    /// entre los dos es qué tabla de asignaciones manuales leen y cómo eligen entre los candidatos
    /// —uno se queda con el primero, el otro con todos—; de dónde sale la estructura no cambia, y
    /// copiarlo habría dejado que las dos pantallas discreparan sobre quién es el jefe de un área.
    ///
    /// Todo se carga por lotes: un número FIJO de consultas sea para 1 nodo o para todo el árbol.
    /// </summary>
    public static class EstructuraAreaLoader
    {
        /// <summary>Solo cuenta como candidato quien tiene correo corporativo.</summary>
        public const string EmailDomainCorp = "@abril.pe";

        /// <summary>
        /// Identifica a OFICINA CENTRAL, que es una fila más de <c>project</c> sin bandera que la
        /// distinga de una obra: la única salida es el nombre normalizado (en prod va en mayúsculas
        /// y en dev como "Oficina Central"). Mismo criterio que usa el aviso de obra del Onboarding.
        /// </summary>
        private const string ProyectoOficinaCentral = "OFICINA CENTRAL";

        /// <summary>
        /// Una persona que el árbol señala para un nodo o para una obra: el Jefe/Gerente del área,
        /// o el residente del proyecto. No dice para qué sirve — eso lo decide quien la consume.
        /// </summary>
        /// <param name="PersonId">
        /// Persona (<c>workers.person_id</c>). Hace falta para comparar por persona y no por ficha:
        /// un reingreso deja varias filas en <c>workers</c> para la misma persona.
        /// </param>
        public sealed record PersonaDeArea(int WorkerId, int? PersonId, string Email, string? Nombre);

        /// <summary>La estructura ya resuelta para un conjunto de nodos.</summary>
        /// <param name="JefePorNodo">
        /// area_scope_id → Jefe (en un "Área Estándar") o Gerente (en un "Área de Gerencia") cuyo
        /// puesto apunta a ese nodo. Puede haber más de uno; van ordenados por ficha más antigua,
        /// que es un desempate arbitrario pero estable entre llamadas.
        /// </param>
        /// <param name="ResidentePorProyecto">project_id → residente de la obra. OFICINA CENTRAL no está.</param>
        public sealed record EstructuraArea(
            ILookup<int, PersonaDeArea> JefePorNodo,
            IReadOnlyDictionary<int, PersonaDeArea> ResidentePorProyecto);

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
        /// Jefes/Gerentes de cada nodo y residentes de cada obra, en dos consultas fijas.
        ///
        ///   • Por ÁREA: el trabajador cuyo puesto apunta a ese nodo con la categoría que le toca al
        ///     tipo de nodo — Jefe en un "Área Estándar", Gerente en un "Área de Gerencia". La
        ///     categoría sale del puesto (<c>workers</c> ya no la guarda) y el nodo también
        ///     (<c>puesto.area_destino_scope_id</c>).
        ///   • Por PROYECTO: <c>project.residente_workers_id</c>. OFICINA CENTRAL queda fuera: no es
        ///     una obra, así que ahí manda el jefe del área como en cualquier nodo sin filtro.
        /// </summary>
        public static async Task<EstructuraArea> CargarAsync(AppDbContext ctx, IReadOnlyCollection<int> nodos)
        {
            var ids = nodos as List<int> ?? nodos.ToList();

            // Las dos categorías se traen juntas y se filtra por tipo de nodo al armar el lookup:
            // una sola consulta en vez de dos.
            var jefes = await (
                from w in ctx.Worker.AsNoTracking()
                join pu in ctx.Puesto.AsNoTracking() on w.PuestoId equals pu.PuestoId
                where w.State
                      && pu.AreaDestinoScopeId != null
                      && ids.Contains(pu.AreaDestinoScopeId.Value)
                      && (pu.CategoriaId == CategoriaIds.Jefe || pu.CategoriaId == CategoriaIds.Gerente)
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
                .Where(j => j.CategoriaId == (j.AreaTypeId == AreaTypeIds.AreaDeGerencia
                    ? CategoriaIds.Gerente
                    : CategoriaIds.Jefe))
                // Desempate estable cuando un área tiene más de un jefe: la ficha más antigua.
                // Es arbitrario a propósito — lo que importa es que no cambie entre llamadas; el
                // área que quiera otro orden lo fija a mano en su pantalla de asignaciones.
                .OrderBy(j => j.Id)
                .ToLookup(
                    j => j.AreaScopeId,
                    j => new PersonaDeArea(j.Id, j.PersonId, j.EmailCorporativo!, j.Nombre));

            var residentes = await (
                from p in ctx.Project.AsNoTracking()
                where p.State && p.ResidenteWorkersId != null
                      && p.ProjectDescription != null
                      && p.ProjectDescription.ToUpper().Trim() != ProyectoOficinaCentral
                join w in ctx.Worker.AsNoTracking() on p.ResidenteWorkersId.Value equals w.Id
                where w.State
                      && w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                select new
                {
                    p.ProjectId, w.Id, w.PersonId, w.EmailCorporativo,
                    Nombre = w.Person != null ? w.Person.FullName : null,
                }
            ).ToListAsync();

            var residentePorProyecto = residentes.ToDictionary(
                r => r.ProjectId,
                r => new PersonaDeArea(r.Id, r.PersonId, r.EmailCorporativo!, r.Nombre));

            return new EstructuraArea(jefePorNodo, residentePorProyecto);
        }
    }
}
