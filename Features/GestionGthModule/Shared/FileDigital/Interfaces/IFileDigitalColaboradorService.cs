using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Interfaces
{
    /// <summary>
    /// File digital del colaborador en SharePoint: la carpeta «{DNI} - {NOMBRE}» dentro de la
    /// biblioteca configurada, con una subcarpeta por tipo de documento.
    ///
    /// Vive en el <c>Shared/</c> del módulo porque lo usan tres caras del mismo expediente: la
    /// pantalla de GTH en Reclutamiento (que carga la carta oferta y puede subir la firmada a mano),
    /// la página pública donde el candidato firma la suya, y Onboarding, que sigue llenando ese
    /// mismo file. Las tres tienen que dejar los documentos exactamente en el mismo sitio, así que
    /// resolver la carpeta y subir un documento se escribe una sola vez acá: si cada servicio armara
    /// el nombre de la carpeta por su cuenta, un colaborador podría terminar con dos files.
    /// </summary>
    public interface IFileDigitalColaboradorService
    {
        /// <summary>
        /// Resuelve (creando si hace falta) el file digital del colaborador dentro de la biblioteca
        /// configurada en <c>gth_carta_oferta_folder</c>. Solo se llama cuando la fila que lo usa no
        /// tiene su carpeta persistida: las que sí la tienen la reusan tal cual.
        /// </summary>
        Task<FileDigitalCarpetaDto> ResolverCarpetaAsync(string dni, string nombre);

        /// <summary>
        /// Sube un documento a la <paramref name="subcarpeta"/> del file del colaborador, creándola si
        /// es la primera vez. <paramref name="queEs"/> solo arma el mensaje de error
        /// ("la carta oferta firmada").
        /// </summary>
        Task<FileDigitalDocumentoDto> SubirDocumentoAsync(
            FileDigitalCarpetaDto carpeta,
            string subcarpeta,
            string fileName,
            byte[] content,
            string contentType,
            string queEs);

        /// <summary>
        /// Nombre con el que se guarda un documento del expediente:
        /// <c>{prefijo}_{código del requerimiento}_{sello de tiempo}{extensión}</c>. El sello evita
        /// pisar una versión anterior del mismo documento.
        /// </summary>
        string NombreArchivo(string prefijo, string codigo, string extension);

        /// <summary>
        /// Baja un documento del file convertido a PDF por SharePoint. Lo usa la carta oferta, que
        /// se arma en Word para que GTH la revise —y la corrija ahí mismo si hace falta— pero se le
        /// manda al candidato en PDF, que es lo que puede ver y firmar desde el enlace.
        ///
        /// Convierte lo que HAY en SharePoint, no lo que se generó: así el PDF se lleva también las
        /// correcciones posteriores. <paramref name="queEs"/> solo arma el mensaje de error.
        /// </summary>
        Task<byte[]> DescargarComoPdfAsync(string driveId, string itemId, string queEs);
    }

    /// <summary>
    /// Lectura de la biblioteca configurada donde vive el file de los colaboradores. Es lo único que
    /// <see cref="IFileDigitalColaboradorService"/> necesita de la base de datos, así que va en su
    /// propio repositorio en vez de colgar del repositorio de una feature: si dependiera del de
    /// Onboarding, Reclutamiento no podría subir la carta oferta sin arrastrarse esa feature entera.
    /// </summary>
    public interface IFileDigitalFolderRepository
    {
        /// <summary>La fila vigente de <c>gth_carta_oferta_folder</c>. Null si no hay ninguna.</summary>
        Task<FileDigitalFolderDto?> GetFolder();
    }
}
