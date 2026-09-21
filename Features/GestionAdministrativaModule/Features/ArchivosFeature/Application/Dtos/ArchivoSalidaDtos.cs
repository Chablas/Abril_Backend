namespace Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Dtos
{
    /// <summary>Un archivo del módulo ya bajado de SharePoint, listo para servirse.</summary>
    public class ArchivoSalidaDto
    {
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
    }

    /// <summary>Lo que dice la base de una URL pedida.</summary>
    public enum AccesoArchivoSalida
    {
        /// <summary>No es un archivo del módulo: el endpoint no sirve cualquier cosa de SharePoint.</summary>
        NoExiste = 0,
        /// <summary>Es del módulo, pero ni es del usuario ni tiene una bandeja de revisión.</summary>
        SinAcceso = 1,
        Permitido = 2,
    }
}
