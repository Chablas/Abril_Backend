using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Models
{
    /// <summary>
    /// Un batch de rendición — produce 1 PDF que agrupa N solicitudes (una página por trabajador).
    /// Persiste la referencia al archivo subido a SharePoint.
    /// </summary>
    public class GaRendicion
    {
        public int Id { get; set; }

        /// <summary>
        /// Código único de la rendición en formato <c>REN-AAAA-NNNN</c> (RG-02). Es el
        /// identificador que ve el trabajador en pantallas y correos; el <see cref="Id"/> es
        /// interno y <see cref="NumeroPlanilla"/> es el correlativo que se imprime en el papel.
        ///
        /// Sobrevive a la subsanación: cuando una rendición observada se vuelve a generar, el PDF
        /// se reemplaza y esta fila —con su código— es la misma.
        ///
        /// Nullable solo por las planillas anteriores a la columna; la migración las numeró todas.
        /// </summary>
        public string? Codigo { get; set; }

        /// <summary>Año del código (AAAA), en hora Perú — el correlativo se reinicia con él.</summary>
        public int? Anio { get; set; }

        /// <summary>Correlativo (NNNN) dentro del año.</summary>
        public int? Numero { get; set; }

        /// <summary>WebUrl devuelta por SharePoint al subir el PDF.</summary>
        public string PdfUrl { get; set; } = string.Empty;
        /// <summary>Item ID de Graph (opcional, útil para re-descargas vía API).</summary>
        public string? PdfItemId { get; set; }
        /// <summary>Nombre original del archivo subido (ej. "Planilla_Rendicion_20260522_153012_u14.pdf").</summary>
        public string PdfFilename { get; set; } = string.Empty;
        /// <summary>Usuario que disparó la rendición.</summary>
        public int RendidoPorId { get; set; }
        public DateTimeOffset RendidoAt { get; set; }
        /// <summary>Número correlativo del registro de planilla — se imprime como "TI: 000NNN" en el PDF.</summary>
        public int? NumeroPlanilla { get; set; }

        // -- Primera revisión de la jefatura ----------------------------------
        // El eje de estado que va ANTES del Consolidado del S10: el jefe revisa tramos, montos y
        // capturas registrados en Abril One y solo con su aprobación se habilita cargar el
        // consolidado (RG-30 y RG-35). Vive acá y no en la salida porque lo que se revisa es la
        // planilla entera. Ver EstadosSalida.PrimeraRevision.

        /// <summary>FK a <c>ga_estado_primera_revision</c>. Ver <see cref="EstadosSalida.PrimeraRevision"/>.</summary>
        public int EstadoPrimeraRevisionId { get; set; } = EstadosSalida.PrimeraRevision.Borrador;

        /// <summary>Momento en que el trabajador la envió a primera revisión.</summary>
        public DateTimeOffset? EnviadaRevisionAt { get; set; }
        /// <summary>FK a <c>app_user.user_id</c> de quien la envió.</summary>
        public int? EnviadaRevisionPorId { get; set; }

        /// <summary>Momento de la decisión del jefe en la primera revisión.</summary>
        public DateTimeOffset? PrimeraRevisionAt { get; set; }
        /// <summary>FK a <c>app_user.user_id</c> del jefe que aprobó u observó.</summary>
        public int? PrimeraRevisionPorId { get; set; }

        /// <summary>
        /// Comentario que escribe el jefe al OBSERVAR la primera revisión: es lo que el trabajador
        /// tiene que corregir. Se conserva después de subsanar para que se vea qué se observó.
        /// </summary>
        public string? PrimeraRevisionObservacion { get; set; }

        // -- Copia firmada por la jefatura ------------------------------------
        // El PDF original (PdfUrl) NO se toca: la firma se estampa sobre una copia y se sube
        // aparte. Vive en la planilla y no en cada salida porque el documento es uno solo -- la
        // planilla puede cubrir varias salidas y todas comparten la misma hoja firmada.

        /// <summary>WebUrl de la copia firmada en SharePoint. Null mientras nadie la firme.</summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoItemId { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        /// <summary>FK a <c>app_user.user_id</c> del jefe que firmo.</summary>
        public int? FirmadoPorId { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
    }
}
