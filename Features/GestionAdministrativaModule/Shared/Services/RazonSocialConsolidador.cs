using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// La razón social bajo la que queda un Consolidado del S10: la de <b>quien consolida</b>, no la
    /// de los trabajadores de las planillas que cubre.
    ///
    /// Un consolidado ya no exige que sus planillas sean de una misma razón social —el consolidador
    /// agrupa las rendiciones de su área, sean de la empresa que sean—, así que la razón social de
    /// los trabajadores dejó de describir al documento. La que lo describe es la del consolidador
    /// que lo registró en el S10. No se guarda en <c>ga_consolidado_s10</c>: se deduce de quien lo
    /// subió (<c>uploaded_by_id</c>), o del usuario que está por subirlo.
    ///
    /// Cuál es la vigente de cada ficha lo decide <see cref="RazonSocialCuposHelper.RazonSocialVigente"/>
    /// (la vinculación abierta y, si no hay, la de la ficha): no se repite acá.
    /// </summary>
    public static class RazonSocialConsolidador
    {
        /// <summary>Una razón social resuelta (<c>contributor</c>), con su RUC.</summary>
        public sealed record RazonSocial(int Id, string? Nombre, string? Ruc = null);

        /// <summary>
        /// Razón social vigente de cada usuario (<c>app_user.user_id</c>). Un usuario con varias
        /// fichas por reingreso toma la más reciente que tenga razón social. Los usuarios sin ficha o
        /// sin razón social no aparecen en el resultado. Tres roundtrips fijos: fichas, razón social
        /// vigente y nombres.
        /// </summary>
        public static async Task<Dictionary<int, RazonSocial>> LoadPorUsuarioAsync(
            AppDbContext ctx, IReadOnlyCollection<int> userIds)
        {
            var ids = userIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return new();

            var fichas = await (
                from w in ctx.Worker
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                where per.UserId != null && ids.Contains(per.UserId.Value)
                select new { WorkerId = w.Id, UserId = per.UserId!.Value }
            ).ToListAsync();
            if (fichas.Count == 0) return new();

            var workerIds = fichas.Select(f => f.WorkerId).Distinct().ToList();
            var vigentes = await RazonSocialCuposHelper
                .RazonSocialVigente(ctx, ctx.Worker.Where(w => workerIds.Contains(w.Id)))
                .Where(f => f.ContributorId != null)
                .ToListAsync();
            if (vigentes.Count == 0) return new();

            var contributorPorWorker = vigentes.ToDictionary(v => v.WorkerId, v => v.ContributorId!.Value);

            var contributorIds = contributorPorWorker.Values.Distinct().ToList();
            var contribuyentes = await ctx.Contributor
                .Where(c => contributorIds.Contains(c.ContributorId))
                .Select(c => new { c.ContributorId, c.ContributorName, c.ContributorRuc })
                .ToDictionaryAsync(c => c.ContributorId);

            return fichas
                .Where(f => contributorPorWorker.ContainsKey(f.WorkerId))
                .GroupBy(f => f.UserId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var contributorId = contributorPorWorker[g.Max(f => f.WorkerId)];
                        contribuyentes.TryGetValue(contributorId, out var c);
                        return new RazonSocial(contributorId, c?.ContributorName, c?.ContributorRuc);
                    });
        }
    }
}
