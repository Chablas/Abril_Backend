using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Repositories
{
    public class ControlVencimientosRepository : IControlVencimientosRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ControlVencimientosRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<VecinoLicenciaDto>> GetLicencias()
        {
            using var ctx = _factory.CreateDbContext();

            var licencias = await ctx.VecinoLicencia
                .Where(l => l.State)
                .OrderBy(l => l.FechaVencimiento)
                .ThenByDescending(l => l.VecinoLicenciaId)
                .Select(l => new VecinoLicenciaDto
                {
                    VecinoLicenciaId = l.VecinoLicenciaId,
                    ArchivoUrl = l.ArchivoUrl,
                    OriginalFileName = l.OriginalFileName,
                    FechaVencimiento = l.FechaVencimiento,
                    FechaRecordatorio = l.FechaRecordatorio,
                    DiasAntes = l.DiasAntes,
                    RecordatorioEnviadoDateTime = l.RecordatorioEnviadoDateTime,
                })
                .ToListAsync();

            await CargarEmails(ctx, licencias);
            return licencias;
        }

        public async Task<VecinoLicenciaDto> CreateLicencia(VecinoLicenciaCreateDto dto, string archivoUrl, string? originalFileName, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var now = DateTime.UtcNow;
            var entity = new VecinoLicencia
            {
                ArchivoUrl = archivoUrl,
                OriginalFileName = originalFileName,
                FechaVencimiento = dto.FechaVencimiento,
                FechaRecordatorio = dto.FechaRecordatorio,
                DiasAntes = dto.DiasAntes,
                CreatedDateTime = now,
                CreatedUserId = userId,
                Active = true,
                State = true,
            };

            ctx.VecinoLicencia.Add(entity);
            await ctx.SaveChangesAsync();

            foreach (var email in dto.Emails)
            {
                ctx.VecinoLicenciaEmail.Add(new VecinoLicenciaEmail
                {
                    VecinoLicenciaId = entity.VecinoLicenciaId,
                    Email = email,
                    CreatedDateTime = now,
                    CreatedUserId = userId,
                    Active = true,
                    State = true,
                });
            }
            await ctx.SaveChangesAsync();

            return new VecinoLicenciaDto
            {
                VecinoLicenciaId = entity.VecinoLicenciaId,
                ArchivoUrl = entity.ArchivoUrl,
                OriginalFileName = entity.OriginalFileName,
                FechaVencimiento = entity.FechaVencimiento,
                FechaRecordatorio = entity.FechaRecordatorio,
                DiasAntes = entity.DiasAntes,
                Emails = dto.Emails,
                RecordatorioEnviadoDateTime = null,
            };
        }

        public async Task<List<VecinoLicenciaDto>> GetPendientesRecordatorio(DateOnly hoy)
        {
            using var ctx = _factory.CreateDbContext();

            var licencias = await ctx.VecinoLicencia
                .Where(l => l.State && l.Active
                    && l.RecordatorioEnviadoDateTime == null
                    && l.FechaRecordatorio <= hoy)
                .OrderBy(l => l.FechaVencimiento)
                .Select(l => new VecinoLicenciaDto
                {
                    VecinoLicenciaId = l.VecinoLicenciaId,
                    ArchivoUrl = l.ArchivoUrl,
                    OriginalFileName = l.OriginalFileName,
                    FechaVencimiento = l.FechaVencimiento,
                    FechaRecordatorio = l.FechaRecordatorio,
                    DiasAntes = l.DiasAntes,
                })
                .ToListAsync();

            await CargarEmails(ctx, licencias);
            return licencias;
        }

        public async Task MarcarRecordatorioEnviado(int vecinoLicenciaId)
        {
            using var ctx = _factory.CreateDbContext();
            var licencia = await ctx.VecinoLicencia.FirstOrDefaultAsync(l => l.VecinoLicenciaId == vecinoLicenciaId);
            if (licencia is null) return;
            licencia.RecordatorioEnviadoDateTime = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        /// <summary>Anexa a cada DTO sus correos destinatarios (una sola consulta para todas).</summary>
        private static async Task CargarEmails(AppDbContext ctx, List<VecinoLicenciaDto> licencias)
        {
            if (licencias.Count == 0) return;
            var ids = licencias.Select(l => l.VecinoLicenciaId).ToList();

            var emails = await ctx.VecinoLicenciaEmail
                .Where(e => e.State && e.Active && ids.Contains(e.VecinoLicenciaId))
                .OrderBy(e => e.VecinoLicenciaEmailId)
                .Select(e => new { e.VecinoLicenciaId, e.Email })
                .ToListAsync();

            var porLicencia = emails
                .GroupBy(e => e.VecinoLicenciaId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Email).ToList());

            foreach (var l in licencias)
                l.Emails = porLicencia.TryGetValue(l.VecinoLicenciaId, out var list) ? list : new List<string>();
        }
    }
}
