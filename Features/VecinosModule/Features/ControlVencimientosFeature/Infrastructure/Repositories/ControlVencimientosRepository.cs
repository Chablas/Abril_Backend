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

            return await ctx.VecinoLicencia
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
                })
                .ToListAsync();
        }

        public async Task<VecinoLicenciaDto> CreateLicencia(VecinoLicenciaCreateDto dto, string archivoUrl, string? originalFileName, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = new VecinoLicencia
            {
                ArchivoUrl = archivoUrl,
                OriginalFileName = originalFileName,
                FechaVencimiento = dto.FechaVencimiento,
                FechaRecordatorio = dto.FechaRecordatorio,
                DiasAntes = dto.DiasAntes,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true,
            };

            ctx.VecinoLicencia.Add(entity);
            await ctx.SaveChangesAsync();

            return new VecinoLicenciaDto
            {
                VecinoLicenciaId = entity.VecinoLicenciaId,
                ArchivoUrl = entity.ArchivoUrl,
                OriginalFileName = entity.OriginalFileName,
                FechaVencimiento = entity.FechaVencimiento,
                FechaRecordatorio = entity.FechaRecordatorio,
                DiasAntes = entity.DiasAntes,
            };
        }
    }
}
