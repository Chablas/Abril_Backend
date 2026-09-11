namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos
{
    /// <summary>
    /// Un nodo del árbol de áreas (<c>area_scope</c>) tal como lo necesita el formulario de
    /// trabajadores: lo del árbol para armar los desplegables en cascada, la equivalencia legacy
    /// que se guardará si se elige el nodo, y los revisores que le tocarían al trabajador.
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

        /// <summary>
        /// El revisor que le tocaría a un trabajador de este nodo sin considerar su proyecto, YA
        /// ELEGIDO por el backend (<c>IJefeRevisorResolver</c>): el formulario lo muestra tal cual,
        /// no elige entre candidatos. Null si la rama no tiene ninguno ni llega al fallback de GTH.
        /// </summary>
        public AreaArbolRevisorDto? Revisor { get; set; }

        /// <summary>
        /// True cuando el primer candidato de la rama era el propio trabajador y por eso
        /// <see cref="Revisor"/> es el siguiente. Es lo normal en los jefes de área, que son el
        /// revisor de su propia área; el formulario lo avisa para que no se lea como un error de
        /// configuración. Solo viene con valor cuando el árbol se pidió para un trabajador
        /// concreto (<c>?workerId=</c>).
        /// </summary>
        public bool EsRevisorDeSuPropiaArea { get; set; }

        /// <summary>
        /// El revisor por proyecto, solo para las áreas configuradas como "filtrar por proyecto".
        /// Si el proyecto del trabajador está acá, esta entrada manda sobre <see cref="Revisor"/>;
        /// el formulario solo indexa por su proyecto, sin aplicar ninguna regla.
        /// </summary>
        public List<AreaArbolRevisorProyectoDto> RevisorPorProyecto { get; set; } = new();
    }

    /// <summary>
    /// Un revisor. <see cref="WorkerId"/> y <see cref="PersonId"/> van en null cuando el revisor es
    /// el área de GTH (el fallback), que es un correo de área y no una persona.
    /// </summary>
    public class AreaArbolRevisorDto
    {
        public int? WorkerId { get; set; }
        public int? PersonId { get; set; }
        public string? Nombre { get; set; }
        public string? Email { get; set; }
    }

    public class AreaArbolRevisorProyectoDto
    {
        public int ProyectoId { get; set; }
        public AreaArbolRevisorDto? Revisor { get; set; }
        public bool EsRevisorDeSuPropiaArea { get; set; }
    }
}
