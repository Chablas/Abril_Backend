using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;

namespace Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Infrastructure.Interfaces
{
    /// <summary>
    /// Acceso a datos de "Correcciones S10", la bandeja del Coordinador ERP. Sin recorte por área:
    /// el responsable ERP es uno para toda la organización (§6.1), igual que Tesorería.
    /// </summary>
    public interface ICorreccionS10Repository
    {
        /// <summary>Las correcciones vivas que pasan los filtros, ordenadas por antigüedad del pedido.</summary>
        Task<List<CorreccionS10ListItemDto>> GetAll(CorreccionS10FiltersDto filters);

        /// <summary>Una corrección viva por id, o null si no existe (o ya se cerró).</summary>
        Task<CorreccionS10ListItemDto?> GetDetalle(int correccionId);

        /// <summary>Opciones de los filtros: solo lo que aparece en la bandeja.</summary>
        Task<CorreccionS10FilterDataDto> GetFilterData();

        /// <summary>
        /// Marca como atendidas las correcciones indicadas (el check de RG-22 / RF-OBS-07) y
        /// devuelve los ids que efectivamente se movieron. Las que ya estaban atendidas se ignoran
        /// en silencio: en una acción masiva la selección puede traer filas que otro ya resolvió.
        /// </summary>
        Task<List<int>> Atender(
            IEnumerable<int> correccionIds, string? comentario, bool guiaAnulada, int erpUserId);

        /// <summary>
        /// Lo que necesitan los correos de una corrección, en una sola consulta. Null si la
        /// corrección no existe.
        /// </summary>
        Task<CorreccionS10CorreoDatos?> GetCorreoDatos(int correccionId);

        /// <summary>
        /// El colaborador dueño de la planilla de una corrección, con su correo. Null si la
        /// corrección no existe o su planilla no tiene salidas.
        /// </summary>
        Task<CorreccionS10SolicitanteDto?> GetSolicitante(int correccionId);
    }
}
