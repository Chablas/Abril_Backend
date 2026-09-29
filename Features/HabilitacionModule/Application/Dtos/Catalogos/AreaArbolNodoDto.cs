namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos
{
    /// <summary>
    /// Un nodo del árbol de áreas (<c>area_scope</c>) tal como lo necesitan los formularios y
    /// filtros: lo del árbol para armar los desplegables en cascada y la equivalencia legacy que se
    /// guardará si se elige el nodo.
    ///
    /// Hasta el 2026-09-25 traía además el revisor precalculado de cada nodo para la ficha del
    /// trabajador; ahora la ficha pide los cinco actores de ESE trabajador
    /// (<c>GET catalogos/actores</c>), así que el árbol ya no paga esa resolución para todos los nodos.
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
    }
}
