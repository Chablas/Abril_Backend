namespace Abril_Backend.Features.ConvivirModule.Shared.Dtos
{
    /// <summary>Una propiedad del propietario que entró a la app (fila de <c>propietario</c>).</summary>
    public class ConvivirPropiedadDto
    {
        public int PropietarioId { get; set; }
        public int ProjectId { get; set; }
        public string Proyecto { get; set; } = string.Empty;
        public string? Torre { get; set; }
        public string Departamento { get; set; } = string.Empty;
    }

    public static class ConvivirHitoEstado
    {
        /// <summary>El hito del cronograma tiene fecha real (lo culminaron en el Cronograma de Hitos).</summary>
        public const string Cumplido = "CUMPLIDO";
        /// <summary>El primero sin cumplir después del último cumplido.</summary>
        public const string Proximo = "PROXIMO";
        public const string Pendiente = "PENDIENTE";
    }

    /// <summary>Un hito para propietarios (<c>owner_milestone</c>) con las fechas del cronograma vigente.</summary>
    public class ConvivirHitoDto
    {
        public int Orden { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        /// <summary>Inicio del hito en el cronograma, o su única fecha.</summary>
        public DateOnly? FechaEstimada { get; set; }
        /// <summary>Fecha en que lo culminaron; null si todavía no.</summary>
        public DateOnly? FechaReal { get; set; }
        /// <summary>Ver <see cref="ConvivirHitoEstado"/>.</summary>
        public string Estado { get; set; } = ConvivirHitoEstado.Pendiente;
    }

    /// <summary>Avance de obra de un proyecto, en el lenguaje del propietario (por hitos, no porcentajes).</summary>
    public class ConvivirAvanceDto
    {
        /// <summary>false si el proyecto todavía no tiene ninguna versión del cronograma.</summary>
        public bool TieneCronograma { get; set; }
        /// <summary>El último hito cumplido.</summary>
        public ConvivirHitoDto? HitoActual { get; set; }
        public ConvivirHitoDto? ProximoHito { get; set; }
        /// <summary>
        /// Fecha del último hito para propietarios (Edificio concluido), real si ya se cumplió; si el
        /// cronograma no lo tiene, el fin de obra de Configuración → Proyectos.
        /// </summary>
        public DateOnly? EntregaEstimada { get; set; }
    }

    /// <summary>
    /// Lo que la base devuelve de un propietario en un solo viaje: su nombre, sus propiedades y los
    /// hitos del proyecto de la propiedad elegida.
    /// </summary>
    public class ConvivirContextoDto
    {
        public string? Nombres { get; set; }
        public List<ConvivirPropiedadDto> Propiedades { get; set; } = new();
        /// <summary>La pedida si es suya; si no, la primera. Null si no tiene propiedades.</summary>
        public ConvivirPropiedadDto? Seleccionada { get; set; }
        public bool TieneCronograma { get; set; }
        public List<ConvivirHitoFila> Hitos { get; set; } = new();
        public DateTime? FinObraProyecto { get; set; }
        /// <summary>Documentos de la propiedad elegida que el propietario todavía no abrió.</summary>
        public int DocumentosNuevos { get; set; }
        /// <summary>
        /// Avisos de la campana sin leer, de todas sus propiedades. Solo si se pidió con
        /// notificaciones (Inicio).
        /// </summary>
        public int NotificacionesNuevas { get; set; }
    }

    /// <summary>
    /// Fila cruda: un owner_milestone con lo que tenga el cronograma vigente. Las fechas llegan como
    /// DateTime porque las lee Dapper (columnas <c>date</c>); el servicio las pasa a DateOnly.
    /// </summary>
    public class ConvivirHitoFila
    {
        public int Orden { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        /// <summary>La versión vigente tiene una fila para el hito interno al que apunta.</summary>
        public bool EnCronograma { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? FechaRealFin { get; set; }
    }
}
