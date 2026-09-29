using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;

namespace Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Infrastructure.Interfaces
{
    /// <summary>
    /// Acceso a datos de "Correcciones S10", la bandeja del Coordinador ERP. Sin recorte por área:
    /// el responsable ERP es uno para toda la organización (§6.1), igual que Tesorería.
    /// </summary>
    public interface ICorreccionS10Repository
    {
        /// <summary>
        /// Las correcciones vivas que pasan los filtros: primero las por atender y, dentro de cada
        /// estado, por antigüedad del pedido.
        /// </summary>
        Task<List<CorreccionS10ListItemDto>> GetAll(CorreccionS10FiltersDto filters);

        /// <summary>
        /// Una corrección viva por id con las rendiciones que cubre su consolidado y sus salidas, o
        /// null si no existe (o ya se cerró).
        /// </summary>
        Task<CorreccionS10DetalleDto?> GetDetalle(int correccionId);

        /// <summary>
        /// El detalle de UNA salida de la bandeja —trayectos, capturas y adjuntos—, en consulta.
        /// Null si la salida no está en una planilla de una corrección viva ni de su consolidado.
        /// </summary>
        Task<SolicitudSalidaDetalleDto?> GetSalidaDetalle(int solicitudId);

        /// <summary>Opciones de los filtros: solo lo que aparece en la bandeja.</summary>
        Task<CorreccionS10FilterDataDto> GetFilterData();

        /// <summary>
        /// Marca como atendidas las correcciones indicadas (el check de RG-22 / RF-OBS-07) y
        /// devuelve las que efectivamente se movieron. Las que ya estaban atendidas se ignoran
        /// en silencio: en una acción masiva la selección puede traer filas que otro ya resolvió.
        ///
        /// Lo que se corrige es el Consolidado del S10, así que atender una planilla atiende
        /// también las demás planillas del MISMO consolidado que siguen por atender: el pedido del
        /// consolidador es uno solo aunque la bandeja lo muestre por planilla.
        /// </summary>
        Task<List<GaCorreccionS10>> Atender(
            IEnumerable<int> correccionIds, string? comentario, int erpUserId);

        /// <summary>
        /// Lo que necesitan los avisos de atención de esas correcciones: UNO por pedido (mismo
        /// consolidado y mismo consolidador que lo pidió), no uno por planilla.
        /// </summary>
        Task<List<CorreccionS10CorreoDatos>> GetCorreoDatosAtendidas(IReadOnlyCollection<int> correccionIds);

        /// <summary>
        /// Correos de quienes pidieron esas correcciones (y las demás por atender de sus mismos
        /// consolidados): los consolidadores a los que les llegaría el aviso de atención.
        /// </summary>
        Task<List<string>> GetCorreosSolicitantes(IReadOnlyCollection<int> correccionIds);
    }
}
