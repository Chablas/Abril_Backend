using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Resuelve en lote el Consolidado del S10 vigente de N planillas o de N salidas, para las
    /// tablas y detalles del módulo (evita el N+1). Es un loader estático sobre el contexto —igual
    /// que <see cref="GaAreaTreeLoader"/>— para que lo puedan usar tanto los repositorios como
    /// <see cref="ConsolidadoS10Service"/> sin duplicar la regla de cuál es el vigente.
    ///
    /// Vigente = vínculo vivo en <c>ga_consolidado_s10_rendicion</c> hacia un consolidado vivo. Un
    /// consolidado puede cubrir varias planillas, así que varias claves del resultado pueden
    /// apuntar al MISMO DTO, con la lista completa de lo que cubre en
    /// <see cref="ConsolidadoS10Dto.Rendiciones"/>.
    ///
    /// Precedencia por salida: si la salida tiene su propio consolidado, ese manda; si no, hereda
    /// el de su planilla. El consolidado por salida solo existe en registros antiguos, y por eso se
    /// sigue leyendo: darlo de baja escondería el respaldo de esas rendiciones.
    /// </summary>
    public static class ConsolidadoS10Loader
    {
        /// <param name="rendicionPorSolicitud">solicitudId → rendicionId (null si no está rendida).</param>
        public static async Task<Dictionary<int, ConsolidadoS10Dto>> LoadAsync(
            AppDbContext ctx,
            IReadOnlyDictionary<int, int?> rendicionPorSolicitud)
        {
            if (rendicionPorSolicitud.Count == 0) return new();

            var solicitudIds = rendicionPorSolicitud.Keys.ToList();
            var rendicionIds = rendicionPorSolicitud.Values
                .Where(r => r != null)
                .Select(r => r!.Value)
                .Distinct()
                .ToList();

            var propios = await ctx.GaConsolidadoS10
                .Where(c => c.State && c.SolicitudId != null && solicitudIds.Contains(c.SolicitudId.Value))
                .ToListAsync();
            var porSolicitud = propios.ToDictionary(c => c.SolicitudId!.Value, ToDto);

            var porRendicion = await LoadPorRendicionAsync(ctx, rendicionIds);

            var result = new Dictionary<int, ConsolidadoS10Dto>(solicitudIds.Count);
            foreach (var (solicitudId, rendicionId) in rendicionPorSolicitud)
            {
                if (porSolicitud.TryGetValue(solicitudId, out var propio))
                {
                    result[solicitudId] = propio;
                    continue;
                }
                if (rendicionId != null && porRendicion.TryGetValue(rendicionId.Value, out var deRendicion))
                    result[solicitudId] = deRendicion;
            }
            return result;
        }

        /// <summary>
        /// Consolidado vigente de N planillas. Las que comparten consolidado reciben el MISMO DTO
        /// (es el mismo documento), con todas las planillas que cubre. Un solo roundtrip. Las
        /// planillas sin consolidado no aparecen.
        /// </summary>
        public static async Task<Dictionary<int, ConsolidadoS10Dto>> LoadPorRendicionAsync(
            AppDbContext ctx,
            IReadOnlyCollection<int> rendicionIds)
        {
            if (rendicionIds.Count == 0) return new();

            var ids = rendicionIds.Distinct().ToList();

            // Todos los vínculos vigentes de los consolidados que cubren alguna de las planillas
            // pedidas: de ahí salen tanto el consolidado de cada una como la lista de lo que cubre.
            var filas = await (
                from v in ctx.GaConsolidadoS10Rendicion
                join c in ctx.GaConsolidadoS10 on v.ConsolidadoS10Id equals c.Id
                join r in ctx.GaRendicion on v.RendicionId equals r.Id
                where v.State && c.State
                   && ctx.GaConsolidadoS10Rendicion.Any(x => x.State
                                                         && x.ConsolidadoS10Id == c.Id
                                                         && ids.Contains(x.RendicionId))
                select new { Consolidado = c, RendicionId = r.Id, r.Codigo }
            ).ToListAsync();

            if (filas.Count == 0) return new();

            var pedidas = ids.ToHashSet();
            var result  = new Dictionary<int, ConsolidadoS10Dto>(ids.Count);

            foreach (var grupo in filas.GroupBy(f => f.Consolidado.Id))
            {
                var dto = ToDto(grupo.First().Consolidado);
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

        /// <summary>
        /// El DTO de un consolidado SIN la lista de planillas que cubre
        /// (<see cref="ConsolidadoS10Dto.Rendiciones"/> vacía): esa la llenan los loaders de arriba.
        /// Suelto lo usan quienes miran un consolidado puntual por su id —la bandeja del ERP, que
        /// muestra el observado aunque ya se haya reemplazado— y la subida, que ya sabe qué cubre.
        /// </summary>
        public static ConsolidadoS10Dto ToDto(GaConsolidadoS10 c) => new()
        {
            Id          = c.Id,
            // Sin salida suelta, es de planillas: el consolidado por salida es solo de registros viejos.
            Ambito      = c.SolicitudId != null
                            ? ConsolidadoS10Ambito.Solicitud.ToString()
                            : ConsolidadoS10Ambito.Rendicion.ToString(),
            PdfUrl      = c.PdfUrl,
            PdfFilename = c.PdfFilename,
            MontoTotal  = c.MontoTotal,
            NumeroGuia  = c.NumeroGuia,
            PdfFirmadoUrl      = c.PdfFirmadoUrl,
            PdfFirmadoFilename = c.PdfFirmadoFilename,
            FirmadoAt          = c.FirmadoAt,
            UploadedAt  = c.UploadedAt,
        };
    }
}
