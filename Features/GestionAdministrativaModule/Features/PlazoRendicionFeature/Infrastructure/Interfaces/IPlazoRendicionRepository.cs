using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Infrastructure.Interfaces
{
    public interface IPlazoRendicionRepository
    {
        /// <summary>
        /// El plazo configurado con su efecto ya resuelto (límite del mes anterior). Va en un solo
        /// método porque el número solo se entiende junto a la fecha que produce, y las dos salen
        /// del mismo contexto: el calendario de feriados se carga una vez.
        /// </summary>
        Task<PlazoRendicionDto> Get();

        /// <summary>Escribe el plazo en la fila única de <c>ga_rendicion_config</c> (la crea si no existe).</summary>
        Task Upsert(int diasHabilesPlazo, int userId);
    }
}
