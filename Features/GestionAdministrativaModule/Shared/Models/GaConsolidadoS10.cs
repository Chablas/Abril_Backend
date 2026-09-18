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
        /// Código de la rendición grupal: <c>CONS-&lt;ÁREA&gt;-AAAA-NNN</c> (CONS-GTH-2026-001), con
        /// correlativo propio por área y año. Es el nombre con el que se sigue al conjunto de
        /// planillas que se consolidaron juntas, en las cuatro pantallas por las que pasa (Gestión
        /// de Rendiciones, Consolidados, Correcciones S10 y Reembolsos), y el que lleva impreso la
        /// planilla de reembolso. La sigla sale de <c>area_item.abreviatura</c> del área del
        /// consolidado (<see cref="AreaScopeId"/>). Hasta el 2026-09-18 era CON-AAAA-NNNN, por año.
        ///
        /// Al REEMPLAZAR el documento el código NO cambia: la fila nueva hereda el de la que se da
        /// de baja, igual que una planilla conserva su REN al subsanarla. Lo que se reemplaza es el
        /// archivo; el grupo sigue siendo el mismo y Tesorería y el ERP lo venían siguiendo por ese
        /// nombre. Por eso los únicos de <c>codigo</c> y de (<c>anio</c>, <c>numero</c>) son
        /// parciales por <see cref="State"/>.
        ///
        /// Null solo en los consolidados anteriores a la columna que no alcanzó a numerar la
        /// migración.
        /// </summary>
        public string? Codigo { get; set; }

        /// <summary>Año del correlativo (hora de Perú). Null junto con <see cref="Codigo"/>.</summary>
        public int? Anio { get; set; }

        /// <summary>Correlativo dentro del área y el año. Null junto con <see cref="Codigo"/>.</summary>
        public int? Numero { get; set; }

        /// <summary>
        /// Área del consolidado: la del consolidador que lo subió (su ficha vigente → puesto → área
        /// de destino). Da la sigla del <see cref="Codigo"/> y el "ÁREA" de la planilla de
        /// reembolso. Se hereda al reemplazar, junto con el código. Null si no se pudo resolver.
        /// </summary>
        public int? AreaScopeId { get; set; }

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
        /// Número de reembolso que devuelve el S10. Es TEXTO y no un número: no es un correlativo
        /// nuestro y puede traer letras y separadores. Null solo en las filas viejas — ver
        /// <see cref="MontoTotal"/>.
        /// </summary>
        public string? NumeroReembolso { get; set; }

        /// <summary>
        /// PLANILLA GRUPAL ("PLANILLA DE REEMBOLSO" en el papel): el tercer documento del ciclo. Los
        /// trayectos de TODAS las planillas que cubre este consolidado en una sola tabla, con la
        /// columna RENDICIÓN diciendo de qué planilla sale cada fila, la cabecera del consolidador
        /// y el código <see cref="Codigo"/> impreso.
        ///
        /// La genera Abril One —no se sube— en el mismo acto en que el consolidador adjunta el
        /// Consolidado del S10, que es cuando queda definido qué planillas van juntas. Si el
        /// consolidado se reemplaza, se rehace: los montos pueden haber cambiado en la subsanación.
        ///
        /// Null en los consolidados anteriores a esta columna, que nunca la tuvieron.
        /// </summary>
        public string? PlanillaGrupalUrl { get; set; }
        public string? PlanillaGrupalItemId { get; set; }
        public string? PlanillaGrupalDriveId { get; set; }
        public string? PlanillaGrupalFilename { get; set; }

        /// <summary>
        /// Copia de la planilla grupal con la firma de la jefatura, que se estampa en el mismo acto
        /// que la del consolidado (aprobar el reembolso ES firmar) y con las mismas reglas: si firma
        /// más de una jefatura, las firmas se acumulan sobre esta copia. Null mientras no se apruebe,
        /// y en los consolidados aprobados antes de que la grupal se firmara.
        /// </summary>
        public string? PlanillaGrupalFirmadoUrl { get; set; }
        public string? PlanillaGrupalFirmadoItemId { get; set; }
        public string? PlanillaGrupalFirmadoFilename { get; set; }

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
