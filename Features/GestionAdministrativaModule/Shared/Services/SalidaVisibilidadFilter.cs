using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Recorte de visibilidad de las solicitudes de salida, en un solo lugar: lo aplican Gestión de
    /// Salidas y Gestión de Rendiciones, que muestran el mismo universo de salidas (una por fila,
    /// la otra agrupadas en planillas). Tenerlo duplicado habría dejado que las dos pantallas
    /// discreparan sobre quién ve qué.
    ///
    /// El usuario SIEMPRE ve sus propias solicitudes (worker_id → su user), sin importar rol ni
    /// área. Además ve las que le fueron enviadas para revisar (enviado_a_correo → su
    /// email_corporativo), las que él decidió (aprobador_worker_id → su user; también cubre
    /// solicitudes antiguas donde ese campo guardaba al revisor al que se envió), MÁS las de los
    /// trabajadores de las áreas (area_scope) que tiene permitido ver.
    /// </summary>
    public static class SalidaVisibilidadFilter
    {
        /// <summary>
        /// Aplica el recorte. Con <paramref name="seesAll"/> en true no restringe nada (GTH,
        /// recepción o quien el resolver haya marcado con alcance total).
        /// </summary>
        public static IQueryable<GaSolicitudSalida> Aplicar(
            IQueryable<GaSolicitudSalida> query,
            AppDbContext ctx,
            int? currentUserId,
            bool seesAll,
            IReadOnlyCollection<int>? visibleAreaScopeIds)
        {
            if (!currentUserId.HasValue || seesAll) return query;

            var uid = currentUserId.Value;
            var areaIds = visibleAreaScopeIds?.ToList() ?? new List<int>();

            return query.Where(s =>
                ctx.Worker.Any(w => w.Id == s.WorkerId &&
                    ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid))
                ||
                (s.AprobadorWorkerId != null &&
                 ctx.Worker.Any(w => w.Id == s.AprobadorWorkerId &&
                     ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid)))
                ||
                (s.EnviadoACorreo != null &&
                 ctx.Worker.Any(w => w.EmailCorporativo != null &&
                     w.EmailCorporativo.Trim().ToLower() == s.EnviadoACorreo.Trim().ToLower() &&
                     ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid)))
                ||
                ctx.Worker.Any(w => w.Id == s.WorkerId &&
                    w.PuestoCatalogo!.AreaDestinoScopeId != null
                    && areaIds.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value)));
        }
    }
}
