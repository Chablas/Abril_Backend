using Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Interfaces
{
    /// <summary>
    /// Sirve los archivos del módulo —planillas, documentos del consolidado y adjuntos de los
    /// trayectos— para que los modales los muestren embebidos. Pasan por el backend porque el
    /// navegador no puede leer un webUrl de SharePoint (CORS y sesión de Microsoft 365).
    ///
    /// No es un proxy de SharePoint: solo sirve URLs que están guardadas en las tablas del módulo,
    /// y solo a su dueño o a quien tenga alguna bandeja de revisión.
    /// </summary>
    public interface IArchivoSalidaService
    {
        /// <summary>404 si la URL no es un archivo del módulo o el usuario no puede verlo.</summary>
        Task<ArchivoSalidaDto> Get(string url, int userId, int[] roleIds);
    }
}
