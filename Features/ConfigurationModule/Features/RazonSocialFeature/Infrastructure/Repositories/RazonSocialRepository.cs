using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Infrastructure.Repositories
{
    /// <summary>
    /// Razones sociales del sistema (tabla <c>contributor</c>) desde la mirada de Configuración:
    /// las del grupo y las de terceros, con su banco. SSOMA lee la misma tabla por su propio
    /// catálogo, que solo necesita el nombre y el RUC.
    /// </summary>
    public class RazonSocialRepository : IRazonSocialRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public RazonSocialRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<RazonSocialBandejaDto> GetBandeja()
        {
            using var ctx = _factory.CreateDbContext();

            // Activas e inactivas: el filtro de estado es de pantalla y se resuelve en memoria, así
            // que cambiarlo no vuelve a pedir nada.
            var razonesSociales = await Query(ctx).ToListAsync();

            var bancos = await ctx.Banco
                .Where(b => b.State && b.Active)
                .OrderBy(b => b.Orden).ThenBy(b => b.Nombre)
                .Select(b => new BancoOpcionDto { Id = b.BancoId, Nombre = b.Nombre })
                .ToListAsync();

            return new RazonSocialBandejaDto { RazonesSociales = razonesSociales, Bancos = bancos };
        }

        public async Task<RazonSocialDto> Create(RazonSocialCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var ruc = dto.Ruc.Trim();
            if (await ctx.Contributor.AnyAsync(c => c.ContributorRuc == ruc && c.State))
                throw new AbrilException("Ya existe una razón social registrada con ese RUC.", 409);

            var entity = new Contributor
            {
                ContributorRuc                         = ruc,
                ContributorName                        = dto.Nombre.Trim(),
                ContributorAddress                     = dto.Direccion.Trim(),
                ContributorEconomicActivityDescription = dto.TipoActividad.Trim(),
                ContributorDistrict                    = dto.Distrito.Trim(),
                ContributorProvince                    = dto.Provincia.Trim(),
                ContributorDepartment                  = dto.Departamento.Trim(),
                LegalEntityRegistryNumber = string.IsNullOrWhiteSpace(dto.PartidaRegistral)
                                            ? null : dto.PartidaRegistral.Trim(),
                CreatedDateTime = DateTimeOffset.UtcNow,
                CreatedUserId   = userId,
                Active  = true,
                State   = true,
                EsAbril = dto.EsAbril,
                // El banco solo se guarda si es del grupo: la base lo exige con un CHECK, así que
                // mandarlo igual sería un 500 en vez de un dato ignorado.
                BancoId = dto.EsAbril ? dto.BancoId : null,
            };

            ctx.Contributor.Add(entity);
            await ctx.SaveChangesAsync();

            return await Leer(ctx, entity.ContributorId);
        }

        public async Task<RazonSocialDto> Update(int contributorId, RazonSocialUpdateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.Contributor
                .FirstOrDefaultAsync(c => c.ContributorId == contributorId && c.State)
                ?? throw new AbrilException("La razón social indicada no existe o fue eliminada.", 404);

            // El RUC, el nombre y la partida registral vienen de SUNAT y no se editan.
            entity.ContributorAddress = (dto.Direccion ?? "").Trim();
            entity.ContributorEconomicActivityDescription =
                string.IsNullOrWhiteSpace(dto.TipoActividad) ? null : dto.TipoActividad.Trim();
            entity.Active  = dto.Activo;
            entity.EsAbril = dto.EsAbril;
            // Dejar de ser del grupo se lleva el banco: conservarlo violaría el CHECK de la base.
            entity.BancoId = dto.EsAbril ? dto.BancoId : null;
            entity.UpdatedDateTime = DateTimeOffset.UtcNow;
            entity.UpdatedUserId   = userId;

            await ctx.SaveChangesAsync();

            return await Leer(ctx, entity.ContributorId);
        }

        /// <summary>Proyección única de la fila: la comparten la bandeja, el alta y la edición.</summary>
        private static IQueryable<RazonSocialDto> Query(AppDbContext ctx) =>
            ctx.Contributor
                .Where(c => c.State)
                .OrderBy(c => c.ContributorName)
                .Select(c => new RazonSocialDto
                {
                    Id               = c.ContributorId,
                    Nombre           = c.ContributorName,
                    Ruc              = c.ContributorRuc,
                    Direccion        = c.ContributorAddress,
                    PartidaRegistral = c.LegalEntityRegistryNumber,
                    TipoActividad    = c.ContributorEconomicActivityDescription,
                    Activo           = c.Active,
                    EsAbril          = c.EsAbril,
                    BancoId          = c.BancoId,
                    BancoNombre      = ctx.Banco
                                          .Where(b => b.BancoId == c.BancoId)
                                          .Select(b => b.Nombre)
                                          .FirstOrDefault(),
                });

        /// <summary>Relee la fila ya escrita para devolverla con el nombre de su banco resuelto.</summary>
        private static async Task<RazonSocialDto> Leer(AppDbContext ctx, int contributorId) =>
            await Query(ctx).FirstOrDefaultAsync(r => r.Id == contributorId)
            ?? throw new AbrilException("No se pudo releer la razón social guardada.", 500);
    }
}
