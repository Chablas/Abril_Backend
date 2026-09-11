namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>
    /// Forma común de las dos pantallas que asignan PERSONAS A ÁREAS con el mismo algoritmo:
    /// Revisores de Áreas (quién aprueba las salidas de un área) y Consolidadores de Áreas (quién
    /// puede adjuntar el Consolidado del S10 por los trabajadores de un área).
    ///
    /// Son la misma tabla, el mismo árbol, el mismo "filtrar por proyecto" y el mismo modal de
    /// edición; lo único que cambia es cuántos de los asignados quedan vigentes
    /// (<see cref="AreaAsignacionItemDto.Efectivos"/>): en revisores gana uno, en consolidadores
    /// quedan todos. Comparten DTO para que el frontend las monte con un solo componente y no
    /// puedan desfasarse.
    /// </summary>
    public class AreaAsignacionItemDto
    {
        public int AreaScopeId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        /// <summary>Tipo del nodo: "Área de Gerencia" o "Área Estándar".</summary>
        public string AreaTypeName { get; set; } = string.Empty;
        /// <summary>Nombre del área padre (normalmente la gerencia). null = nodo raíz.</summary>
        public string? ParentName { get; set; }

        /// <summary>Lo asignado a mano a nivel de área (project_id NULL), por prioridad.</summary>
        public List<AreaAsignadoDto> Asignados { get; set; } = new();

        /// <summary>
        /// Quién queda efectivamente a cargo hoy de un trabajador de esta área, resuelto por el
        /// algoritmo: lo asignado acá si hay algo y, si no, lo que deduce del árbol (el Jefe del
        /// área, el Gerente de la gerencia, el residente de la obra). Una entrada en Revisores;
        /// puede tener varias en Consolidadores.
        /// </summary>
        public List<AreaEfectivoDto> Efectivos { get; set; } = new();

        /// <summary>
        /// Si true, el área se "subdivide por proyecto" (ga_salidas_area_config.filtra_por_proyecto):
        /// las asignaciones se hacen por proyecto y se muestran subfilas por proyecto.
        /// </summary>
        public bool FiltraPorProyecto { get; set; }

        /// <summary>
        /// Solo cuando <see cref="FiltraPorProyecto"/> es true: TODOS los proyectos activos, cada
        /// uno con lo suyo asignado (vacío si no se asignó nada) y con sus efectivos resueltos. Van
        /// todos y no solo los asignados porque desde que existe el algoritmo un proyecto sin nada
        /// cargado igual resuelve: su residente.
        /// </summary>
        public List<AreaProyectoAsignacionesDto> Proyectos { get; set; } = new();
    }

    /// <summary>Asignaciones de un proyecto dentro de un área "filtrada por proyecto".</summary>
    public class AreaProyectoAsignacionesDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public List<AreaAsignadoDto> Asignados { get; set; } = new();
        public List<AreaEfectivoDto> Efectivos { get; set; } = new();
    }

    /// <summary>Una persona asignada a mano (fila viva de area_revisores / area_consolidadores).</summary>
    public class AreaAsignadoDto
    {
        /// <summary>Id de la fila (area_revisores_id / area_consolidadores_id).</summary>
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Category { get; set; }
        /// <summary>1 = primero. En revisores decide quién gana; en consolidadores solo ordena.</summary>
        public int OrdenPrioridad { get; set; }
        /// <summary>false = no se considera (ej. ausencia temporal).</summary>
        public bool Active { get; set; }
    }

    /// <summary>Una persona vigente para el área/proyecto, con de dónde salió.</summary>
    public class AreaEfectivoDto
    {
        /// <summary>Ficha, para que el filtro por persona de la pantalla la encuentre. Null en el fallback GTH.</summary>
        public int? WorkerId { get; set; }
        public string? Nombre { get; set; }
        /// <summary>
        /// "Personalizado" (alguien la asignó en ESTA área), "Algoritmo" (la dedujo el sistema o
        /// subió por el árbol hasta otra área) o "Gth" (último recurso, solo en revisores).
        /// </summary>
        public string? Origen { get; set; }
    }

    /// <summary>Opción del selector: worker con correo corporativo @abril.pe.</summary>
    public class AreaWorkerOptionDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>Opción/subfila de proyecto.</summary>
    public class AreaProyectoOptionDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
    }

    /// <summary>Carga inicial de la pantalla: áreas configurables + opciones del selector.</summary>
    public class AreaAsignacionInicialDto
    {
        public List<AreaAsignacionItemDto> Areas { get; set; } = new();
        public List<AreaWorkerOptionDto> Options { get; set; } = new();
    }

    /// <summary>
    /// Cuerpo del PUT: reemplaza el conjunto completo de asignaciones del área (si
    /// <see cref="ProjectId"/> es null) o del proyecto dentro del área (si tiene valor).
    /// </summary>
    public class AreaAsignacionUpdateDto
    {
        public int? ProjectId { get; set; }
        public List<AreaAsignacionInputDto> Asignados { get; set; } = new();
    }

    /// <summary>Una asignación dentro del PUT.</summary>
    public class AreaAsignacionInputDto
    {
        public int WorkerId { get; set; }
        public int OrdenPrioridad { get; set; }
        public bool Active { get; set; } = true;
    }

    /// <summary>Cuerpo del PUT de flag: marca/desmarca "filtrar por proyecto" para el área.</summary>
    public class AreaFiltroProyectoUpdateDto
    {
        public bool FiltraPorProyecto { get; set; }
    }
}
