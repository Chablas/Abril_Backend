namespace Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Dtos
{
    /// <summary>
    /// Una fila de Configuración → Capturas: un nodo vivo de <c>area_scope</c> con el flag que dice
    /// si sus trabajadores están obligados a subir capturas de movilidad para rendir.
    ///
    /// Se listan TODAS las áreas vivas y activas, sin colapsar ramas: si de "Gerencia de Proyectos"
    /// cuelga "Unidad de Proyectos" y de ella "Planeamiento BIM" e "Ingeniería BIM", son cuatro
    /// filas independientes. Por eso viene el nombre del padre: hay áreas con el mismo nombre en
    /// ramas distintas (dos "Producción", dos "Unidad de Proyectos") y el nombre solo no alcanza
    /// para distinguirlas.
    /// </summary>
    public class CapturaAreaItemDto
    {
        public int AreaScopeId { get; set; }
        /// <summary>Nombre del nodo (area_item_name), sin la ruta completa de la rama.</summary>
        public string AreaName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        /// <summary>Nombre del área padre. null = nodo raíz (una gerencia).</summary>
        public string? ParentName { get; set; }
        /// <summary>
        /// true = los trabajadores del área deben subir una captura por trayecto para poder rendir.
        /// Es el default: un área sin fila en <c>ga_salidas_area_config</c> llega acá en true.
        /// </summary>
        public bool CapturasObligatorias { get; set; } = true;
    }

    /// <summary>Opción del filtro "Tipo de área".</summary>
    public class CapturaAreaTipoOptionDto
    {
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Carga inicial de la sección: la tabla y las opciones de sus filtros en una sola petición.
    /// El filtro de área se arma en el frontend con <see cref="Areas"/> (ya viene completa), así
    /// que acá solo viajan los tipos.
    /// </summary>
    public class CapturaAreaInicialDto
    {
        public List<CapturaAreaItemDto> Areas { get; set; } = new();
        public List<CapturaAreaTipoOptionDto> Tipos { get; set; } = new();
    }

    /// <summary>Cuerpo del PUT: marca las capturas del área como obligatorias u opcionales.</summary>
    public class CapturaAreaUpdateDto
    {
        public bool CapturasObligatorias { get; set; }
    }
}
