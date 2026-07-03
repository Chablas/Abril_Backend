using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Repositories
{
    public class ManagerSignatureRepository : IManagerSignatureRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ManagerSignatureRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ManagerSignatureDto?> GetSingleton()
        {
            using var ctx = _factory.CreateDbContext();

            var s = await ctx.ManagerSignature
                .Where(x => x.State)
                .OrderBy(x => x.ManagerSignatureId)
                .Select(x => new
                {
                    x.ManagerSignatureId,
                    x.ImageBytes,
                    x.Mime,
                    x.CreatedDateTime,
                    x.UpdatedDateTime
                })
                .FirstOrDefaultAsync();

            if (s == null) return null;

            return new ManagerSignatureDto
            {
                ManagerSignatureId = s.ManagerSignatureId,
                ImageDataUrl = $"data:{s.Mime};base64,{Convert.ToBase64String(s.ImageBytes)}",
                CreatedDateTime = s.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                UpdatedDateTime = s.UpdatedDateTime.HasValue
                    ? s.UpdatedDateTime.Value.ToOffset(TimeSpan.FromHours(-5)).DateTime
                    : (DateTime?)null
            };
        }

        public async Task Upsert(byte[] imageBytes, string mime, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var record = await ctx.ManagerSignature
                .Where(x => x.State)
                .OrderBy(x => x.ManagerSignatureId)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                ctx.ManagerSignature.Add(new ManagerSignature
                {
                    ImageBytes = imageBytes,
                    Mime = mime,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = userId
                });
            }
            else
            {
                record.ImageBytes = imageBytes;
                record.Mime = mime;
                record.Active = true;
                record.UpdatedDateTime = DateTimeOffset.UtcNow;
                record.UpdatedUserId = userId;
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<(byte[] Bytes, string Mime)?> GetActiveBytes()
        {
            using var ctx = _factory.CreateDbContext();

            var s = await ctx.ManagerSignature
                .Where(x => x.State && x.Active)
                .OrderBy(x => x.ManagerSignatureId)
                .Select(x => new { x.ImageBytes, x.Mime })
                .FirstOrDefaultAsync();

            return s == null ? null : (s.ImageBytes, s.Mime);
        }
    }
}
