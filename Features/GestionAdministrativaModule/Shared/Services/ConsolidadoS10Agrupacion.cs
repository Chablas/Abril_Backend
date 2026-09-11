using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Qué planillas pueden compartir un Consolidado del S10. El consolidado es UN registro en el
    /// S10 y puede cubrir varias planillas —de uno o de varios trabajadores— con dos condiciones:
    ///
    ///   1. <b>Una sola razón social.</b> El registro del S10 es de una empresa, así que no puede
    ///      mezclar trabajadores de dos razones sociales. Un único trabajador cumple siempre, tenga
    ///      o no la razón social cargada: todas sus planillas son de la misma.
    ///   2. <b>El documento se reemplaza entero.</b> Si una planilla ya tiene un consolidado
    ///      compartido, las demás planillas de ese consolidado que siguen con el reembolso abierto
    ///      van con ella (su <see cref="Conjunto"/>): dejarlas en el viejo las dejaría respaldadas
    ///      por un registro del S10 que ya no es el vigente. Las que ya tienen el reembolso decidido
    ///      se quedan con el viejo, que es justo el documento que se firmó.
    ///
    /// Vive en el Shared del módulo porque la aplica la subida (<see cref="ConsolidadoS10Service"/>)
    /// y la anticipan las pantallas (Gestión de Rendiciones arma el conjunto de cada fila y Mis
    /// Rendiciones apaga el botón de las compartidas): con una copia en cada lado, el botón y el
    /// servidor podrían discrepar.
    /// </summary>
    public static class ConsolidadoS10Agrupacion
    {
        /// <summary>Lo que hace falta saber de una planilla para agruparla.</summary>
        public sealed class PlanillaParaAgrupar
        {
            public int RendicionId { get; init; }

            /// <summary>
            /// True si el reembolso de TODAS sus salidas sigue por decidir (Pendiente u Observado):
            /// solo entonces se le puede cambiar el consolidado. Basta una salida ya decidida para
            /// congelarlo, porque la firma de la jefatura quedó estampada sobre ese documento.
            /// </summary>
            public bool ReembolsoAbierto { get; init; }

            /// <summary>Trabajadores de TODAS sus salidas, sin recortar por visibilidad.</summary>
            public List<int> WorkerIds { get; init; } = new();
        }

        /// <summary>Una razón social resuelta (<c>contributor</c>). Id null = el trabajador no tiene ninguna.</summary>
        public sealed record RazonSocial(int? Id, string? Nombre);

        /// <summary>
        /// Estado del reembolso y trabajadores de N planillas, mirando TODAS sus salidas (sin
        /// recorte de visibilidad: el consolidado cubre los documentos enteros). Un roundtrip. Las
        /// planillas sin salidas no aparecen.
        /// </summary>
        public static async Task<Dictionary<int, PlanillaParaAgrupar>> LoadPlanillasAsync(
            AppDbContext ctx, IReadOnlyCollection<int> rendicionIds)
        {
            var ids = rendicionIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var salidas = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null && ids.Contains(s.RendicionId.Value))
                .Select(s => new { RendicionId = s.RendicionId!.Value, s.WorkerId, s.EstadoReembolsoId })
                .ToListAsync();

            return salidas
                .GroupBy(s => s.RendicionId)
                .ToDictionary(g => g.Key, g => new PlanillaParaAgrupar
                {
                    RendicionId      = g.Key,
                    ReembolsoAbierto = g.All(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Pendiente
                                               || s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado),
                    WorkerIds        = g.Select(s => s.WorkerId).Distinct().ToList(),
                });
        }

        /// <summary>
        /// Las planillas que cubriría el consolidado adjuntado desde <paramref name="rendicionId"/>:
        /// ella misma primero y, si ya tiene uno, las demás planillas de ese consolidado que siguen
        /// con el reembolso abierto (regla 2).
        /// </summary>
        /// <param name="actual">Consolidado vigente de la planilla, o null si todavía no tiene.</param>
        /// <param name="planillas">
        /// Tiene que traer a las planillas de <paramref name="actual"/>: las que falten se tratan
        /// como cerradas.
        /// </param>
        public static List<int> Conjunto(
            int rendicionId,
            ConsolidadoS10Dto? actual,
            IReadOnlyDictionary<int, PlanillaParaAgrupar> planillas)
        {
            var conjunto = new List<int> { rendicionId };
            if (actual == null) return conjunto;

            foreach (var otra in actual.Rendiciones)
                if (otra.Id != rendicionId
                    && planillas.TryGetValue(otra.Id, out var p) && p.ReembolsoAbierto)
                    conjunto.Add(otra.Id);

            return conjunto;
        }

        /// <summary>
        /// Razón social vigente de N trabajadores, con su nombre. La regla de cuál es la vigente
        /// (vinculación abierta, y si no la de la ficha) es la de
        /// <see cref="RazonSocialCuposHelper.RazonSocialVigente"/>: no se repite acá. Dos
        /// roundtrips fijos: las fichas y los nombres de las empresas.
        /// </summary>
        public static async Task<Dictionary<int, RazonSocial>> LoadRazonSocialAsync(
            AppDbContext ctx, IReadOnlyCollection<int> workerIds)
        {
            var ids = workerIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var fichas = await RazonSocialCuposHelper
                .RazonSocialVigente(ctx, ctx.Worker.Where(w => ids.Contains(w.Id)))
                .ToListAsync();

            var contributorIds = fichas
                .Where(f => f.ContributorId != null)
                .Select(f => f.ContributorId!.Value)
                .Distinct()
                .ToList();

            var nombres = contributorIds.Count == 0
                ? new Dictionary<int, string?>()
                : await ctx.Contributor
                    .Where(c => contributorIds.Contains(c.ContributorId))
                    .ToDictionaryAsync(c => c.ContributorId, c => (string?)c.ContributorName);

            return fichas.ToDictionary(
                f => f.WorkerId,
                f => new RazonSocial(
                    f.ContributorId,
                    f.ContributorId != null && nombres.TryGetValue(f.ContributorId.Value, out var n) ? n : null));
        }

        /// <summary>
        /// La razón social de un grupo de trabajadores si es UNA sola y todos la tienen cargada; null
        /// si se mezclan o si a alguno le falta. Es la que muestra la pantalla, y con la que evita
        /// ofrecer juntar planillas que la subida va a rechazar.
        /// </summary>
        public static RazonSocial? RazonSocialComun(
            IEnumerable<int> workerIds, IReadOnlyDictionary<int, RazonSocial> razones)
        {
            RazonSocial? comun = null;
            foreach (var workerId in workerIds.Distinct())
            {
                if (!razones.TryGetValue(workerId, out var razon) || razon.Id == null) return null;
                if (comun == null) comun = razon;
                else if (comun.Id != razon.Id) return null;
            }
            return comun;
        }

        /// <summary>
        /// Aplica la regla 1 a las planillas que va a cubrir un consolidado. Devuelve null si
        /// cumplen, o el motivo listo para mostrar si no: qué razones sociales se mezclan y en qué
        /// planillas, o quién no tiene razón social cargada.
        /// </summary>
        /// <param name="planillas">Las de <paramref name="rendicionIds"/>, con sus trabajadores.</param>
        /// <param name="codigoPorRendicion">Código REN-AAAA-NNNN de cada planilla, para el mensaje.</param>
        public static async Task<string?> ValidarRazonSocialAsync(
            AppDbContext ctx,
            IReadOnlyCollection<int> rendicionIds,
            IReadOnlyDictionary<int, PlanillaParaAgrupar> planillas,
            IReadOnlyDictionary<int, string> codigoPorRendicion)
        {
            var trabajadoresPorPlanilla = rendicionIds.Distinct().ToDictionary(
                id => id,
                id => planillas.TryGetValue(id, out var p) ? p.WorkerIds : new List<int>());

            var workerIds = trabajadoresPorPlanilla.Values.SelectMany(w => w).Distinct().ToList();

            // Un solo trabajador cumple siempre: todas sus planillas son de la misma razón social,
            // la tenga cargada o no.
            if (workerIds.Count <= 1) return null;

            var razones = await LoadRazonSocialAsync(ctx, workerIds);

            string CodigosDe(Func<List<int>, bool> incluye) => Enumerar(trabajadoresPorPlanilla
                .Where(kv => incluye(kv.Value))
                .Select(kv => codigoPorRendicion.TryGetValue(kv.Key, out var c) ? c : $"#{kv.Key}")
                .OrderBy(c => c, StringComparer.Ordinal));

            var sinRazon = workerIds
                .Where(w => !razones.TryGetValue(w, out var r) || r.Id == null)
                .ToList();

            if (sinRazon.Count > 0)
            {
                var nombres = await ctx.Worker
                    .Where(w => sinRazon.Contains(w.Id))
                    .Select(w => new { w.Id, Nombre = w.Person != null ? w.Person.FullName : null })
                    .ToDictionaryAsync(x => x.Id, x => x.Nombre);

                var quienes = sinRazon.Select(w =>
                {
                    var nombre = nombres.TryGetValue(w, out var n) && !string.IsNullOrWhiteSpace(n)
                        ? n!
                        : "Un trabajador";
                    return $"{nombre} ({CodigosDe(ws => ws.Contains(w))})";
                });

                return $"{Enumerar(quienes)} no {(sinRazon.Count == 1 ? "tiene" : "tienen")} razón social "
                     + "registrada, y sin ella no se puede agrupar con otros trabajadores en un mismo "
                     + "Consolidado del S10.";
            }

            var porRazon = workerIds.GroupBy(w => razones[w].Id!.Value).ToList();
            if (porRazon.Count <= 1) return null;

            var grupos = porRazon.Select(g =>
            {
                var nombre = razones[g.First()].Nombre ?? $"Razón social #{g.Key}";
                return $"{nombre} ({CodigosDe(ws => ws.Any(g.Contains))})";
            });

            return "Un Consolidado del S10 solo puede agrupar trabajadores de una misma razón social, "
                 + $"y la selección mezcla {Enumerar(grupos)}.";
        }

        /// <summary>"A", "A y B", "A, B y C": para nombrar planillas y personas en los mensajes.</summary>
        public static string Enumerar(IEnumerable<string> partes)
        {
            var lista = partes.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
            return lista.Count switch
            {
                0 => string.Empty,
                1 => lista[0],
                _ => string.Join(", ", lista.Take(lista.Count - 1)) + " y " + lista[^1],
            };
        }
    }
}
