using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Interfaces
{
    /// <summary>
    /// Sección "Días reembolsables" de Solicitud de Salidas → Configuración: los días hábiles de
    /// plazo para rendir un mes, contados sobre el mes siguiente, y hasta qué mes hacia atrás
    /// alcanza ese plazo (con ventana o en cualquier momento). Antes era la constante
    /// <c>CalendarioNoLaborable.DiasHabilesDePlazo = 7</c> y "solo el mes anterior" escrito a mano.
    /// </summary>
    public interface IPlazoRendicionService
    {
        Task<PlazoRendicionDto> Get();

        /// <summary>Guarda el plazo. Devuelve el estado ya recalculado (nuevo límite del mes anterior).</summary>
        Task<PlazoRendicionDto> Save(PlazoRendicionSaveDto dto, int userId);
    }
}
