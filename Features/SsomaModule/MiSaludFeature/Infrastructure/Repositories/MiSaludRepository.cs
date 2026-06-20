using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Repositories
{
    public class MiSaludRepository : IMiSaludRepository
    {
        private const int PageSize = 10;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public MiSaludRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<int> ResolverWorkerIdAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var person = await ctx.Person.FirstOrDefaultAsync(p => p.UserId == userId)
                ?? throw new AbrilException("No tienes un perfil de persona asociado a tu usuario.", 403);

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.PersonId == person.PersonId)
                ?? throw new AbrilException("No tienes un perfil de trabajador registrado en el sistema.", 403);

            return worker.Id;
        }

        public async Task<MiSaludResumenDto> GetResumen(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var worker = await ctx.Worker
                .Include(w => w.Person)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            // EMO activo
            var emo = await (
                from e in ctx.WorkerEmo
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                where e.WorkerId == workerId && e.Activo
                orderby e.FechaEmo descending
                select new { e, tipoNombre = t != null ? t.Nombre : null }
            ).FirstOrDefaultAsync();

            // Restricciones vigentes del EMO activo
            var restricciones = new List<string>();
            if (emo != null)
            {
                restricciones = await (
                    from r in ctx.SsEmoRestriccion
                    join rt in ctx.SsRestriccionTipo on r.RestriccionTipoId equals rt.Id into rtj
                    from rt in rtj.DefaultIfEmpty()
                    where r.EmoId == emo.e.Id && r.Vigente
                    select rt != null ? rt.Descripcion : r.DescripcionLibre
                ).Where(d => d != null).Select(d => d!).ToListAsync();
            }

            // Último descanso
            var ultimoDescanso = await ctx.SsDescansoMedico
                .Where(d => d.WorkerId == workerId && d.State)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new { d.Estado, d.FechaFin })
                .FirstOrDefaultAsync();

            DateOnly? fechaVenc = emo?.e.FechaVencimientoCalculada ?? emo?.e.FechaVencimiento;

            return new MiSaludResumenDto
            {
                WorkerId        = workerId,
                WorkerNombre    = worker.Person?.FullName,
                TieneEmo        = emo != null,
                EmoId           = emo?.e.Id,
                TipoEmo         = emo?.tipoNombre,
                Aptitud         = emo?.e.Aptitud,
                FechaEmo        = emo?.e.FechaEmo,
                FechaVencimiento = fechaVenc,
                DiasParaVencer  = fechaVenc.HasValue ? fechaVenc.Value.DayNumber - hoy.DayNumber : null,
                RestriccionesVigentes = restricciones,
                UltimoDescansoEstado  = ultimoDescanso?.Estado,
                UltimoDescansoFechaFin = ultimoDescanso?.FechaFin,
            };
        }

        public async Task<PagedResult<MiDescansoDto>> GetDescansos(int workerId, int page)
        {
            using var ctx = _factory.CreateDbContext();

            var q = ctx.SsDescansoMedico
                .Where(d => d.WorkerId == workerId && d.State)
                .OrderByDescending(d => d.FechaInicio);

            var total = await q.CountAsync();
            var pg    = page < 1 ? 1 : page;

            var items = await q
                .Skip((pg - 1) * PageSize)
                .Take(PageSize)
                .Select(d => new MiDescansoDto
                {
                    Id               = d.Id,
                    Tipo             = d.Tipo,
                    FechaInicio      = d.FechaInicio,
                    FechaFin         = d.FechaFin,
                    Dias             = d.Dias,
                    Motivo           = d.Motivo,
                    Diagnostico      = d.Diagnostico,
                    DiagnosticoCie10 = d.DiagnosticoCie10,
                    Estado           = d.Estado,
                    MotivoRechazo    = d.MotivoRechazo,
                    UrlCertificado   = d.UrlCertificado,
                    UrlDocumento     = d.UrlDocumento,
                    CreatedAt        = d.CreatedAt,
                })
                .ToListAsync();

            return new PagedResult<MiDescansoDto>
            {
                Page        = pg,
                PageSize    = PageSize,
                TotalRecords = total,
                TotalPages  = (int)Math.Ceiling(total / (double)PageSize),
                Data        = items,
            };
        }

        public async Task<int> CreateDescanso(int workerId, CrearMiDescansoDto dto, int? userId, string? urlCertificado)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = new SsDescansoMedico
            {
                WorkerId               = workerId,
                Tipo                   = dto.Tipo,
                FechaInicio            = dto.FechaInicio,
                FechaFin               = dto.FechaFin,
                Dias                   = dto.Dias ?? (dto.FechaFin.DayNumber - dto.FechaInicio.DayNumber + 1),
                Motivo                 = dto.Motivo,
                Diagnostico            = dto.Diagnostico,
                DiagnosticoCie10       = dto.DiagnosticoCie10,
                UrlCertificado         = urlCertificado,
                Estado                 = "Pendiente",
                ReportadoPorTrabajador = true,
                RegistradoPorId        = userId ?? workerId,
                CreatedAt              = DateTimeOffset.UtcNow,
                UpdatedAt              = DateTimeOffset.UtcNow,
                State                  = true,
            };

            ctx.SsDescansoMedico.Add(entity);
            await ctx.SaveChangesAsync();
            return entity.Id;
        }
    }
}
