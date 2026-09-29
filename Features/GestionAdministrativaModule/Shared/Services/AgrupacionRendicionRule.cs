using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Jerarquia;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Qué trabajadores pueden ir JUNTOS en una planilla de rendición y en un Consolidado del S10
    /// —y por lo tanto en la planilla grupal, que se genera a partir del consolidado—.
    ///
    /// Dos reglas, las dos del 2026-09-21:
    ///
    ///   • <b>No se mezcla jefatura con no jefatura.</b> A un jefe lo firma su gerente; si comparte
    ///     documento con su propia gente, el documento entero termina cayendo en sus manos y
    ///     firmaría uno donde él mismo está incluido. Jefaturas entre sí SÍ pueden ir juntas
    ///     (ver <see cref="CategoriasJefatura"/>).
    ///
    ///   • <b>Todos cuelgan de un mismo nodo de área que no sea "Área de Gerencia".</b> Si el único
    ///     ancestro común es una gerencia (o no hay ninguno), no existe una sola jefatura por debajo
    ///     de ella que pueda firmar el documento completo.
    ///
    /// Vive en el Shared del módulo porque la aplican los DOS extremos del ciclo —rendir
    /// (<c>GestionSalidaService.RendirYGenerarPlanilla</c>) y consolidar
    /// (<c>ConsolidadoS10Service.UploadParaRendiciones</c>)—. Separadas se desfasarían, y bastaría
    /// con que una fuera más laxa para poder armar en un paso lo que el otro rechaza.
    /// </summary>
    public static class AgrupacionRendicionRule
    {
        /// <summary>
        /// Las categorías que cuentan como jefatura para esta regla.
        ///
        /// COORDINADOR queda FUERA a propósito: manda sobre su área para ver y para el algoritmo del
        /// revisor, pero para rendir es uno más y se agrupa con su gente.
        ///
        /// GERENTE y GERENTE GENERAL tampoco están, y no por olvido: no piden permiso de salida, así
        /// que nunca aparecen en una planilla. Si algún día lo hicieran, entrarían acá.
        /// </summary>
        /// Es la MISMA lista con la que <c>JefeRevisorResolver</c> decide a quién le toca gerencia
        /// en vez de la jefatura de su nodo: si se separaran, se podría armar un documento que
        /// después nadie de afuera pudiera firmar.
        public static readonly int[] CategoriasJefatura = CategoriaIds.JefaturaDeAreaPorPrecedencia;

        /// <summary>Un trabajador de los que se quieren agrupar, con lo que la regla necesita de él.</summary>
        public sealed record Participante(
            int WorkerId, string Nombre, int? CategoriaId, int? AreaScopeId);

        /// <summary>
        /// Ficha sin puesto = sin categoría: cuenta como NO jefatura. Es el default seguro — se
        /// agrupa con el resto en vez de quedar aislada en una planilla propia por un dato faltante.
        /// </summary>
        public static bool EsJefatura(int? categoriaId) =>
            categoriaId.HasValue && CategoriasJefatura.Contains(categoriaId.Value);

        /// <summary>
        /// Los datos que la regla necesita de cada trabajador. La categoría sale del puesto
        /// (<c>workers.puesto_id → puesto.categoria_id</c>), nunca de la columna congelada
        /// <c>workers.categoria_id</c>; el área, de <c>puesto.area_destino_scope_id</c>.
        /// </summary>
        public static async Task<List<Participante>> CargarParticipantesAsync(
            AppDbContext ctx, IReadOnlyCollection<int> workerIds)
        {
            var ids = workerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return new List<Participante>();

            return await ctx.Worker.AsNoTracking()
                .Where(w => ids.Contains(w.Id))
                .Select(w => new Participante(
                    w.Id,
                    w.Person != null ? w.Person.FullName! : "(sin nombre)",
                    w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                    w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null))
                .ToListAsync();
        }

        /// <summary>
        /// Valida que el conjunto pueda ir en un mismo documento y, si no, lanza el 400 con el
        /// motivo concreto. Con un solo trabajador no hay nada que validar: nunca se mezcla consigo
        /// mismo y su propia área es su ancestro común.
        /// </summary>
        /// <param name="queSeAgrupa">
        /// Cómo nombrar lo que se está juntando en el mensaje ("salidas", "planillas"): el error lo
        /// leen dos pantallas distintas y tiene que decir qué deseleccionar.
        /// </param>
        public static async Task ValidarAsync(
            AppDbContext ctx, IReadOnlyCollection<int> workerIds, string queSeAgrupa)
        {
            var participantes = await CargarParticipantesAsync(ctx, workerIds);
            if (participantes.Count < 2) return;

            // ── 1) Jefatura con no jefatura ────────────────────────────────────
            var jefaturas = participantes.Where(p => EsJefatura(p.CategoriaId)).ToList();
            var resto     = participantes.Where(p => !EsJefatura(p.CategoriaId)).ToList();

            if (jefaturas.Count > 0 && resto.Count > 0)
                throw new AbrilException(
                    $"No se pueden juntar {queSeAgrupa} de jefaturas con {queSeAgrupa} del resto del "
                    + $"equipo: {Nombres(jefaturas)} {(jefaturas.Count == 1 ? "tiene" : "tienen")} "
                    + $"jefatura y {Nombres(resto)} no. A una jefatura la firma su gerencia, así que "
                    + "va en un documento aparte.", 400);

            // ── 2) Un solo subárbol, y por debajo de la gerencia ───────────────
            var sinArea = participantes.Where(p => p.AreaScopeId == null).ToList();
            if (sinArea.Count > 0)
                throw new AbrilException(
                    $"{Nombres(sinArea)} no {(sinArea.Count == 1 ? "tiene" : "tienen")} área asignada "
                    + "(su puesto no apunta a ninguna), así que no se "
                    + $"{(sinArea.Count == 1 ? "puede" : "pueden")} agrupar con nadie. "
                    + "Corrige su puesto en la ficha del trabajador.", 400);

            var arbol   = await EstructuraAreaLoader.CargarArbolAsync(ctx);
            var cadenas = EstructuraAreaLoader.ConstruirCadenas(
                participantes.Select(p => p.AreaScopeId!.Value).Distinct(), arbol);

            var comun = AncestroComun(participantes, cadenas);
            if (comun == null)
            {
                var areas = await AreasDeAsync(ctx, participantes);
                throw new AbrilException(
                    $"Las {queSeAgrupa} seleccionadas son de áreas de ramas distintas ({areas}) y no "
                    + "comparten un área superior común: agrúpalas por área.", 400);
            }

            var tipoPorNodo = await TiposDeAreaAsync(ctx, new[] { comun.Value });
            if (tipoPorNodo.TryGetValue(comun.Value, out var tipo) && tipo == AreaTypeIds.AreaDeGerencia)
            {
                var areas = await AreasDeAsync(ctx, participantes);
                throw new AbrilException(
                    $"Las {queSeAgrupa} seleccionadas ({areas}) solo coinciden a nivel de gerencia, "
                    + "no de área: agrúpalas por área.", 400);
            }
        }

        /// <summary>
        /// El nodo común más PROFUNDO de todos los participantes: el primero de la cadena del primer
        /// trabajador (que va del nodo hacia la raíz) que también esté en la cadena de todos los
        /// demás. null si no comparten ninguno.
        /// </summary>
        private static int? AncestroComun(
            List<Participante> participantes, IReadOnlyDictionary<int, List<int>> cadenas)
        {
            var porNodo = participantes
                .Select(p => p.AreaScopeId!.Value)
                .Distinct()
                .Select(nodo => cadenas.TryGetValue(nodo, out var c) ? c : new List<int> { nodo })
                .ToList();

            foreach (var candidato in porNodo[0])
                if (porNodo.All(cadena => cadena.Contains(candidato)))
                    return candidato;

            return null;
        }

        private static async Task<Dictionary<int, int>> TiposDeAreaAsync(
            AppDbContext ctx, IReadOnlyCollection<int> nodos)
            => await (
                from s  in ctx.AreaScope.AsNoTracking()
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                where nodos.Contains(s.AreaScopeId)
                select new { s.AreaScopeId, ai.AreaTypeId }
            ).ToDictionaryAsync(x => x.AreaScopeId, x => x.AreaTypeId);

        /// <summary>Los nombres de un grupo, en una enumeración legible y sin repetir.</summary>
        private static string Nombres(IEnumerable<Participante> gente)
        {
            var nombres = gente
                .Select(p => p.Nombre)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            return nombres.Count switch
            {
                0 => "nadie",
                1 => nombres[0],
                _ => string.Join(", ", nombres.Take(nombres.Count - 1)) + " y " + nombres[^1],
            };
        }

        /// <summary>Las áreas en juego, para que el error diga qué se está mezclando.</summary>
        private static async Task<string> AreasDeAsync(
            AppDbContext ctx, List<Participante> participantes)
        {
            var nodos = participantes.Select(p => p.AreaScopeId!.Value).Distinct().ToList();

            var nombres = await (
                from s  in ctx.AreaScope.AsNoTracking()
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                where nodos.Contains(s.AreaScopeId)
                select ai.AreaItemName
            ).Distinct().OrderBy(n => n).ToListAsync();

            return string.Join(", ", nombres);
        }
    }
}
