using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Infrastructure.Repositories
{
    /// <summary>
    /// Fila única de <c>ga_rendicion_config</c>: los días hábiles de plazo para rendir. Lo mismo que
    /// leen las dos pantallas de salidas a través de <see cref="CalendarioNoLaborable"/>, así que
    /// acá no se duplica el cálculo del límite — se reusa el calendario.
    /// </summary>
    public class PlazoRendicionRepository : IPlazoRendicionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PlazoRendicionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<PlazoRendicionDto> Get()
        {
            using var ctx = _factory.CreateDbContext();

            // El calendario ya trae adentro el plazo configurado: una sola carga da el número y la
            // fecha límite que produce, sin preguntar dos veces por lo mismo.
            var calendario = await CalendarioNoLaborable.CargarAsync(ctx);
            var (desde, _) = MesAnteriorPeru.Rango();

            return new PlazoRendicionDto
            {
                DiasHabilesPlazo   = calendario.DiasHabilesDePlazo,
                DiasMinimo         = GaRendicionConfig.DiasMinimo,
                DiasMaximo         = GaRendicionConfig.DiasMaximo,
                LimiteMesAnterior  = calendario.LimiteDeRendicion(desde.Year, desde.Month),
                MesAnteriorAnio    = desde.Year,
                MesAnteriorMes     = desde.Month,
                MesAnteriorVencido = calendario.PlazoVencido(desde.Year, desde.Month),
            };
        }

        public async Task Upsert(int diasHabilesPlazo, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var config = await ctx.GaRendicionConfig
                .Where(c => c.State)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                ctx.GaRendicionConfig.Add(new GaRendicionConfig
                {
                    DiasHabilesPlazo = diasHabilesPlazo,
                    State            = true,
                    CreatedAt        = DateTimeOffset.UtcNow,
                    CreatedUserId    = userId,
                });
            }
            else
            {
                if (config.DiasHabilesPlazo == diasHabilesPlazo) return;

                config.DiasHabilesPlazo = diasHabilesPlazo;
                config.UpdatedAt        = DateTimeOffset.UtcNow;
                config.UpdatedUserId    = userId;
            }

            await ctx.SaveChangesAsync();
        }
    }
}
