using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Alerta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class EmoAutoProgramacionService : IEmoAutoProgramacionService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IProgramacionEmoRepository _progRepo;
        private readonly ILogger<EmoAutoProgramacionService> _logger;

        public EmoAutoProgramacionService(
            IDbContextFactory<AppDbContext> factory,
            IProgramacionEmoRepository progRepo,
            ILogger<EmoAutoProgramacionService> logger)
        {
            _factory = factory;
            _progRepo = progRepo;
            _logger = logger;
        }

        public async Task<EmoAutoProgramacionResultDto> ProcesarAutoProgramacion()
        {
            var result = new EmoAutoProgramacionResultDto();
            using var ctx = _factory.CreateDbContext();

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5).Date);
            var ventanaFin = hoy.AddDays(30);

            // Candidatos: WorkerEmo activo con tipo que requiere renovación,
            // vencimiento en los próximos 30 días, y vinculación activa.
            var candidatosRaw = await (
                from e in ctx.WorkerEmo
                join w in ctx.Worker on e.WorkerId equals w.Id
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id
                join v in ctx.WorkerVinculacion on w.Id equals v.WorkerId
                join contrib in ctx.Contributor on v.EmpresaId equals contrib.ContributorId
                where e.Activo
                    && t.RequiereNuevo
                    && t.VigenciaMeses != null
                    && v.FechaFin == null
                    && contrib.EsAbril
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) != null
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) >= hoy.AddDays(1)
                    && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) <= ventanaFin
                select new
                {
                    Emo = e,
                    Worker = w,
                    TipoEmo = t,
                    Vinculacion = v,
                    WorkerNombre = w.Person != null ? w.Person.FullName : null
                }
            ).AsNoTracking().ToListAsync();

            if (candidatosRaw.Count == 0) return result;

            // Por cada (WorkerId, TipoEmoId) quedarse con la vinculación más reciente
            // (puede haber múltiples registros si hay más de una vinculación sin fecha fin).
            var candidatos = candidatosRaw
                .GroupBy(x => (x.Emo.WorkerId, x.Emo.TipoEmoId))
                .Select(g => g.OrderByDescending(x => x.Vinculacion.CreatedAt).First())
                .ToList();

            // Verificar cuáles ya tienen programación activa
            var workerTipoPares = candidatos
                .Select(x => new { x.Emo.WorkerId, TipoEmoId = x.Emo.TipoEmoId!.Value })
                .ToList();

            var workerIds = workerTipoPares.Select(p => p.WorkerId).Distinct().ToList();

            var programacionesExistentes = await ctx.SsProgramacionEmo
                .AsNoTracking()
                .Where(p =>
                    workerIds.Contains(p.WorkerId)
                    && p.FechaProgramada >= hoy
                    && p.Estado != "Cancelado"
                    && p.Estado != "Rechazado por Clínica")
                .Select(p => new { p.WorkerId, p.TipoEmoId })
                .ToListAsync();

            var existentesSet = new HashSet<(int, int)>(
                programacionesExistentes.Select(p => (p.WorkerId, p.TipoEmoId)));

            foreach (var c in candidatos)
            {
                try
                {
                    var tipoEmoId = c.Emo.TipoEmoId!.Value;
                    var clave = (c.Emo.WorkerId, tipoEmoId);

                    if (existentesSet.Contains(clave))
                    {
                        result.YaTenianProgramacion++;
                        result.Detalle.Add(
                            $"Worker {c.Worker.Id} ({c.WorkerNombre}) / TipoEMO {tipoEmoId} — ya tiene programación activa. Omitido.");
                        continue;
                    }

                    var fv = (c.Emo.FechaVencimientoCalculada ?? c.Emo.FechaVencimiento)!.Value;
                    var esOficina = EsCalendarioOficina(c.Worker);
                    var fechaDesdeVencimiento = RestarDiasHabiles(fv, 4, esOficina);
                    var fechaMinima = SiguienteDiaHabil(SiguienteDiaHabil(hoy, esOficina), esOficina);
                    var fechaProg = fechaDesdeVencimiento > fechaMinima ? fechaDesdeVencimiento : fechaMinima;

                    await _progRepo.Create(new ProgramacionCreateDto
                    {
                        WorkerId        = c.Emo.WorkerId,
                        EmpresaId       = c.Vinculacion.EmpresaId,
                        TipoEmoId       = tipoEmoId,
                        FechaProgramada = fechaProg,
                        Origen          = "Automatico",
                        Motivo          = "Programación automática por vencimiento de EMO",
                    }, userId: null);

                    result.Procesados++;
                    result.Detalle.Add(
                        $"Worker {c.Worker.Id} ({c.WorkerNombre}) / TipoEMO {tipoEmoId} — programado para {fechaProg:yyyy-MM-dd}.");
                }
                catch (Exception ex)
                {
                    result.Errores++;
                    _logger.LogError(ex, "Error procesando auto-programación para Worker {WorkerId}", c.Worker.Id);
                    result.Detalle.Add(
                        $"Worker {c.Worker.Id} ({c.WorkerNombre}) — error: {ex.Message}");
                }
            }

            return result;
        }

        // Retrocede `dias` días hábiles desde `fecha`.
        // excluirSabado=true → lunes-viernes (Casa+Staff/Oficina Central).
        // excluirSabado=false → lunes-sábado (Contratista/Obra).
        private static DateOnly RestarDiasHabiles(DateOnly fecha, int dias, bool excluirSabado)
        {
            var resultado = fecha;
            int conteo = 0;
            while (conteo < dias)
            {
                resultado = resultado.AddDays(-1);
                var dow = resultado.DayOfWeek;
                if (dow == DayOfWeek.Sunday) continue;
                if (excluirSabado && dow == DayOfWeek.Saturday) continue;
                conteo++;
            }
            return resultado;
        }

        // Avanza al primer día hábil posterior a `fecha`.
        private static DateOnly SiguienteDiaHabil(DateOnly fecha, bool excluirSabado)
        {
            var resultado = fecha.AddDays(1);
            while (true)
            {
                var dow = resultado.DayOfWeek;
                if (dow == DayOfWeek.Sunday) { resultado = resultado.AddDays(1); continue; }
                if (excluirSabado && dow == DayOfWeek.Saturday) { resultado = resultado.AddDays(1); continue; }
                return resultado;
            }
        }

        // Casa + Staff u Oficina Central → lunes-viernes.
        // Contratista u Obra → lunes-sábado.
        private static bool EsCalendarioOficina(Worker worker)
        {
            return string.Equals(worker.ContrataCasa, "Casa", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase));
        }
    }
}
