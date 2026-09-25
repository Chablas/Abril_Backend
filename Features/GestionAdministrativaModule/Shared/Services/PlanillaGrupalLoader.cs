using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Resuelve en lote la planilla grupal vigente de N planillas de rendición: la que preparó el
    /// consolidador para subirles el Consolidado del S10. Mismo molde que
    /// <see cref="ConsolidadoS10Loader"/> —lo usan la pantalla y la subida del S10, así que la regla
    /// de cuál es la vigente vive en un solo lugar—.
    ///
    /// Vigente = vínculo vivo en <c>ga_planilla_grupal_rendicion</c> hacia una planilla grupal viva.
    /// Las rendiciones que la comparten reciben el MISMO DTO, con todas las que cubre.
    /// </summary>
    public static class PlanillaGrupalLoader
    {
        /// <summary>
        /// Planilla grupal vigente de N planillas de rendición, en un solo roundtrip. Las que no
        /// tienen no aparecen.
        /// </summary>
        public static async Task<Dictionary<int, PlanillaGrupalDto>> LoadPorRendicionAsync(
            AppDbContext ctx,
            IReadOnlyCollection<int> rendicionIds)
        {
            if (rendicionIds.Count == 0) return new();

            var ids = rendicionIds.Distinct().ToList();

            // Todos los vínculos vigentes de las planillas grupales que cubren alguna de las
            // pedidas: de ahí salen tanto la planilla de cada una como la lista de lo que cubre.
            var filas = await (
                from v in ctx.GaPlanillaGrupalRendicion
                join g in ctx.GaPlanillaGrupal on v.PlanillaGrupalId equals g.Id
                join r in ctx.GaRendicion on v.RendicionId equals r.Id
                where v.State && g.State
                   && ctx.GaPlanillaGrupalRendicion.Any(x => x.State
                                                          && x.PlanillaGrupalId == g.Id
                                                          && ids.Contains(x.RendicionId))
                select new { Planilla = g, RendicionId = r.Id, r.Codigo }
            ).AsNoTracking().ToListAsync();

            if (filas.Count == 0) return new();

            var pedidas = ids.ToHashSet();
            var result  = new Dictionary<int, PlanillaGrupalDto>(ids.Count);

            foreach (var grupo in filas.GroupBy(f => f.Planilla.Id))
            {
                var dto = ToDto(grupo.First().Planilla);
                dto.Rendiciones = grupo
                    .Select(f => new ConsolidadoS10RendicionDto
                    {
                        Id     = f.RendicionId,
                        Codigo = PlanillaRendicionHelper.CodigoRendicion(f.Codigo, f.RendicionId),
                    })
                    .OrderBy(x => x.Codigo, StringComparer.Ordinal)
                    .ToList();

                foreach (var cubierta in dto.Rendiciones)
                    if (pedidas.Contains(cubierta.Id)) result[cubierta.Id] = dto;
            }
            return result;
        }

        /// <summary>El DTO de una planilla grupal SIN sus rendiciones: las llena el loader o quien la prepara.</summary>
        public static PlanillaGrupalDto ToDto(GaPlanillaGrupal g) => new()
        {
            Id          = g.Id,
            Codigo      = g.Codigo,
            PdfUrl      = g.PdfUrl,
            PdfFilename = g.PdfFilename,
            PreparadaAt = g.PreparadaAt,
            PreparadaPorId = g.PreparadaPorId,
        };
    }
}
