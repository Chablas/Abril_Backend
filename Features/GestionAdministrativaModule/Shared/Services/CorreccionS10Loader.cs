using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Resuelve en lote la solicitud de corrección del S10 VIGENTE de N planillas, para las tablas
    /// y detalles de las pantallas que la muestran (evita el N+1). Es un loader estático sobre el
    /// contexto, igual que <see cref="ConsolidadoS10Loader"/>, para que lo usen los repositorios de
    /// Mis Rendiciones y de la bandeja del ERP sin duplicar la regla de "cuál es la vigente".
    ///
    /// Vigente = <c>state = true</c>. Una planilla tiene como máximo una: el índice único parcial
    /// de <c>ga_correccion_s10</c> lo garantiza, y al recargar el Consolidado del S10 la corrección
    /// se da de baja, así que si la jefatura vuelve a observar se puede pedir otra.
    /// </summary>
    public static class CorreccionS10Loader
    {
        /// <summary>
        /// rendicionId → corrección vigente. Las planillas sin corrección viva no aparecen en el
        /// diccionario (que es lo normal: la mayoría nunca pasa por el ERP).
        /// </summary>
        public static async Task<Dictionary<int, CorreccionS10Dto>> LoadVigentesAsync(
            AppDbContext ctx, IReadOnlyCollection<int> rendicionIds)
        {
            var ids = rendicionIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var filas = await ctx.GaCorreccionS10
                .Where(c => c.State && ids.Contains(c.RendicionId))
                .ToListAsync();

            if (filas.Count == 0) return new();

            // Los nombres de quien pidió y de quien atendió, de una vez: son pocas filas y
            // resolverlas una por una sería un N+1 sobre app_user.
            var nombres = await NombresAsync(ctx, filas);

            return filas.ToDictionary(c => c.RendicionId, c => ToDto(c, nombres));
        }

        /// <summary>Nombres de los usuarios que pidieron o atendieron las correcciones dadas.</summary>
        public static async Task<Dictionary<int, string>> NombresAsync(
            AppDbContext ctx, IReadOnlyCollection<GaCorreccionS10> correcciones)
        {
            var userIds = correcciones
                .Select(c => c.SolicitadaPorId)
                .Concat(correcciones.Where(c => c.AtendidaPorId != null).Select(c => c.AtendidaPorId!.Value))
                .Distinct()
                .ToList();

            if (userIds.Count == 0) return new();

            // El nombre cuelga de person.user_id (app_user no lo tiene): mismo camino que usa la
            // bandeja de Tesorería para los rastros de firma y pago.
            var filas = await ctx.Person
                .Where(p => p.UserId != null && userIds.Contains(p.UserId!.Value) && p.FullName != null)
                .Select(p => new { UserId = p.UserId!.Value, p.FullName })
                .ToListAsync();

            return filas
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.First().FullName!);
        }

        public static CorreccionS10Dto ToDto(GaCorreccionS10 c, IReadOnlyDictionary<int, string> nombres)
        {
            string? Nombre(int? userId) =>
                userId != null && nombres.TryGetValue(userId.Value, out var n) && !string.IsNullOrWhiteSpace(n)
                    ? n
                    : null;

            return new CorreccionS10Dto
            {
                Id                 = c.Id,
                RendicionId        = c.RendicionId,
                Estado             = EstadosSalida.CorreccionS10.Nombre(c.EstadoId),
                Motivo             = c.Motivo,
                MotivoJefatura     = c.MotivoJefatura,
                NumeroGuia         = c.NumeroGuia,
                SolicitadaPor      = Nombre(c.SolicitadaPorId) ?? string.Empty,
                SolicitadaAt       = c.SolicitadaAt,
                AtendidaPor        = Nombre(c.AtendidaPorId),
                AtendidaAt         = c.AtendidaAt,
                ComentarioAtencion = c.ComentarioAtencion,
                GuiaAnulada        = c.GuiaAnulada,
                EsperandoErp       = c.EstadoId == EstadosSalida.CorreccionS10.Solicitada,
            };
        }
    }
}
