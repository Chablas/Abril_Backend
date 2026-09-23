using Abril_Backend.Shared.Services.Firma.Dtos;

namespace Abril_Backend.Shared.Services.Firma.Interfaces
{
    /// <summary>
    /// Acceso a las firmas de una persona (<c>person_firma</c>, una por <c>firma_tipo</c>). Vive en
    /// Shared porque las MISMAS firmas las registran y las estampan tres módulos: Contabilidad
    /// (visado de facturas), Gestión GTH (carta oferta del postulante) y Gestión Administrativa
    /// (planilla de rendición y Consolidado del S10). Una persona tiene sus firmas, no una por
    /// módulo.
    /// </summary>
    public interface IFirmaPersonalRepository
    {
        /// <summary>
        /// Qué tipos de firma se ofrecen hoy y cuáles tiene ya registrados el usuario. Va todo en
        /// una consulta porque quien lo pide (el panel de firma y el modal de aprobar) necesita las
        /// dos cosas a la vez para saber qué dibujar.
        /// </summary>
        Task<FirmaPersonalEstadoDto> GetEstadoByUserId(int userId);

        /// <summary>
        /// Crea o actualiza (upsert) la firma del usuario indicado para ese tipo. Devuelve el estado
        /// completo ya actualizado para que la pantalla no tenga que volver a pedirlo.
        /// </summary>
        Task<FirmaPersonalEstadoDto> Upsert(int userId, string tipoCodigo, byte[] imageBytes, string mime);

        /// <summary>
        /// Bytes de la firma del usuario para estampar, o null si no tiene ninguna que sirva.
        /// </summary>
        /// <param name="tiposPermitidos">
        /// Códigos de tipo que la pantalla acepta. null = cualquiera (Contabilidad y GTH, que no
        /// miran la configuración de Consolidados). Entre varias candidatas gana la IMAGEN: cuando
        /// alguien se tomó el trabajo de subir una, es la que quiere ver estampada.
        /// </param>
        Task<(byte[] Bytes, string Mime)?> GetActiveBytesByUserId(
            int userId, IReadOnlyCollection<string>? tiposPermitidos = null);

        /// <summary>Igual que <see cref="Upsert"/> pero por ficha, para quien no tiene usuario (postulantes).</summary>
        Task UpsertByPersonId(int personId, string tipoCodigo, byte[] imageBytes, string mime);

        /// <summary>Bytes de la firma de una ficha (para estampar), o null si no tiene ninguna.</summary>
        Task<(byte[] Bytes, string Mime, DateTimeOffset? UpdatedDateTime)?> GetActiveBytesByPersonId(int personId);

        /// <summary>Los tipos del catálogo con su bandera de habilitado, en orden de visualización.</summary>
        Task<List<FirmaTipoDto>> GetTipos();

        /// <summary>Guarda qué tipos quedan habilitados y devuelve el catálogo ya actualizado.</summary>
        Task<List<FirmaTipoDto>> SaveTipos(IReadOnlyCollection<FirmaTipoActivoDto> tipos, int userId);
    }
}
