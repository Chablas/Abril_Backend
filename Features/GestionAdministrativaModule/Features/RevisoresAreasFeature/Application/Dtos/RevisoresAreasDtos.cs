namespace Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Dtos
{
    /// <summary>
    /// Carga inicial de Gestión Administrativa → Configuración → Revisores de Áreas: una fila por
    /// área configurable (y, en las que tienen gente en varias ubicaciones, una subfila por obra) con
    /// los cinco actores que le tocan a un TRABAJADOR NORMAL de esa fila. El resto de los casos (staff,
    /// jefes, residentes, subgerentes) se ve en el detalle de cada fila.
    /// </summary>
    public class RevisoresAreasInicialDto
    {
        /// <summary>Los cinco actores, en el orden de las columnas.</summary>
        public List<CatalogoActorDto> Actores { get; set; } = new();

        /// <summary>Los cinco casos, en el orden en que los muestra el detalle.</summary>
        public List<CatalogoActorDto> Casos { get; set; } = new();

        public List<RevisoresAreaFilaDto> Areas { get; set; } = new();

        /// <summary>Personas elegibles (correo corporativo). Solo para quien puede editar.</summary>
        public List<PersonaOpcionDto> Options { get; set; } = new();

        /// <summary>true = el usuario puede editar (ADMINISTRADOR DE SOLICITUD DE SALIDAS o USUARIO DE GTH).</summary>
        public bool PuedeEditar { get; set; }
    }

    /// <summary>Una fila de los catálogos ga_actor / ga_actor_caso.</summary>
    public class CatalogoActorDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Solo en actores: true = cuentan todos los de la lista.</summary>
        public bool Multiple { get; set; }
    }

    /// <summary>Un área configurable (gerencia o primera área estándar de su rama).</summary>
    public class RevisoresAreaFilaDto
    {
        public int AreaScopeId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        /// <summary>"Área de Gerencia" / "Área Estándar".</summary>
        public string AreaTypeName { get; set; } = string.Empty;
        public string? ParentName { get; set; }
        public bool EsGerencia { get; set; }

        /// <summary>
        /// Deducido, ya no es una casilla: true cuando la gente del área trabaja en más de una
        /// ubicación (o en alguna obra), o cuando hay algo personalizado para una obra. Entonces el
        /// área se muestra partida por obra (<see cref="Proyectos"/>). Las gerencias nunca: ahí todo
        /// lo resuelve su gerente.
        /// </summary>
        public bool FiltraPorProyecto { get; set; }

        /// <summary>El caso de las columnas de esta fila: trabajador de oficina central.</summary>
        public int CasoId { get; set; }

        /// <summary>Los cinco actores de un trabajador normal de la fila.</summary>
        public List<ActorCeldaDto> Actores { get; set; } = new();

        /// <summary>Las subfilas por obra, solo con <see cref="FiltraPorProyecto"/>.</summary>
        public List<RevisoresAreaProyectoDto> Proyectos { get; set; } = new();
    }

    /// <summary>Una obra (o OFICINA CENTRAL) dentro de un área partida por obra.</summary>
    public class RevisoresAreaProyectoDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>false = OFICINA CENTRAL.</summary>
        public bool EsObra { get; set; }

        /// <summary>El caso de las columnas de esta subfila: staff en una obra, oficina central si no.</summary>
        public int CasoId { get; set; }

        public List<ActorCeldaDto> Actores { get; set; } = new();
    }

    /// <summary>El valor que HOY tiene un actor para un caso de una fila.</summary>
    public class ActorCeldaDto
    {
        public int ActorId { get; set; }

        /// <summary>false = el actor no existe para este caso (el jefe notificado fuera del staff).</summary>
        public bool Aplica { get; set; } = true;

        /// <summary>Quiénes son, en orden.</summary>
        public List<ActorPersonaDto> Personas { get; set; } = new();

        /// <summary>
        /// En una fila sin obra, lo que depende de la obra de cada trabajador se describe en vez de
        /// nombrarse ("Residente de la obra").
        /// </summary>
        public string? Descriptor { get; set; }

        /// <summary>
        /// "Personalizado" (se asignó en esta fila), "PersonalizadoArea" (una subfila que hereda lo
        /// asignado a su área), "Algoritmo" (lo dedujo el sistema, o lo asignado en otra área que le
        /// llega subiendo por el árbol) o "Gth" (último recurso).
        /// </summary>
        public string Origen { get; set; } = "Algoritmo";
    }

    public class ActorPersonaDto
    {
        /// <summary>Null en el último recurso (GTH), que es un buzón de área.</summary>
        public int? WorkerId { get; set; }
        public string? Nombre { get; set; }
        public string? Email { get; set; }
        public string? Categoria { get; set; }
    }

    /// <summary>Opción del selector de personas.</summary>
    public class PersonaOpcionDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>
    /// El detalle de una fila (un área, o una obra dentro de ella): los cinco actores de cada caso
    /// que aplica a esa fila, y lo personalizado en ella para poder editarlo.
    /// </summary>
    public class RevisoresAreaDetalleDto
    {
        public int AreaScopeId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public bool EsGerencia { get; set; }

        /// <summary>Null = la fila del área; con valor = la subfila de esa obra.</summary>
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public bool EsObra { get; set; }

        public List<RevisoresAreaCasoDto> Casos { get; set; } = new();
    }

    public class RevisoresAreaCasoDto
    {
        public int CasoId { get; set; }
        public string CasoNombre { get; set; } = string.Empty;
        public List<ActorCeldaDetalleDto> Actores { get; set; } = new();
    }

    /// <summary>Una celda del detalle: el valor de hoy y lo personalizado exactamente en esta fila.</summary>
    public class ActorCeldaDetalleDto : ActorCeldaDto
    {
        /// <summary>false = no se puede personalizar (el actor no aplica a este caso).</summary>
        public bool Editable { get; set; }

        /// <summary>
        /// Lo personalizado en ESTA fila para este caso y actor, en orden y con su estado. Vacía = la
        /// celda no está personalizada acá.
        /// </summary>
        public List<AsignadoDto> Asignados { get; set; } = new();

        /// <summary>
        /// Con la celda personalizada en esta fila: lo que quedaría sin eso (el algoritmo, o lo que
        /// herede de su área). Null si la celda no está personalizada acá.
        /// </summary>
        public ActorCeldaDto? SinPersonalizar { get; set; }
    }

    public class AsignadoDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Categoria { get; set; }
        /// <summary>1 = primero.</summary>
        public int OrdenPrioridad { get; set; }
        /// <summary>false = no se considera (ausencia temporal; lo usa también Delegación de Revisión).</summary>
        public bool Active { get; set; }
    }

    /// <summary>
    /// Guardar una fila entera de una vez (el único botón del modal): por cada celda que el modal
    /// muestra, la lista completa de lo personalizado. Lista vacía = la celda deja de estar
    /// personalizada y vuelve a mandar el algoritmo (o lo que herede).
    /// </summary>
    public class RevisoresAreaGuardarDto
    {
        /// <summary>Null = la fila del área; con valor = la subfila de esa obra.</summary>
        public int? ProjectId { get; set; }

        public List<CeldaGuardarDto> Celdas { get; set; } = new();
    }

    public class CeldaGuardarDto
    {
        public int CasoId { get; set; }
        public int ActorId { get; set; }

        /// <summary>En orden: la posición es la prioridad.</summary>
        public List<AsignadoInputDto> Asignados { get; set; } = new();
    }

    public class AsignadoInputDto
    {
        public int WorkerId { get; set; }
        public bool Active { get; set; } = true;
    }
}
