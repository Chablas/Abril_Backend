namespace Abril_Backend.Features.GestionAdministrativa.VisibilidadSalidas.Application.Dtos
{
    /// <summary>Una fila por trabajador con correo @abril.pe en la lista de configuración.</summary>
    public class VisibilidadWorkerItemDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public int? CategoryId { get; set; }
        public string? Category { get; set; }
        /// <summary>Nodo area_scope al que pertenece el trabajador (para filtrar por área). null = sin área.</summary>
        public int? AreaScopeId { get; set; }
        /// <summary>Cuántos nodos area_scope tiene asignados (override). 0 = usa el algoritmo automático.</summary>
        public int AreasAsignadas { get; set; }
    }

    /// <summary>Carga inicial de la página: trabajadores (tabla) + árbol de áreas (filtro en cascada).</summary>
    public class VisibilidadInicialDto
    {
        public List<VisibilidadWorkerItemDto> Workers { get; set; } = new();
        public List<VisibilidadAreaNodeDto> AreaTree { get; set; } = new();
    }

    /// <summary>Nodo del árbol area_scope para el modal (lista plana; el frontend arma la jerarquía).</summary>
    public class VisibilidadAreaNodeDto
    {
        public int AreaScopeId { get; set; }
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public int? AreaScopeParentId { get; set; }
        public int DisplayOrder { get; set; }
    }

    /// <summary>Una asignación de visibilidad: un nodo + si incluye sus descendientes.</summary>
    public class VisibilidadAsignacionDto
    {
        public int AreaScopeId { get; set; }
        public bool IncluyeDescendientes { get; set; }
    }

    /// <summary>Cuerpo del PUT: reemplaza el conjunto completo de asignaciones del trabajador.</summary>
    public class VisibilidadUpdateDto
    {
        public List<VisibilidadAsignacionDto> Areas { get; set; } = new();
    }
}
