using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Qué planillas pueden compartir un Consolidado del S10. El consolidado es UN registro en el
    /// S10 y puede cubrir varias planillas —de uno o de varios trabajadores, y de las razones
    /// sociales que sean: el consolidador agrupa las rendiciones de su área y el registro queda bajo
    /// SU razón social (ver <see cref="RazonSocialConsolidador"/>)— con una condición:
    ///
    ///   <b>El documento se reemplaza entero.</b> Si una planilla ya tiene un consolidado
    ///   compartido, las demás planillas de ese consolidado que siguen con el reembolso abierto van
    ///   con ella (su <see cref="Conjunto"/>): dejarlas en el viejo las dejaría respaldadas por un
    ///   registro del S10 que ya no es el vigente. Las que ya tienen el reembolso decidido se
    ///   quedan con el viejo, que es justo el documento que se firmó.
    ///
    /// Vive en el Shared del módulo porque la aplica la subida (<see cref="ConsolidadoS10Service"/>)
    /// y la anticipa la pantalla (Gestión de Rendiciones arma el conjunto de cada fila): con una
    /// copia en cada lado, el botón y el servidor podrían discrepar.
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
        /// con el reembolso abierto.
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
