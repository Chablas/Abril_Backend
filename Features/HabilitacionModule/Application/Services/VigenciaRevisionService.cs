using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Application.Services
{
    public class VigenciaRevisionService : IVigenciaRevisionService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public VigenciaRevisionService(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<VigenciaRevisionResultDto> RevisarVigencias()
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

            // El ítem 25 ("Lectura de EMO") queda fuera: su estado y su vigencia ya no salen de
            // ss_hab_trabajador sino del propio EMO (worker_emos), así que vencer la fila vieja
            // solo mueve data que nadie lee. Ver HabTrabajadorRepository.GetEntregablesWorkerAsync.
            // Para "Aprobado" un Vigencia null es siempre el bug (todo ítem sin vigencia real usa
            // fecha centinela 2040-12-31, ver HabilitacionDateHelper) — nunca vigente de verdad,
            // así que se vence igual que si la fecha ya hubiera pasado. Para "En plazo"/"En
            // revision" un Vigencia null SÍ puede ser legítimo (primera revisión que todavía no
            // tiene fecha asignada), así que ahí se mantiene la comparación estricta de antes.
            var trabajadores = await ctx.SsHabTrabajador
                .Where(h => h.ItemId != HabItemIds.LecturaEmo &&
                            ((h.Estado == "Aprobado" && (h.Vigencia == null || h.Vigencia < hoy)) ||
                             ((h.Estado == "En plazo" || h.Estado == "En revision") && h.Vigencia < hoy)))
                .ToListAsync();

            foreach (var h in trabajadores)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            // Excluye TODOS los ítems Centinela (documentos de una sola vez, vigencia sintética
            // 2040-12-31 — ver HabilitacionDateHelper.ItemsCentinela) y SCTR/Vida Ley (11 es el
            // ítem legado de SCTR en Empresa; 15/16 son los actuales), no solo un subconjunto
            // hardcodeado. Antes esta lista {11,12,13,15} dejaba fuera a 14/17/18/19/21/23/24/25,
            // así que un registro con vigencia vieja e incorrecta de esos ítems sí podía vencer.
            var itemsExcluidos = Abril_Backend.Features.Habilitacion.Infrastructure.Helpers.HabilitacionDateHelper.ItemsCentinela;
            var empresas = await ctx.SsHabEmpresa
                .Where(h => h.ItemId != 11 && h.ItemId != 15 && h.ItemId != 16
                         && !itemsExcluidos.Contains(h.ItemId)
                         && ((h.Estado == "Aprobado" && (h.Vigencia == null || h.Vigencia < hoy)) ||
                             (h.Estado == "En plazo" && h.Vigencia < hoy)))
                .ToListAsync();

            foreach (var h in empresas)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            var equipos = await ctx.SsHabEquipo
                .Where(h => (h.Estado == "Aprobado" || h.Estado == "En plazo") && h.Vigencia < hoy)
                .ToListAsync();

            foreach (var h in equipos)
            {
                h.Estado = "Falta";
                h.UpdatedAt = DateTime.UtcNow;
            }

            var hoyDate = DateOnly.FromDateTime(DateTime.Today);
            // Usa FechaVencimientoCalculada ?? FechaVencimiento — el mismo criterio que el
            // resto de la app (badge "Habilitado", vigencia del ítem CertAptitud, etc.). Si solo
            // se mira FechaVencimiento y esa columna quedó null (el caso normal cuando la
            // vigencia se calculó a partir del tipo de EMO), el EMO nunca entra a este filtro y
            // el trabajador queda "Habilitado" para siempre con el EMO ya vencido.
            var emos = await ctx.WorkerEmo
                .Where(e => e.Activo && e.Estado == "Vigente"
                         && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) < hoyDate)
                .ToListAsync();

            foreach (var e in emos)
            {
                e.Estado = "Vencido";
                e.UpdatedAt = DateTimeOffset.UtcNow;

                // Mantener sincronizado el checklist genérico (item Certificado de Aptitud) con
                // el estado real del EMO — si no, el checklist sigue mostrando "Aprobado" con la
                // vigencia vieja aunque el EMO real ya haya vencido (caso Sánchez Taipe).
                var hab = await ctx.SsHabTrabajador
                    .FirstOrDefaultAsync(h => h.WorkerId == e.WorkerId && h.ItemId == HabItemIds.CertAptitud);
                if (hab != null)
                {
                    hab.Estado = "Vencido";
                    hab.UpdatedAt = DateTime.UtcNow;
                }
            }

            await ctx.SaveChangesAsync();

            return new VigenciaRevisionResultDto
            {
                Trabajadores = trabajadores.Count,
                Empresas = empresas.Count,
                Equipos = equipos.Count,
                Emos = emos.Count
            };
        }
    }
}
