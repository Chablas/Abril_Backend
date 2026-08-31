namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Dtos
{
    public class CatalogOptionDto
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = null!;
    }

    public class ProjectOptionDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
    }

    /// <summary>Un tipo de licencia dentro de la plantilla de un proyecto (base o propio).</summary>
    public class VecinoLicenciaTipoDto
    {
        public int VecinoLicenciaControlTipoId { get; set; }
        public string Descripcion { get; set; } = null!;
        public int Orden { get; set; }
        /// <summary>true = viene de la plantilla base (compartida); false = agregado solo para este proyecto.</summary>
        public bool EsBase { get; set; }
        public int? DiasAntesDefault { get; set; }
    }

    /// <summary>Alta o edición de un tipo del catálogo base (visible en todos los proyectos).</summary>
    public class VecinoLicenciaTipoBaseUpsertDto
    {
        public string Descripcion { get; set; } = null!;
        public int? DiasAntesDefault { get; set; }
    }

    /// <summary>Estado de un tipo de licencia para un proyecto puntual (fila de la tabla del control).</summary>
    public class VecinoLicenciaItemDto
    {
        /// <summary>Solo poblado cuando el item viene de la vista combinada (todos los proyectos): a qué proyecto pertenece.</summary>
        public int? ProjectId { get; set; }
        public string? ProjectDescription { get; set; }

        /// <summary>Id del registro vigente (null si el proyecto aún no tiene fila para este tipo).</summary>
        public int? VecinoLicenciaControlId { get; set; }
        public int VecinoLicenciaControlTipoId { get; set; }
        public string TipoDescripcion { get; set; } = null!;
        public int Orden { get; set; }
        public bool EsBase { get; set; }

        public int VecinoLicenciaControlEstadoId { get; set; }
        public string EstadoDescripcion { get; set; } = null!;

        public string? ArchivoUrl { get; set; }
        public string? OriginalFileName { get; set; }
        public DateOnly? FechaVencimiento { get; set; }
        /// <summary>Días de antelación por defecto del tipo, para sugerir el primer recordatorio al subir.</summary>
        public int? DiasAntesDefault { get; set; }

        public DateOnly? FechaInscripcion { get; set; }
        public string? FechaInscripcionEstado { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public string? FechaInicioEstado { get; set; }
        public string? FechaVencimientoEstado { get; set; }
        public DateOnly? FechaRenovacion { get; set; }
        public string? FechaRenovacionEstado { get; set; }
        /// <summary>SI/NO manual: si el documento está vigente/activo este mes.</summary>
        public bool MesActivo { get; set; } = true;

        /// <summary>Recordatorios activos de la licencia vigente (puede haber varios, ej. 30/15/7/2 días antes).</summary>
        public List<VecinoLicenciaRecordatorioDto> Recordatorios { get; set; } = new();

        /// <summary>Cantidad de versiones anteriores archivadas en el historial.</summary>
        public int VersionesHistorial { get; set; }

        /// <summary>Fechas de visita de la municipalidad registradas (solo aplica al tipo Anexo H).</summary>
        public List<VecinoLicenciaVisitaDto> Visitas { get; set; } = new();
    }

    /// <summary>Una fecha de visita de la municipalidad registrada en el Anexo H de una licencia.</summary>
    public class VecinoLicenciaVisitaDto
    {
        public int VecinoLicenciaControlVisitaId { get; set; }
        public DateOnly FechaVisita { get; set; }
        public string? Observacion { get; set; }
        public DateOnly FechaRecordatorio { get; set; }
        public bool Enviado { get; set; }
    }

    /// <summary>Agrega una fecha de visita al Anexo H de la licencia vigente de un tipo.</summary>
    public class VecinoLicenciaVisitaCreateDto
    {
        public DateOnly FechaVisita { get; set; }
        public string? Observacion { get; set; }
    }

    /// <summary>Un recordatorio (N días antes de vencer) de una licencia.</summary>
    public class VecinoLicenciaRecordatorioDto
    {
        public int VecinoLicenciaControlRecordatorioId { get; set; }
        public int DiasAntes { get; set; }
        public DateOnly FechaRecordatorio { get; set; }
        public bool Enviado { get; set; }
    }

    /// <summary>Agrega un recordatorio adicional a la licencia vigente de un tipo.</summary>
    public class VecinoLicenciaRecordatorioCreateDto
    {
        public int DiasAntes { get; set; }
    }

    public class VecinoLicenciaPlantillaResponseDto
    {
        public List<VecinoLicenciaItemDto> Items { get; set; } = new();
        public List<CatalogOptionDto> Estados { get; set; } = new();
    }

    /// <summary>Datos del formulario al subir/reemplazar el documento de un tipo de licencia (el archivo va aparte).</summary>
    public class VecinoLicenciaUploadDto
    {
        public DateOnly FechaVencimiento { get; set; }
        /// <summary>Días de antelación de cada recordatorio a crear (ej. [30, 15, 7, 2]). Al menos uno.</summary>
        public List<int> DiasAntesRecordatorio { get; set; } = new();
    }

    /// <summary>Edita las fechas ampliadas del dashboard (inscripción/inicio/renovación) y el flag Mes Activo.</summary>
    public class VecinoLicenciaFechasUpdateDto
    {
        public DateOnly? FechaInscripcion { get; set; }
        /// <summary>NoSeCuenta/Pendiente/Indeterminado/NoRegistrada, o null si hay fecha real o está en blanco.</summary>
        public string? FechaInscripcionEstado { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public string? FechaInicioEstado { get; set; }
        /// <summary>Estado de la fecha de vencimiento cuando no hay fecha real (la fecha real se edita al subir el documento).</summary>
        public string? FechaVencimientoEstado { get; set; }
        public DateOnly? FechaRenovacion { get; set; }
        public string? FechaRenovacionEstado { get; set; }
        public bool MesActivo { get; set; } = true;
    }

    /// <summary>Valores válidos para *Estado de las 4 fechas del dashboard, cuando no hay una fecha real.</summary>
    public static class VecinoLicenciaFechaEstado
    {
        public const string NoSeCuenta = "NoSeCuenta";
        public const string Pendiente = "Pendiente";
        public const string Indeterminado = "Indeterminado";
        public const string NoRegistrada = "NoRegistrada";

        public static readonly HashSet<string> Validos = new() { NoSeCuenta, Pendiente, Indeterminado, NoRegistrada };
    }

    /// <summary>Una fila del dashboard gerencial: un tipo de licencia de un proyecto, con su semáforo de criticidad.</summary>
    public class VecinoLicenciaDashboardItemDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        /// <summary>Razón social y RUC del proyecto (vía Project.ContributorId), para el encabezado del PDF del comité.</summary>
        public string? RazonSocial { get; set; }
        public string? Ruc { get; set; }
        public string TipoDescripcion { get; set; } = null!;
        public string EstadoDescripcion { get; set; } = null!;
        public DateOnly? FechaInscripcion { get; set; }
        public string? FechaInscripcionEstado { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public string? FechaInicioEstado { get; set; }
        public DateOnly? FechaVencimiento { get; set; }
        public string? FechaVencimientoEstado { get; set; }
        public DateOnly? FechaRenovacion { get; set; }
        public string? FechaRenovacionEstado { get; set; }
        public bool MesActivo { get; set; }
        /// <summary>Negativo si ya venció. Null si no aplica o no tiene fecha de vencimiento.</summary>
        public int? DiasParaVencer { get; set; }
        /// <summary>rojo (&lt;30d o vencido) / amarillo (31-60d) / verde (&gt;60d) / gris (no aplica o sin fecha).</summary>
        public string Semaforo { get; set; } = null!;
    }

    public class VecinoLicenciaDashboardResumenDto
    {
        public int Documentos { get; set; }
        public int Activos { get; set; }
        public int Pendientes { get; set; }
        public int NoAplica { get; set; }
        public int NoTiene { get; set; }
    }

    public class VecinoLicenciaDashboardResponseDto
    {
        public List<VecinoLicenciaDashboardItemDto> Items { get; set; } = new();
        public VecinoLicenciaDashboardResumenDto Resumen { get; set; } = new();
    }

    public class VecinoLicenciaNoAplicaDto
    {
        public bool NoAplica { get; set; }
    }

    /// <summary>Agrega un tipo de licencia propio para un proyecto (no afecta la plantilla base ni a otros proyectos).</summary>
    public class VecinoLicenciaTipoCreateDto
    {
        public string Descripcion { get; set; } = null!;
        public int? DiasAntesDefault { get; set; }
    }

    /// <summary>Una versión anterior archivada de una licencia.</summary>
    public class VecinoLicenciaHistorialItemDto
    {
        public int VecinoLicenciaControlHistorialId { get; set; }
        public string ArchivoUrl { get; set; } = null!;
        public string? OriginalFileName { get; set; }
        public DateOnly? FechaVencimiento { get; set; }
        public DateOnly? FechaRecordatorio { get; set; }
        public int? DiasAntes { get; set; }
        public string? Motivo { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string? CreatedUserName { get; set; }
    }

    /// <summary>Correo resuelto automáticamente desde la ficha del proyecto (Residente, Coordinador SSOMA, Administración) — mismo criterio que EMOs.</summary>
    public class VecinoLicenciaDestinatarioAutomaticoDto
    {
        public string Rol { get; set; } = null!;
        public string? Email { get; set; }
    }

    /// <summary>Correo adicional configurado a mano para un proyecto (ej. Jefe SSOMA cuando aplique).</summary>
    public class VecinoLicenciaDestinatarioDto
    {
        public int VecinoLicenciaControlDestinatarioId { get; set; }
        public string Rol { get; set; } = null!;
        public string Email { get; set; } = null!;
    }

    public class VecinoLicenciaDestinatarioUpsertDto
    {
        public string Rol { get; set; } = null!;
        public string Email { get; set; } = null!;
    }

    /// <summary>Destinatarios de un proyecto: automáticos (ficha del proyecto) + adicionales configurados a mano.</summary>
    public class VecinoLicenciaDestinatariosResponseDto
    {
        public List<VecinoLicenciaDestinatarioAutomaticoDto> Automaticos { get; set; } = new();
        public List<VecinoLicenciaDestinatarioDto> Adicionales { get; set; } = new();
    }

    /// <summary>Un recordatorio pendiente de envío, con el contexto necesario para armar el correo.</summary>
    public class VecinoLicenciaRecordatorioPendienteDto
    {
        public int VecinoLicenciaControlRecordatorioId { get; set; }
        public int ProjectId { get; set; }
        public string TipoDescripcion { get; set; } = null!;
        public DateOnly FechaVencimiento { get; set; }
        public int DiasAntes { get; set; }
    }

    /// <summary>Una visita de Anexo H con recordatorio pendiente de envío, con el contexto necesario para el correo.</summary>
    public class VecinoLicenciaVisitaPendienteDto
    {
        public int VecinoLicenciaControlVisitaId { get; set; }
        public int ProjectId { get; set; }
        public string TipoDescripcion { get; set; } = null!;
        public DateOnly FechaVisita { get; set; }
    }

    /// <summary>Resultado del procesamiento del cron de recordatorios (todos los proyectos).</summary>
    public class RecordatoriosResultDto
    {
        public int LicenciasProcesadas { get; set; }
        public int CorreosEnviados { get; set; }
        public List<string> Errores { get; set; } = new();
    }
}
