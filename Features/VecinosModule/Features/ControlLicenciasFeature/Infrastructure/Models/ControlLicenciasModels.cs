namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Models
{
    /// <summary>Catálogo de tipos de licencia: plantilla base (ProjectId null) o tipo propio de un proyecto.</summary>
    public class VecinoLicenciaControlTipo
    {
        public int VecinoLicenciaControlTipoId { get; set; }
        public int? ProjectId { get; set; }
        public string Descripcion { get; set; } = null!;
        public int Orden { get; set; }
        /// <summary>Días de antelación sugeridos por defecto al subir el documento (el usuario puede cambiarlos).</summary>
        public int? DiasAntesDefault { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }

    /// <summary>Catálogo fijo de estados: Pendiente, Cargado, No aplica, Vencido.</summary>
    public class VecinoLicenciaControlEstado
    {
        public int VecinoLicenciaControlEstadoId { get; set; }
        public string Descripcion { get; set; } = null!;
        public bool Active { get; set; }
        public bool State { get; set; }
    }

    /// <summary>Registro vigente de una licencia/permiso por proyecto + tipo.</summary>
    public class VecinoLicenciaControl
    {
        public int VecinoLicenciaControlId { get; set; }

        public int ProjectId { get; set; }

        public int VecinoLicenciaControlTipoId { get; set; }
        public VecinoLicenciaControlTipo? Tipo { get; set; }

        public int VecinoLicenciaControlEstadoId { get; set; }
        public VecinoLicenciaControlEstado? Estado { get; set; }

        public string? ArchivoUrl { get; set; }
        public string? OriginalFileName { get; set; }
        public DateOnly? FechaVencimiento { get; set; }

        /// <summary>Fecha en que se inscribió/tramitó el documento (dato informativo del dashboard).</summary>
        public DateOnly? FechaInscripcion { get; set; }
        /// <summary>Cuando no hay fecha real: NoSeCuenta/Pendiente/Indeterminado/NoRegistrada. Mutuamente excluyente con FechaInscripcion.</summary>
        public string? FechaInscripcionEstado { get; set; }

        /// <summary>Fecha de inicio de vigencia del documento.</summary>
        public DateOnly? FechaInicio { get; set; }
        public string? FechaInicioEstado { get; set; }

        /// <summary>Cuando no hay fecha real de vencimiento: NoSeCuenta/Pendiente/Indeterminado/NoRegistrada.</summary>
        public string? FechaVencimientoEstado { get; set; }

        /// <summary>Fecha de renovación, si el documento es renovable (distinto de FechaVencimiento).</summary>
        public DateOnly? FechaRenovacion { get; set; }
        public string? FechaRenovacionEstado { get; set; }

        /// <summary>SI/NO manual: si el documento está vigente/activo este mes, para el dashboard gerencial.</summary>
        public bool MesActivo { get; set; } = true;

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }

    /// <summary>
    /// Un recordatorio de una licencia (N días antes de vencer). Una licencia puede tener
    /// varios activos a la vez (ej. 30, 15, 7 y 2 días antes); cada uno se envía y se marca
    /// una sola vez, independiente de los demás.
    /// </summary>
    public class VecinoLicenciaControlRecordatorio
    {
        public int VecinoLicenciaControlRecordatorioId { get; set; }
        public int VecinoLicenciaControlId { get; set; }
        public int DiasAntes { get; set; }
        public DateOnly FechaRecordatorio { get; set; }
        public DateTime? EnviadoDateTime { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }

    /// <summary>
    /// Una fecha de visita de la municipalidad registrada en el Anexo H de una licencia.
    /// El recordatorio es fijo: 2 días antes de la fecha de visita, enviado a Residente y
    /// Administrador del proyecto (project.email_residente / project.email_coord_admin).
    /// </summary>
    public class VecinoLicenciaControlVisita
    {
        public int VecinoLicenciaControlVisitaId { get; set; }
        public int VecinoLicenciaControlId { get; set; }
        public DateOnly FechaVisita { get; set; }
        public string? Observacion { get; set; }
        public DateOnly FechaRecordatorio { get; set; }
        public DateTime? RecordatorioEnviadoDateTime { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }

    /// <summary>Versión anterior de una licencia, archivada al reemplazar el archivo vigente.</summary>
    public class VecinoLicenciaControlHistorial
    {
        public int VecinoLicenciaControlHistorialId { get; set; }
        public int VecinoLicenciaControlId { get; set; }
        public string ArchivoUrl { get; set; } = null!;
        public string? OriginalFileName { get; set; }
        public DateOnly? FechaVencimiento { get; set; }
        public DateOnly? FechaRecordatorio { get; set; }
        public int? DiasAntes { get; set; }
        public string? Motivo { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }

    /// <summary>Correo destinatario de recordatorios por proyecto + rol (Residente, Administrador, etc.).</summary>
    public class VecinoLicenciaControlDestinatario
    {
        public int VecinoLicenciaControlDestinatarioId { get; set; }
        public int ProjectId { get; set; }
        public string Rol { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
