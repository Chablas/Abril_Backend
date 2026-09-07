namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// PDF "Consolidado del S10" de una salida ya rendida — el respaldo que devuelve el S10 una vez
    /// que la planilla de rendición quedó registrada en el sistema contable.
    ///
    /// El ámbito lo elige quien lo sube y es EXCLUYENTE (CHECK <c>chk_ga_consolidado_s10_ambito_unico</c>):
    /// <list type="bullet">
    ///   <item><see cref="RendicionId"/> → cubre toda la planilla, es decir TODAS las salidas de ese
    ///   batch de rendición. Es el caso normal: una planilla = un registro en el S10.</item>
    ///   <item><see cref="SolicitudId"/> → cubre solo esa salida puntual, cuando el consolidado del
    ///   S10 no corresponde a la planilla completa.</item>
    /// </list>
    ///
    /// Reemplazar el archivo no borra el anterior: la fila vieja queda con <see cref="State"/> = false
    /// (auditoría) y los índices únicos parciales garantizan a lo sumo un consolidado vigente por
    /// rendición y uno por solicitud.
    /// </summary>
    public class GaConsolidadoS10
    {
        public int Id { get; set; }

        /// <summary>FK a <c>ga_rendicion.id</c> cuando el consolidado cubre la planilla completa. Excluyente con <see cref="SolicitudId"/>.</summary>
        public int? RendicionId { get; set; }

        /// <summary>FK a <c>ga_solicitud_salida.id</c> cuando el consolidado cubre solo esa salida. Excluyente con <see cref="RendicionId"/>.</summary>
        public int? SolicitudId { get; set; }

        /// <summary>webUrl del PDF en SharePoint/OneDrive (para abrirlo desde el detalle).</summary>
        public string PdfUrl { get; set; } = string.Empty;
        public string? PdfItemId { get; set; }
        public string? PdfDriveId { get; set; }
        public string PdfFilename { get; set; } = string.Empty;

        /// <summary>
        /// Importe total con el que el S10 registró la planilla. Tiene que coincidir con el monto
        /// de la planilla COMPLETA (todas sus salidas, que es lo que cubre el consolidado); lo
        /// valida <c>ConsolidadoS10Service</c> antes de guardar.
        ///
        /// Nullable solo por los consolidados subidos antes de que el formulario pidiera el dato:
        /// no hay valor cierto con el que rellenarlos y rellenarlo a mano ensuciaría la auditoría.
        /// En las filas nuevas nunca es null.
        /// </summary>
        public decimal? MontoTotal { get; set; }

        /// <summary>
        /// Número de guía que devuelve el S10. Es TEXTO y no un número: no es un correlativo
        /// nuestro y puede traer letras y separadores. Null solo en las filas viejas — ver
        /// <see cref="MontoTotal"/>.
        /// </summary>
        public string? NumeroGuia { get; set; }

        /// <summary>
        /// Copia del consolidado con la firma del revisor estampada en TODAS sus hojas. Se genera
        /// al aprobar el reembolso —aprobar ES la firma—, así que es null mientras no se apruebe.
        /// El PDF original nunca se pisa: la firma va sobre una copia.
        /// </summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoItemId { get; set; }
        public string? PdfFirmadoFilename { get; set; }

        /// <summary>FK a <c>app_user.user_id</c> del revisor que firmó. Null hasta que se apruebe.</summary>
        public int? FirmadoPorId { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }

        /// <summary>FK a <c>app_user.user_id</c> de quien subió el archivo.</summary>
        public int UploadedById { get; set; }
        public DateTimeOffset UploadedAt { get; set; }

        /// <summary>Soft delete: false = versión reemplazada por una subida posterior.</summary>
        public bool State { get; set; } = true;
    }
}
