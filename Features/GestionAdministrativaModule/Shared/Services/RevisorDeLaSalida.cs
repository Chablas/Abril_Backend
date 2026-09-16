using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Regla única de quién está APTO para decidir sobre la salida de un trabajador: <b>solo su
    /// revisor resuelto</b> por <c>IJefeRevisorResolver</c> —el jefe personalizado del trabajador o,
    /// si no tiene, el que sale de su área subiendo por el árbol—. Es el MISMO resolver que decide a
    /// quién se le mandan los correos del flujo, así que en la web decide exactamente quien los
    /// recibe y nadie más.
    ///
    /// La aplican las dos decisiones que son de la jefatura del trabajador: aprobar o rechazar la
    /// salida (Gestión de Salidas) y aprobar u observar el reembolso de su Consolidado del S10
    /// (Consolidados). Vive en el Shared del módulo para que las dos no puedan discrepar.
    ///
    /// Ver no es decidir: la visibilidad por área (<c>ISalidaVisibilityResolver</c>) da a VER las
    /// salidas de una rama —un gerente, recepción, GTH o el consolidador del área—, pero si no son
    /// su revisor no las deciden.
    ///
    /// "Nadie decide lo suyo" queda cubierto de arranque: el revisor que se deriva del área nunca es
    /// el propio trabajador (lo descarta <c>JefeRevisorResolver</c>), así que sobre lo propio solo
    /// decide quien tenga el jefe personalizado apuntándose a sí mismo.
    /// </summary>
    public static class RevisorDeLaSalida
    {
        private const string AreaGthNombre = "Gestión del Talento Humano";

        /// <summary>
        /// Lo que hay que saber del usuario logueado para responder si es el revisor de una salida:
        /// sus fichas de <c>workers</c> (puede tener varias por reingreso), las personas de esas
        /// fichas y los nodos de área de sus puestos. Se carga UNA vez por consulta y sirve para
        /// todas las filas de la página.
        /// </summary>
        public sealed record QuienDecide(
            HashSet<int> WorkerIds, HashSet<int> PersonIds, List<int> AreaScopeIds)
        {
            public static QuienDecide Nadie() => new(new(), new(), new());
        }

        public static async Task<QuienDecide> CargarQuienDecideAsync(AppDbContext ctx, int? userId)
        {
            if (!userId.HasValue) return QuienDecide.Nadie();

            var uid = userId.Value;
            var fichas = await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals (int?)p.PersonId
                where p.UserId == uid
                select new
                {
                    w.Id,
                    w.PersonId,
                    // El área sale del puesto: workers ya no la guarda.
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null
                }
            ).ToListAsync();

            if (fichas.Count == 0) return QuienDecide.Nadie();

            return new QuienDecide(
                fichas.Select(f => f.Id).ToHashSet(),
                fichas.Where(f => f.PersonId.HasValue).Select(f => f.PersonId!.Value).ToHashSet(),
                fichas.Where(f => f.AreaScopeId.HasValue).Select(f => f.AreaScopeId!.Value).Distinct().ToList());
        }

        /// <summary>
        /// Topología del árbol de áreas indexada por <c>area_scope_id</c>: nombre del nodo y su
        /// padre. Es una tabla chica (decenas de filas), se trae completa y se camina en memoria.
        /// No se filtra por <c>state</c> a propósito: un trabajador que quedó en un nodo dado de
        /// baja igual tiene que mostrar su área en vez de una celda vacía.
        /// </summary>
        public static async Task<Dictionary<int, (int? Padre, string Nombre)>> CargarArbolAsync(AppDbContext ctx)
        {
            return await (
                from sc in ctx.AreaScope
                join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                select new { sc.AreaScopeId, sc.AreaScopeParentId, Nombre = it.AreaItemName }
            ).ToDictionaryAsync(
                x => x.AreaScopeId,
                x => (Padre: x.AreaScopeParentId, Nombre: x.Nombre));
        }

        /// <summary>
        /// El cálculo puro, sin ir a la base, para poder marcarlo fila por fila en un listado.
        ///
        /// Dos formas de revisor, según cómo lo resolvió <c>IJefeRevisorResolver</c>:
        ///   • una PERSONA (jefe personalizado o revisor de área) → apto si es una de las fichas
        ///     del usuario. Se compara también por <c>person_id</c>: un reingreso deja varias
        ///     fichas de la misma persona y el revisor puede estar configurado en cualquiera.
        ///   • un ÁREA (el fallback de GTH, que es un correo de área y no una persona) → apto
        ///     cualquiera que cuelgue de ese nodo, o sea todo GTH. Es el mismo criterio con el que
        ///     <c>SalidaVisibilityResolver</c> les da a ver todas las salidas: sin esto, las
        ///     salidas que caen al fallback no las podría decidir nadie desde la web.
        ///
        /// Y un tercer caso que no es un revisor sino su ausencia: cuando el resolver no devuelve
        /// NADA (trabajador sin área ni jefe personalizado, con el área de GTH sin correo cargado)
        /// también decide GTH. Es el caso que el correo al solicitante ya anuncia como "sin
        /// jefatura inmediata identificada": sin esta rama esas salidas quedarían sin nadie que las
        /// pueda decidir.
        /// </summary>
        /// <param name="arbol">
        /// Solo se consulta cuando el revisor no es una persona; en el caso normal puede ir vacío.
        /// </param>
        public static bool EsElRevisor(
            QuienDecide quien,
            JefeRevisorResolution? revisor,
            IReadOnlyDictionary<int, (int? Padre, string Nombre)> arbol)
        {
            if (quien.WorkerIds.Count == 0) return false;

            if (revisor?.WorkerId != null)
                return quien.WorkerIds.Contains(revisor.WorkerId.Value)
                    || (revisor.PersonId.HasValue && quien.PersonIds.Contains(revisor.PersonId.Value));

            // Revisor de ÁREA: el nodo lo dio el resolver, así que se compara por id.
            if (revisor?.AreaScopeId != null)
                return quien.AreaScopeIds.Any(mio => CuelgaDe(mio, revisor.AreaScopeId.Value, arbol));

            // Sin revisor resuelto: decide GTH igual que en el fallback. Acá se compara por NOMBRE
            // subiendo por la cadena (mismo criterio que SalidaVisibilityResolver) y no eligiendo
            // un nodo llamado GTH: el árbol admite nombres repetidos, y lo que se pregunta es si el
            // usuario pertenece a GTH, no cuál de los nodos es "el" de GTH.
            return quien.AreaScopeIds.Any(mio => CuelgaDeAreaLlamada(mio, AreaGthNombre, arbol));
        }

        /// <summary>
        /// True si <paramref name="nodoId"/> es <paramref name="ancestroId"/> o desciende de él.
        /// Camina hacia la raíz y corta ciclos por si el árbol quedó mal.
        /// </summary>
        private static bool CuelgaDe(
            int nodoId, int ancestroId,
            IReadOnlyDictionary<int, (int? Padre, string Nombre)> arbol)
        {
            var vistos = new HashSet<int>();
            int? actual = nodoId;
            while (actual.HasValue && vistos.Add(actual.Value))
            {
                if (actual.Value == ancestroId) return true;
                if (!arbol.TryGetValue(actual.Value, out var nodo)) return false;
                actual = nodo.Padre;
            }
            return false;
        }

        /// <summary>
        /// True si <paramref name="nodoId"/> o alguno de sus ancestros se llama
        /// <paramref name="nombre"/>. Corta ciclos igual que <see cref="CuelgaDe"/>.
        /// </summary>
        private static bool CuelgaDeAreaLlamada(
            int nodoId, string nombre,
            IReadOnlyDictionary<int, (int? Padre, string Nombre)> arbol)
        {
            var vistos = new HashSet<int>();
            int? actual = nodoId;
            while (actual.HasValue && vistos.Add(actual.Value)
                   && arbol.TryGetValue(actual.Value, out var nodo))
            {
                if (string.Equals(nodo.Nombre, nombre, StringComparison.OrdinalIgnoreCase)) return true;
                actual = nodo.Padre;
            }
            return false;
        }
    }
}
