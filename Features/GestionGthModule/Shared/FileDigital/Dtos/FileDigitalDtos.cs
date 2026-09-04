namespace Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos
{
    /// <summary>
    /// Biblioteca de SharePoint donde el módulo guarda el file de los colaboradores — hoy el «File de
    /// colaboradores» (<c>gth_carta_oferta_folder</c>): el link con el que se resuelve y su nombre
    /// legible, que es el primer tramo de la ruta que se muestra en pantalla.
    /// </summary>
    public class FileDigitalFolderDto
    {
        public string LinkUrl { get; set; } = string.Empty;
        public string? FolderName { get; set; }
    }

    /// <summary>
    /// Carpeta de SharePoint que hace de file digital del colaborador («{DNI} - {NOMBRE}» dentro de
    /// la biblioteca configurada). Se resuelve una vez (al enviar la carta oferta) y se persiste: los
    /// documentos siguientes se suben ahí sin volver a derivarla del DNI y el nombre. Cada tipo de
    /// documento vive en una subcarpeta suya («Carta Oferta Enviada», «Carta Oferta Firmada»), que se
    /// resuelve en el momento de subir.
    /// </summary>
    public class FileDigitalCarpetaDto
    {
        public string DriveId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Ruta legible («File de colaboradores / 80508050 - DIEGO HERRERA»).</summary>
        public string? Ruta { get; set; }
    }

    /// <summary>Documento ya subido al file digital, para persistirlo en la fila que lo referencia.</summary>
    public class FileDigitalDocumentoDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? ItemId { get; set; }
        public string? DriveId { get; set; }
    }
}
