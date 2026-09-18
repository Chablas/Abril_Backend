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
        /// <summary>
        /// Código de la rendición grupal, <c>CONS-ÁREA-AAAA-NNN</c>: el nombre del conjunto de planillas
        /// que se consolidaron juntas. Sobrevive al reemplazo del archivo. Null en los consolidados
        /// anteriores a la columna.
        /// </summary>
        public string? Codigo { get; set; }
        /// <summary>"Rendicion" | "Solicitud" — a qué quedó asociado el archivo.</summary>
        public string Ambito { get; set; } = string.Empty;
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        /// <summary>Importe total con el que el S10 registró las planillas. Null en los consolidados viejos.</summary>
        public decimal? MontoTotal { get; set; }
        /// <summary>Número del reembolso del S10 (texto). Null en los consolidados viejos.</summary>
        public string? NumeroReembolso { get; set; }

        /// <summary>
        /// La PLANILLA GRUPAL: el PDF que junta en un solo documento las planillas de gasto de todo
        /// lo que cubre el consolidado. La genera Abril One al adjuntarse el S10. Null en los
        /// consolidados anteriores a la columna.
        /// </summary>
        public string? PlanillaGrupalUrl { get; set; }
        public string? PlanillaGrupalFilename { get; set; }
        /// <summary>
        /// Copia de la planilla grupal con la firma de la jefatura. Null mientras no se apruebe, y
        /// en los consolidados aprobados antes de que la grupal se firmara.
        /// </summary>
        public string? PlanillaGrupalFirmadoUrl { get; set; }
        public string? PlanillaGrupalFirmadoFilename { get; set; }

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

    /// <summary>
    /// Resultado de adjuntar (Gestión de Rendiciones) o reemplazar (Consolidados) el Consolidado del
    /// S10: el documento que quedó y cómo salió el aviso a la jefatura, que va pegado al mismo paso.
    /// Consolidar es exactamente lo que deja el reembolso esperando la firma, así que avisar no es un
    /// trámite aparte que haya que acordarse de hacer.
    ///
    /// El aviso es best-effort: si no sale —está apagado en Configuración → Correos, no se pudo
    /// resolver a quién, o falló el envío— el consolidado igual quedó adjunto y
    /// <see cref="AvisoJefatura"/> dice por qué. Desde Consolidados se puede volver a mandar a mano.
    /// </summary>
    public class ConsolidadoS10UploadResultDto
    {
        /// <summary>El consolidado adjunto, tal como lo devuelve la subida.</summary>
        public ConsolidadoS10Dto Consolidado { get; set; } = new();

        /// <summary>true = el correo salió y la jefatura ya lo tiene en su bandeja.</summary>
        public bool JefaturaAvisada { get; set; }

        /// <summary>Qué pasó con el aviso, para imprimirlo tal cual en la pantalla.</summary>
        public string AvisoJefatura { get; set; } = string.Empty;
    }
}
