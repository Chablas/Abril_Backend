namespace Abril_Backend.Shared.Services.Convivir.Dtos
{
    /// <summary>
    /// La carpeta de <c>propietario_documento_folder</c> ya resuelta, para un guardado: se resuelve
    /// una sola vez por lote y recuerda las subcarpetas que ya creó o encontró.
    /// </summary>
    public sealed class PropietarioDocumentoCarpetaDto
    {
        public string DriveId { get; init; } = string.Empty;
        public string ItemId { get; init; } = string.Empty;
        internal Dictionary<string, string> Subcarpetas { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Dónde quedó en SharePoint un documento recién subido.</summary>
    public sealed class PropietarioDocumentoSubidoDto
    {
        public string Url { get; init; } = string.Empty;
        public string DriveId { get; init; } = string.Empty;
        public string ItemId { get; init; } = string.Empty;
    }

    /// <summary>El contenido de un documento, para devolverlo tal cual.</summary>
    public sealed class PropietarioDocumentoArchivoDto
    {
        public byte[] Contenido { get; init; } = [];
        public string ContentType { get; init; } = "application/octet-stream";
    }
}
