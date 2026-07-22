namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Datos de la vista de GTH ("Reclutamiento"): tarjetas de resumen + tabla de solicitudes de
    /// contratación de toda la organización, servidos en una sola petición. Por ahora solo trae la
    /// tarjeta "En proceso" y la tabla; las demás tarjetas y acciones se agregarán después.
    /// </summary>
    public class BandejaReclutamientoDto
    {
        public ResumenReclutamientoDto Resumen { get; set; } = new();

        /// <summary>Filas de la tabla "Solicitudes de contratación" (un requerimiento por fila), más recientes primero.</summary>
        public List<RequerimientoGthListItemDto> Solicitudes { get; set; } = new();

        /// <summary>Catálogo de prioridades (Alta/Media/Baja) para el desplegable de la columna "Prioridad".</summary>
        public List<OpcionDto> Prioridades { get; set; } = new();
    }

    /// <summary>Contadores de las tarjetas de resumen de la vista de GTH.</summary>
    public class ResumenReclutamientoDto
    {
        /// <summary>Requerimientos activos actualmente (en curso dentro del pipeline).</summary>
        public int EnProceso { get; set; }
    }

    /// <summary>
    /// Fila de la tabla "Solicitudes de contratación" de la vista de GTH: un requerimiento (vacante)
    /// de cualquier área, con los datos que muestra la tabla del Figma que ya tienen soporte de modelo.
    /// (La columna "Prioridad" queda pendiente hasta modelarla en BD.)
    /// </summary>
    public class RequerimientoGthListItemDto
    {
        public int RequerimientoId { get; set; }

        /// <summary>Código REQ-AAAA-NNNN (columna "N° requerimiento").</summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Área solicitante (snapshot al registrar).</summary>
        public string? Area { get; set; }

        /// <summary>Puesto solicitado.</summary>
        public string Puesto { get; set; } = string.Empty;

        /// <summary>Proyecto/obra destino de la vacante.</summary>
        public string? ProyectoObra { get; set; }

        /// <summary>Fecha en que llegó la solicitud (created) en hora Perú (UTC-5). Columna "Fecha llegada".</summary>
        public DateTime FechaLlegada { get; set; }

        /// <summary>Fecha requerida de ingreso (solo fecha). Columna "Fecha requerida".</summary>
        public DateOnly FechaRequeridaIngreso { get; set; }

        /// <summary>Prioridad asignada (id del catálogo gth_prioridad). Null si no tiene. Columna "Prioridad".</summary>
        public int? PrioridadId { get; set; }

        /// <summary>Nombre de la prioridad (Alta/Media/Baja). Null si no tiene.</summary>
        public string? PrioridadNombre { get; set; }

        /// <summary>Código estable del estado (p.ej. NUEVO) — para lógica de front.</summary>
        public string EstadoCodigo { get; set; } = string.Empty;

        /// <summary>Nombre legible del estado (para el badge).</summary>
        public string EstadoNombre { get; set; } = string.Empty;
    }

    /// <summary>Body del PATCH que actualiza la prioridad de un requerimiento.</summary>
    public class UpdatePrioridadDto
    {
        public int PrioridadId { get; set; }
    }
}
