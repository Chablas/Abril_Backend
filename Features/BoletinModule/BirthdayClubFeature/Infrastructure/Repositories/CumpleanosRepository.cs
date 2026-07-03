using Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Application.Dtos;
using Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Infrastructure.Repositories
{
    public class CumpleanosRepository : ICumpleanosRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CumpleanosRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<CumpleaneroDto>> GetCumpleaneros(int trimestre)
        {
            // Meses del trimestre: T1→{1,2,3}, T2→{4,5,6}, T3→{7,8,9}, T4→{10,11,12}.
            var mesInicio = (trimestre - 1) * 3 + 1;
            var meses = new HashSet<int> { mesInicio, mesInicio + 1, mesInicio + 2 };

            using var ctx = _factory.CreateDbContext();

            // El universo de trabajadores internos (@abril.pe con persona) es pequeño, así que
            // se traen las fechas crudas y el filtrado por trimestre + fallback se hace en memoria
            // (evita traducir un COALESCE sobre DateOnly.Month a SQL).
            var filas = await (
                from w in ctx.Worker
                join p in ctx.Person on w.PersonId equals p.PersonId
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.ToLower().EndsWith("@abril.pe")
                      && (p.Cumpleanos != null || w.FechaNacimiento != null)
                select new
                {
                    w.Id,
                    p.PersonId,
                    p.FullName,
                    w.Ocupacion,
                    w.EmailCorporativo,
                    Cumple = p.Cumpleanos,
                    Nacimiento = w.FechaNacimiento,
                }).ToListAsync();

            var resultado = new List<CumpleaneroDto>();
            var vistos = new HashSet<int>(); // dedupe por persona (puede tener varios workers)

            foreach (var f in filas)
            {
                if (!vistos.Add(f.PersonId)) continue;

                // person.cumpleanos manda; si no hay, se usa workers.fecha_nacimiento.
                var fecha = f.Cumple ?? f.Nacimiento;
                if (fecha is null) continue;
                if (!meses.Contains(fecha.Value.Month)) continue;

                resultado.Add(new CumpleaneroDto
                {
                    WorkerId = f.Id,
                    NombreCompleto = f.FullName ?? string.Empty,
                    Ocupacion = f.Ocupacion,
                    Email = f.EmailCorporativo!,
                    Mes = fecha.Value.Month,
                    Dia = fecha.Value.Day,
                });
            }

            return resultado;
        }
    }
}
