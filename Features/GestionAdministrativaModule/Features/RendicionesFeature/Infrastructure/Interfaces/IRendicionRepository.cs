using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Infrastructure.Interfaces
{
    public interface IRendicionRepository
    {
        /// <summary>
        /// Planillas que incluyen alguna salida del trabajador de ese usuario, ya filtradas. Los
        /// conteos y montos vienen acotados a sus propias salidas.
        /// </summary>
        Task<List<RendicionListItemDto>> GetByUserId(int userId, RendicionFiltersDto? filters = null);

        /// <summary>
        /// Una planilla con el desglose de las salidas propias del usuario. Null si la planilla no
        /// existe o no tiene ninguna salida suya (el guard de propiedad de esta pantalla).
        /// </summary>
        Task<RendicionDetalleDto?> GetDetalleForUser(int rendicionId, int userId);

        /// <summary>
        /// Meses con al menos una planilla propia, para el filtro de periodo, y la ficha del
        /// trabajador que se resolvió para hallarlos, con su correo de usuario. Los dos se devuelven
        /// porque el servicio los necesita para resolver los correos de "Enviar a revisión" (el jefe
        /// sale de la ficha y el acuse va al correo) y acá ya se consultaron: pedirlos aparte sería
        /// un roundtrip más por la misma fila. WorkerId null cuando el usuario no tiene ficha.
        /// </summary>
        Task<(int? WorkerId, string? Email, List<PeriodoOptionDto> Periodos)> GetPeriodos(int userId);

        /// <summary>
        /// Pasa la planilla a "En primera revisión" y estampa quién y cuándo la envió. El estado es
        /// de la planilla, así que se escribe una sola fila.
        /// </summary>
        Task MarcarEnviadaAPrimeraRevision(int rendicionId, int userId);

        /// <summary>
        /// Datos del trabajador dueño de las salidas propias de la planilla, para los correos:
        /// su ficha (para resolver el jefe), su nombre, su correo y el nombre de su área.
        /// Null si el usuario no tiene ninguna salida en esa planilla.
        /// </summary>
        Task<RendicionSolicitanteDto?> GetSolicitante(int rendicionId, int userId);

        /// <summary>Cuántos trayectos suman las salidas propias de la planilla.</summary>
        Task<int> ContarTrayectos(int rendicionId, int userId);
    }
}
