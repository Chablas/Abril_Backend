namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// PDF "Consolidado del S10": el respaldo que devuelve el S10 una vez que las planillas de
    /// rendición quedaron registradas en el sistema contable.
    ///
    /// Un consolidado es UN registro en el S10 y puede cubrir VARIAS planillas —de uno o de varios
    /// trabajadores, siempre de una misma razón social (lo valida <c>ConsolidadoS10Service</c>)—.
    /// Qué planillas cubre no vive en esta fila sino en <see cref="GaConsolidadoS10Rendicion"/>.
    ///
    /// <see cref="SolicitudId"/> solo está en registros antiguos, de cuando el consolidado se podía
    /// asociar a una salida suelta: se sigue leyendo para no esconder el respaldo de esas
    /// rendiciones, pero ya no se escribe.
    ///
    /// Reemplazar el archivo no borra el anterior: el nuevo es otra fila, las planillas que cubre
    /// pasan a él y el viejo queda con <see cref="State"/> = false en cuanto no le queda ninguna
    /// (auditoría).
    /// </summary>
    public class GaConsolidadoS10
    {
        public int Id { get; set; }

        /// <summary>
        /// FK a <c>ga_solicitud_salida.id</c> en los consolidados antiguos que cubrían una sola
        /// salida. Null en el resto: lo que cubren está en <see cref="GaConsolidadoS10Rendicion"/>.
        /// </summary>
        public int? SolicitudId { get; set; }

        /// <summary>webUrl del PDF en SharePoint/OneDrive (para abrirlo desde el detalle).</summary>
        public string PdfUrl { get; set; } = string.Empty;
        public string? PdfItemId { get; set; }
        public string? PdfDriveId { get; set; }
        public string PdfFilename { get; set; } = string.Empty;

        /// <summary>
        /// Importe total con el que el S10 registró las planillas que cubre. Tiene que coincidir con
        /// la suma de esas planillas COMPLETAS (todas sus salidas); lo valida
        /// <c>ConsolidadoS10Service</c> antes de guardar.
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
        /// Copia del consolidado con la firma de la jefatura estampada en TODAS sus hojas. Se genera
        /// al aprobar el reembolso —aprobar ES la firma—, así que es null mientras no se apruebe. Si
        /// el consolidado es compartido y otro jefe aprueba otra de sus planillas, su firma se suma
        /// sobre esta misma copia, al lado de la anterior. El PDF original nunca se pisa.
        /// </summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoItemId { get; set; }
        public string? PdfFirmadoFilename { get; set; }

        /// <summary>FK a <c>app_user.user_id</c> del último jefe que firmó. Null hasta que se apruebe.</summary>
        public int? FirmadoPorId { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }

        /// <summary>FK a <c>app_user.user_id</c> de quien subió el archivo.</summary>
        public int UploadedById { get; set; }
        public DateTimeOffset UploadedAt { get; set; }

        /// <summary>Soft delete: false = versión reemplazada, ya no cubre ninguna planilla.</summary>
        public bool State { get; set; } = true;
    }
}
