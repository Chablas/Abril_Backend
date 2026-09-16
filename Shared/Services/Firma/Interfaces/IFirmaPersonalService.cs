using Abril_Backend.Shared.Services.Firma.Dtos;

namespace Abril_Backend.Shared.Services.Firma.Interfaces
{
    /// <summary>
    /// Registro de las firmas del usuario actual. Una persona tiene una firma POR TIPO
    /// (<c>person_firma</c> → <c>firma_tipo</c>): puede tener la dibujada con el mouse, la subida
    /// como imagen, o las dos. Se registran desde cualquiera de las pantallas que las ofrecen: la
    /// firma que se guarda en Contabilidad es la misma que estampa Gestión Administrativa en la
    /// planilla de rendición.
    /// </summary>
    public interface IFirmaPersonalService
    {
        /// <summary>Qué tipos se ofrecen y qué firmas tiene ya registradas el usuario indicado.</summary>
        Task<FirmaPersonalEstadoDto> GetEstado(int userId);

        /// <summary>
        /// Valida y guarda la firma del usuario para el tipo indicado; devuelve el estado completo
        /// ya actualizado.
        /// </summary>
        Task<FirmaPersonalEstadoDto> Save(FirmaPersonalSaveDto dto, int userId);

        /// <summary>Los tipos de firma del catálogo con su bandera de habilitado.</summary>
        Task<List<FirmaTipoDto>> GetTipos();

        /// <summary>Guarda qué tipos de firma quedan habilitados para firmar.</summary>
        Task<List<FirmaTipoDto>> SaveTipos(FirmaTiposSaveDto dto, int userId);
    }
}
