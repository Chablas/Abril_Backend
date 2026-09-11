namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>Ámbito al que se asocia el PDF Consolidado del S10. Excluyentes entre sí.</summary>
    public enum ConsolidadoS10Ambito
    {
        /// <summary>Cubre planillas de rendición completas —una o varias—. Es el caso normal.</summary>
        Rendicion = 1,
        /// <summary>Cubre solo esa salida puntual. Solo existe en registros antiguos.</summary>
        Solicitud = 2,
    }

    /// <summary>
    /// Consolidado del S10 vigente de una planilla (o de una salida, en los registros antiguos),
    /// para exponerlo al frontend. Las planillas que comparten consolidado reciben el mismo: es un
    /// solo documento.
    /// </summary>
    public class ConsolidadoS10Dto
    {
        public int Id { get; set; }
        /// <summary>"Rendicion" | "Solicitud" — a qué quedó asociado el archivo.</summary>
        public string Ambito { get; set; } = string.Empty;
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        /// <summary>Importe total con el que el S10 registró las planillas. Null en los consolidados viejos.</summary>
        public decimal? MontoTotal { get; set; }
        /// <summary>Número de guía del S10 (texto). Null en los consolidados viejos.</summary>
        public string? NumeroGuia { get; set; }

        /// <summary>
        /// Copia firmada por la jefatura (todas sus hojas). Se genera al aprobar el reembolso, así
        /// que es null mientras no se haya aprobado.
        /// </summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }

        public DateTimeOffset UploadedAt { get; set; }

        /// <summary>
        /// Planillas que cubre (sus vínculos vigentes), ordenadas por código. Las pantallas la usan
        /// para decir con qué otras rendiciones se comparte el documento. La llenan los loaders;
        /// vacía en los consolidados antiguos por salida y en los ya reemplazados.
        /// </summary>
        public List<ConsolidadoS10RendicionDto> Rendiciones { get; set; } = new();
    }

    /// <summary>Una planilla cubierta por un Consolidado del S10.</summary>
    public class ConsolidadoS10RendicionDto
    {
        public int Id { get; set; }
        /// <summary>Código REN-AAAA-NNNN (o "#id" en las anteriores al código).</summary>
        public string Codigo { get; set; } = string.Empty;
    }
}
