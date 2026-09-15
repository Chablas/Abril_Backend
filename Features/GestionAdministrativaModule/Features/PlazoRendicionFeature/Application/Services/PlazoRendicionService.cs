using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;

namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Services
{
    /// <summary>
    /// Sección "Días reembolsables" de Solicitud de Salidas → Configuración. Lo único propio es validar
    /// el rango antes de escribir: el CHECK de la base también lo corta, pero acá el error sale con
    /// un mensaje que la pantalla puede mostrar en vez de un 500.
    /// </summary>
    public class PlazoRendicionService : IPlazoRendicionService
    {
        private readonly IPlazoRendicionRepository _repo;

        public PlazoRendicionService(IPlazoRendicionRepository repo) => _repo = repo;

        public Task<PlazoRendicionDto> Get() => _repo.Get();

        public async Task<PlazoRendicionDto> Save(PlazoRendicionSaveDto dto, int userId)
        {
            var dias = dto?.DiasHabilesPlazo ?? 0;

            if (dias < GaRendicionConfig.DiasMinimo || dias > GaRendicionConfig.DiasMaximo)
                throw new AbrilException(
                    $"El plazo tiene que estar entre {GaRendicionConfig.DiasMinimo} y {GaRendicionConfig.DiasMaximo} días hábiles.",
                    400);

            await _repo.Upsert(dias, userId);
            return await _repo.Get();
        }
    }
}
