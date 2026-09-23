using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;

namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Services
{
    /// <summary>
    /// Sección "Días reembolsables" de Solicitud de Salidas → Configuración. Lo único propio es
    /// validar antes de escribir: el CHECK y las FK de la base también cortan, pero acá el error
    /// sale con un mensaje que la pantalla puede mostrar en vez de un 500.
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

            // Un alcance que no existe (o que se dio de baja mientras la pantalla estaba abierta)
            // dejaría la fila apuntando a la nada: la FK lo cortaría con un 500 sin explicación.
            var validos = await _repo.GetAlcancesValidos();

            var plazo = dto!.AlcancePlazoId;
            if (!validos.Contains(plazo))
                throw new AbrilException("El alcance del plazo seleccionado ya no está disponible.", 400);

            var permanente = dto.AlcancePermanenteId;
            if (permanente.HasValue && !validos.Contains(permanente.Value))
                throw new AbrilException("El alcance permanente seleccionado ya no está disponible.", 400);

            await _repo.Upsert(dias, plazo, permanente, userId);
            return await _repo.Get();
        }
    }
}
