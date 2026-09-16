using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Infrastructure.Interfaces
{
    public interface IPlazoRendicionRepository
    {
        /// <summary>
        /// El plazo configurado con su efecto ya resuelto (límite del mes anterior) y las opciones
        /// de alcance. Va en un solo método porque el número solo se entiende junto a la fecha que
        /// produce, y todo sale del mismo contexto: el calendario de feriados se carga una vez.
        /// </summary>
        Task<PlazoRendicionDto> Get();

        /// <summary>Ids vivos de <c>ga_rendicion_alcance</c>, para validar lo que llega del cliente.</summary>
        Task<HashSet<int>> GetAlcancesValidos();

        /// <summary>
        /// Escribe el plazo y los dos alcances en la fila única de <c>ga_rendicion_config</c> (la
        /// crea si no existe). <paramref name="alcancePermanenteId"/> en null = sin alcance
        /// permanente.
        /// </summary>
        Task Upsert(int diasHabilesPlazo, int alcancePlazoId, int? alcancePermanenteId, int userId);
    }
}
