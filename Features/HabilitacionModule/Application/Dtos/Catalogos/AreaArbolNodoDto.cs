namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos
{
    /// <summary>
    /// Un nodo del árbol de áreas (<c>area_scope</c>) tal como lo necesita el formulario de
    /// trabajadores: lo del árbol para armar los desplegables en cascada, la equivalencia legacy
    /// que se guardará si se elige el nodo, y el revisor que le tocaría al trabajador.
    ///
    /// Todo viene resuelto por el backend para que el formulario no tenga que replicar ninguna
    /// regla ni pedir nada más al cambiar de área.
    /// </summary>
    public class AreaArbolNodoDto
    {
        public int AreaScopeId { get; set; }
        public int? AreaScopeParentId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        /// <summary>"Área de Gerencia" / "Área Estándar".</summary>
        public string AreaTypeName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        /// <summary>Equivalencia legacy que quedará en workers.area / .subarea / .jefatura.</summary>
        public string? Area { get; set; }
        public string? Subarea { get; set; }
        public string? Jefatura { get; set; }

        /// <summary>Revisor que le tocaría a un trabajador de este nodo sin considerar su proyecto.</summary>
        public string? RevisorNombre { get; set; }
        public string? RevisorEmail { get; set; }

        /// <summary>
        /// Revisor por proyecto, solo para las áreas configuradas como "filtrar por proyecto".
        /// Si el proyecto elegido en el formulario está acá, este revisor manda sobre el de arriba.
        /// </summary>
        public List<AreaArbolRevisorProyectoDto> RevisoresPorProyecto { get; set; } = new();
    }

    public class AreaArbolRevisorProyectoDto
    {
        public int ProyectoId { get; set; }
        public string? RevisorNombre { get; set; }
        public string? RevisorEmail { get; set; }
    }
}
