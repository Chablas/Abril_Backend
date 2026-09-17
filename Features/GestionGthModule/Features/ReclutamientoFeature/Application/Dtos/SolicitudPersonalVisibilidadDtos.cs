namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    // Solicitud de Personal → Configuración → Visibilidad. El contrato es el mismo que el de la
    // Visibilidad de Gestión Administrativa porque las dos pantallas usan la misma sección
    // compartida del frontend (shared/components/visibilidad-areas).

    /// <summary>Una fila por trabajador activo con correo @abril.pe.</summary>
    public class SolicitudPersonalVisibilidadWorkerDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public int? CategoryId { get; set; }
        public string? Category { get; set; }

        /// <summary>Área del puesto (para el filtro por área). null = sin área.</summary>
        public int? AreaScopeId { get; set; }

        /// <summary>Áreas de su configuración propia. 0 = lo resuelve el algoritmo.</summary>
        public int AreasAsignadas { get; set; }
    }

    /// <summary>Nodo del árbol <c>area_scope</c>, en lista plana: el frontend arma la jerarquía.</summary>
    public class SolicitudPersonalVisibilidadAreaNodoDto
    {
        public int AreaScopeId { get; set; }
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public int? AreaScopeParentId { get; set; }
        public int DisplayOrder { get; set; }
    }

    /// <summary>Carga de la sección: trabajadores (tabla) + árbol de áreas (filtro y modales).</summary>
    public class SolicitudPersonalVisibilidadInicialDto
    {
        public List<SolicitudPersonalVisibilidadWorkerDto> Workers { get; set; } = new();
        public List<SolicitudPersonalVisibilidadAreaNodoDto> AreaTree { get; set; } = new();
    }

    /// <summary>Un área de la configuración propia.</summary>
    public class SolicitudPersonalVisibilidadAsignacionDto
    {
        public int AreaScopeId { get; set; }
    }

    /// <summary>
    /// Lo que los dos modales de un trabajador necesitan, en una sola llamada: lo que tiene
    /// configurado y lo que realmente ve hoy. El de detalle muestra <see cref="Efectivas"/> y el de
    /// edición parte de <see cref="Asignaciones"/> si las hay y, si no, de <see cref="Efectivas"/>.
    /// </summary>
    public class SolicitudPersonalVisibilidadDetalleDto
    {
        /// <summary>Configuración propia. Vacío = lo resuelve el algoritmo.</summary>
        public List<SolicitudPersonalVisibilidadAsignacionDto> Asignaciones { get; set; } = new();

        /// <summary>Nodos que ve hoy; con <see cref="VeTodo"/>, el árbol entero.</summary>
        public List<int> Efectivas { get; set; } = new();

        /// <summary>true = <see cref="Efectivas"/> sale de su configuración propia.</summary>
        public bool EsPersonalizado { get; set; }

        /// <summary>true = no se le filtra por área.</summary>
        public bool VeTodo { get; set; }
    }

    /// <summary>Cuerpo del PUT: reemplaza la configuración propia completa del trabajador.</summary>
    public class SolicitudPersonalVisibilidadUpdateDto
    {
        public List<SolicitudPersonalVisibilidadAsignacionDto> Areas { get; set; } = new();
    }
}
