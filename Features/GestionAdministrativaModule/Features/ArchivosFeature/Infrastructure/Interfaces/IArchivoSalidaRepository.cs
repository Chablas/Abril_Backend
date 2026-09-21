using Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Archivos.Infrastructure.Interfaces
{
    public interface IArchivoSalidaRepository
    {
        /// <summary>
        /// Si la URL es un archivo del módulo y si el usuario puede verlo: es suyo (de una salida,
        /// planilla o consolidado que lo incluye) o sus roles le dan alguna bandeja de revisión.
        /// </summary>
        Task<AccesoArchivoSalida> GetAcceso(string url, int userId, int[] roleIds);
    }
}
