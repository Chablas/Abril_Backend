using Abril_Backend.Shared.Services.Convivir.Dtos;

namespace Abril_Backend.Shared.Services.Convivir.Interfaces
{
    /// <summary>
    /// Archivos de los documentos de los propietarios en SharePoint. Global porque sube la intranet
    /// (módulo Propietarios) y descargan la intranet y la app (ConvivirModule).
    /// </summary>
    public interface IPropietarioDocumentoStorage
    {
        /// <summary>
        /// Formato y peso. Se llama con todo el lote antes de subir nada, para que un archivo
        /// inválido no deje la mitad de los otros en SharePoint.
        /// </summary>
        void Validar(IFormFile archivo);

        /// <summary>Resuelve el link de <c>propietario_documento_folder</c>. Lanza si falta o no se puede abrir.</summary>
        Task<PropietarioDocumentoCarpetaDto> ResolverCarpetaAsync(string? linkCarpeta);

        /// <summary>
        /// Sube el archivo dentro de <paramref name="subcarpetas"/> (se crean si no existen) con un
        /// nombre legible y único: la subida de Graph reemplaza al que tenga el mismo nombre.
        /// </summary>
        Task<PropietarioDocumentoSubidoDto> SubirAsync(
            PropietarioDocumentoCarpetaDto carpeta,
            IReadOnlyList<string> subcarpetas,
            string nombre,
            IFormFile archivo);

        Task<PropietarioDocumentoArchivoDto> DescargarAsync(string driveId, string itemId);

        /// <summary>Tipo MIME por extensión: el navegador a veces no lo manda, y la app abre el archivo con él.</summary>
        string ContentType(string nombreArchivo);
    }
}
