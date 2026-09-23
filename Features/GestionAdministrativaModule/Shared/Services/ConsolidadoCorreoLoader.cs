using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Arma los datos de los correos que hablan de un Consolidado del S10 a partir de un conjunto de
    /// salidas: las agrupa por su consolidado vigente (con la precedencia de
    /// <see cref="ConsolidadoS10Loader"/>) y devuelve UNA entrada por consolidado, con el
    /// consolidador que lo adjuntó como destinatario.
    ///
    /// Vive en el Shared del módulo porque le escriben al consolidador dos pantallas: Consolidados
    /// (la jefatura aprobó u observó) y Reembolsos (Tesorería observó). Desde la primera revisión
    /// aprobada el trámite del S10 es del consolidador, así que lo que hay que subsanar le vuelve a
    /// él y no al dueño de la salida.
    ///
    /// Un número fijo de consultas, sin importar cuántas salidas o consolidados traiga.
    /// </summary>
    public static class ConsolidadoCorreoLoader
    {
        /// <param name="solicitudIds">
        /// Las salidas de las que habla el correo (las que se decidieron u observaron). Las que no
        /// tienen consolidado vigente se ignoran.
        /// </param>
        public static async Task<List<ConsolidadoCorreoDatos>> LoadAsync(
            AppDbContext ctx, IReadOnlyCollection<int> solicitudIds)
        {
            var ids = solicitudIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var salidas = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                where ids.Contains(s.Id)
                select new
                {
                    s.Id,
                    s.RendicionId,
                    s.FechaSalida,
                    s.ObservacionReembolso,
                    s.ReembolsoDecididoPorId,
                    Trabajador = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                }
            ).ToListAsync();
            if (salidas.Count == 0) return new();

            var consolidadoPorSolicitud = await ConsolidadoS10Loader.LoadAsync(
                ctx, salidas.ToDictionary(s => s.Id, s => s.RendicionId));
            if (consolidadoPorSolicitud.Count == 0) return new();

            var consolidadoIds = consolidadoPorSolicitud.Values.Select(c => c.Id).Distinct().ToList();

            // Quién adjuntó cada consolidado, con su correo: es el destinatario de los correos de vuelta.
            var consolidadores = await (
                from c in ctx.GaConsolidadoS10
                join u in ctx.User on c.UploadedById equals u.UserId into uGroup
                from u in uGroup.DefaultIfEmpty()
                join per in ctx.Person on (int?)c.UploadedById equals per.UserId into perGroup
                from per in perGroup.DefaultIfEmpty()
                where consolidadoIds.Contains(c.Id)
                select new { c.Id, Email = u != null ? u.Email : null, Nombre = per != null ? per.FullName : null }
            ).ToListAsync();
            var consolidadorDe = consolidadores
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());

            // Los códigos de las planillas de las salidas, para los consolidados antiguos por salida
            // (esos no tienen vínculos de planilla que listen lo que cubren).
            var rendicionIds = salidas.Where(s => s.RendicionId != null).Select(s => s.RendicionId!.Value).Distinct().ToList();
            var codigoDe = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Codigo })
                .ToDictionaryAsync(r => r.Id, r => PlanillaRendicionHelper.CodigoRendicion(r.Codigo, r.Id));

            var decisorIds = salidas.Where(s => s.ReembolsoDecididoPorId != null)
                .Select(s => s.ReembolsoDecididoPorId!.Value).Distinct().ToList();
            var nombreDecisor = decisorIds.Count == 0
                ? new Dictionary<int, string>()
                : (await ctx.Person
                    .Where(p => p.UserId != null && decisorIds.Contains(p.UserId.Value) && p.FullName != null)
                    .Select(p => new { UserId = p.UserId!.Value, p.FullName })
                    .ToListAsync())
                    .GroupBy(x => x.UserId)
                    .ToDictionary(g => g.Key, g => g.First().FullName!);

            return salidas
                .Where(s => consolidadoPorSolicitud.ContainsKey(s.Id))
                .GroupBy(s => consolidadoPorSolicitud[s.Id].Id)
                .Select(g =>
                {
                    var dto = consolidadoPorSolicitud[g.First().Id];
                    consolidadorDe.TryGetValue(g.Key, out var consolidador);

                    var codigos = dto.Rendiciones.Count > 0
                        ? dto.Rendiciones.Select(r => r.Codigo).ToList()
                        : g.Where(s => s.RendicionId != null && codigoDe.ContainsKey(s.RendicionId.Value))
                           .Select(s => codigoDe[s.RendicionId!.Value])
                           .Distinct()
                           .OrderBy(c => c, StringComparer.Ordinal)
                           .ToList();

                    var decisorId = g.Select(s => s.ReembolsoDecididoPorId).FirstOrDefault(x => x != null);

                    return new ConsolidadoCorreoDatos
                    {
                        ConsolidadoId     = g.Key,
                        Codigo            = dto.Codigo,
                        NumeroReembolso   = dto.NumeroReembolso,
                        MontoTotal        = dto.MontoTotal,
                        Rendiciones       = codigos,
                        Trabajadores      = g.Select(s => s.Trabajador).Distinct().OrderBy(n => n).ToList(),
                        Periodo           = PlanillaRendicionHelper.EtiquetaPeriodo(
                                                g.Min(s => s.FechaSalida), g.Max(s => s.FechaSalida)),
                        SalidasCount      = g.Count(),
                        Consolidador      = consolidador?.Nombre,
                        ConsolidadorEmail = consolidador?.Email,
                        DecididoPor       = decisorId != null ? nombreDecisor.GetValueOrDefault(decisorId.Value) : null,
                        Observacion       = g.Select(s => s.ObservacionReembolso)
                                             .FirstOrDefault(o => !string.IsNullOrWhiteSpace(o)),
                    };
                })
                .ToList();
        }
    }
}
