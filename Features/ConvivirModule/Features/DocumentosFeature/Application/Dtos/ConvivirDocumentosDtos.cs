using Abril_Backend.Features.ConvivirModule.Shared.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Dtos
{
    /// <summary>«Mis documentos» de la app: los de la propiedad elegida, en una sola llamada.</summary>
    public class ConvivirDocumentosDto
    {
        /// <summary>La pedida si es suya; si no, la primera. Null si todavía no tiene propiedades.</summary>
        public ConvivirPropiedadDto? Propiedad { get; set; }
        /// <summary>Los tipos que tienen documentos, en el orden del catálogo: los filtros de la pantalla.</summary>
        public List<string> Tipos { get; set; } = new();
        /// <summary>Los más recientes primero.</summary>
        public List<ConvivirDocumentoDto> Documentos { get; set; } = new();
    }

    public class ConvivirDocumentoDto
    {
        public int DocumentoId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        /// <summary>Extensión en mayúsculas («PDF», «JPG»), para mostrar.</summary>
        public string Formato { get; set; } = string.Empty;
        /// <summary>Con él la app elige con qué abrirlo.</summary>
        public string ContentType { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
        /// <summary>Día en que Abril lo subió (hora de Perú).</summary>
        public DateOnly Fecha { get; set; }
        /// <summary>El propietario todavía no lo abrió.</summary>
        public bool Nuevo { get; set; }
    }

    /// <summary>Fila cruda de un documento; el servicio la pasa a <see cref="ConvivirDocumentoDto"/>.</summary>
    public class ConvivirDocumentoFila
    {
        public int DocumentoId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public int TipoOrden { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ArchivoNombre { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
        public DateTime SubidoEl { get; set; }
        public bool Nuevo { get; set; }
    }

    /// <summary>Dónde está el archivo de un documento del propietario, para descargarlo.</summary>
    public class ConvivirDocumentoArchivoFila
    {
        public string ArchivoNombre { get; set; } = string.Empty;
        public string ArchivoContentType { get; set; } = string.Empty;
        public string ArchivoDriveId { get; set; } = string.Empty;
        public string ArchivoItemId { get; set; } = string.Empty;
    }
}
