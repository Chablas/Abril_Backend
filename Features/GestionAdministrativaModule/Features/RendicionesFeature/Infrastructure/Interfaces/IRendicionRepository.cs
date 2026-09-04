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

        /// <summary>Meses con al menos una planilla propia, para el filtro de periodo.</summary>
        Task<List<PeriodoOptionDto>> GetPeriodos(int userId);

        /// <summary>
        /// Marca que se le avisó al revisor por esta planilla. El sello queda en TODAS las salidas
        /// propias que cubre (la columna vive en la salida), porque el aviso es uno solo.
        /// </summary>
        Task MarcarRevisorNotificado(int rendicionId, int userId);
    }
}
