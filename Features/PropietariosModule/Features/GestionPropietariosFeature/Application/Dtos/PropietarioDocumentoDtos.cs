namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos
{
    /// <summary>
    /// Modal «Documentos» de un propietario: los tipos (para el desplegable) y, por cada propiedad
    /// vigente, sus documentos. Lo devuelven también guardar y eliminar, para repintar sin otra llamada.
    /// </summary>
    public class PropietarioDocumentosDto
    {
        public List<PropietarioDocumentoTipoDto> Tipos { get; set; } = new();
        public List<PropiedadDocumentosDto> Propiedades { get; set; } = new();
    }

    public class PropietarioDocumentoTipoDto
    {
        public int TipoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class PropiedadDocumentosDto
    {
        public int PropietarioId { get; set; }
        public string Proyecto { get; set; } = string.Empty;
        public string? Torre { get; set; }
        public string Departamento { get; set; } = string.Empty;
        public List<PropietarioDocumentoDto> Documentos { get; set; } = new();
    }

    public class PropietarioDocumentoDto
    {
        public int DocumentoId { get; set; }
        public int PropietarioId { get; set; }
        public int TipoId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        /// <summary>Nombre del archivo que se subió: se descarga con ese nombre.</summary>
        public string ArchivoNombre { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
        /// <summary>Hora de Perú.</summary>
        public DateTime SubidoEl { get; set; }
        /// <summary>Hora de Perú. Primera vez que el propietario lo abrió en la app; null si todavía no.</summary>
        public DateTime? LeidoEl { get; set; }
    }

    /// <summary>
    /// Un documento nuevo del guardado. Viaja en el campo <c>data</c> del multipart (una lista), y
    /// su archivo en <c>archivos</c>, en el mismo orden.
    /// </summary>
    public class PropietarioDocumentoNuevoDto
    {
        public int PropietarioId { get; set; }
        public int TipoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>Lo que el guardado necesita saber antes de subir los archivos, en un solo viaje.</summary>
    public class PropietarioDocumentosSubidaDto
    {
        public string? Dni { get; set; }
        public string? FullName { get; set; }
        /// <summary>Solo las propiedades vigentes de la persona entre las pedidas.</summary>
        public List<PropiedadDocumentosDto> Propiedades { get; set; } = new();
        public HashSet<int> TiposVigentes { get; set; } = new();
        /// <summary>Link de la fila vigente de <c>propietario_documento_folder</c>.</summary>
        public string? LinkCarpeta { get; set; }
    }

    /// <summary>Un documento ya subido a SharePoint, listo para insertar.</summary>
    public class PropietarioDocumentoInsertDto
    {
        public int PropietarioId { get; set; }
        public int TipoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ArchivoNombre { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
        public string Url { get; set; } = string.Empty;
        public string DriveId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
    }

    /// <summary>Dónde está el archivo de un documento, para descargarlo.</summary>
    public class PropietarioDocumentoArchivoRefDto
    {
        public string ArchivoNombre { get; set; } = string.Empty;
        public string ArchivoContentType { get; set; } = string.Empty;
        public string ArchivoDriveId { get; set; } = string.Empty;
        public string ArchivoItemId { get; set; } = string.Empty;
    }
}
