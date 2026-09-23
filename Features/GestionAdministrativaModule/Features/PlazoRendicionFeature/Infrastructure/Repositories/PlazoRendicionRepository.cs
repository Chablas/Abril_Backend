using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Infrastructure.Repositories
{
    /// <summary>
    /// Fila única de <c>ga_rendicion_config</c>: los días hábiles de plazo para rendir y hasta qué
    /// mes hacia atrás alcanza. Lo mismo que leen las dos pantallas de salidas a través de
    /// <see cref="CalendarioNoLaborable"/>, así que acá no se duplica el cálculo del límite — se
    /// reusa el calendario.
    /// </summary>
    public class PlazoRendicionRepository : IPlazoRendicionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PlazoRendicionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<PlazoRendicionDto> Get()
        {
            using var ctx = _factory.CreateDbContext();

            // El calendario ya trae adentro el plazo y los dos alcances configurados: una sola
            // carga da los números y el mes desde el que hoy se puede rendir, sin preguntar dos
            // veces por lo mismo. Lo único que falta son las opciones del desplegable.
            var calendario = await CalendarioNoLaborable.CargarAsync(ctx);
            var desde      = calendario.MesMasAntiguoRendible();

            var alcances = await ctx.GaRendicionAlcance
                .Where(a => a.State && a.Active)
                .OrderBy(a => a.Orden).ThenBy(a => a.MesesAtras)
                .Select(a => new AlcanceRendicionOpcionDto
                {
                    Id         = a.GaRendicionAlcanceId,
                    Nombre     = a.Nombre,
                    MesesAtras = a.MesesAtras,
                })
                .ToListAsync();

            return new PlazoRendicionDto
            {
                DiasHabilesPlazo    = calendario.DiasHabilesDePlazo,
                DiasMinimo          = GaRendicionConfig.DiasMinimo,
                DiasMaximo          = GaRendicionConfig.DiasMaximo,
                RendibleDesdeAnio   = desde.Anio,
                RendibleDesdeMes    = desde.Mes,
                AlcancePlazoId      = calendario.AlcancePlazoId,
                AlcancePermanenteId = calendario.AlcancePermanenteId,
                Alcances            = alcances,
            };
        }

        /// <summary>Ids vivos del catálogo, para que el servicio rechace un alcance inexistente.</summary>
        public async Task<HashSet<int>> GetAlcancesValidos()
        {
            using var ctx = _factory.CreateDbContext();

            return (await ctx.GaRendicionAlcance
                .Where(a => a.State && a.Active)
                .Select(a => a.GaRendicionAlcanceId)
                .ToListAsync()).ToHashSet();
        }

        public async Task Upsert(int diasHabilesPlazo, int alcancePlazoId, int? alcancePermanenteId, int userId)
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
                    DiasHabilesPlazo    = diasHabilesPlazo,
                    AlcancePlazoId      = alcancePlazoId,
                    AlcancePermanenteId = alcancePermanenteId,
                    State               = true,
                    CreatedAt           = DateTimeOffset.UtcNow,
                    CreatedUserId       = userId,
                });
            }
            else
            {
                if (config.DiasHabilesPlazo    == diasHabilesPlazo
                 && config.AlcancePlazoId      == alcancePlazoId
                 && config.AlcancePermanenteId == alcancePermanenteId) return;

                config.DiasHabilesPlazo    = diasHabilesPlazo;
                config.AlcancePlazoId      = alcancePlazoId;
                config.AlcancePermanenteId = alcancePermanenteId;
                config.UpdatedAt           = DateTimeOffset.UtcNow;
                config.UpdatedUserId       = userId;
            }

            await ctx.SaveChangesAsync();
        }
    }
}
