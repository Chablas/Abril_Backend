namespace Abril_Backend.Shared.Services.Residentes.Interfaces
{
    /// <summary>
    /// «¿Qué proyecto maneja este residente?» con una sola respuesta en todo el sistema: el
    /// Residente del proyecto en Configuración → Proyectos (<c>project.residente_workers_id</c>).
    /// No la tabla antigua <c>project_resident</c>, ni <c>user_project</c>, ni el puesto.
    ///
    /// Se cruza siempre por persona, la de la ficha que apunta el proyecto (y con la ficha viva):
    /// una persona puede tener varias fichas por reingreso y el proyecto guarda una sola. Donde el
    /// residente gana permisos, quien pregunta exige además el rol RESIDENTE: el rol dice «es
    /// residente» y esto dice «de qué obra».
    ///
    /// Para meter la regla dentro de otra consulta (mismo contexto, sin N+1) están las consultas de
    /// <c>ResidenteQueries</c>, que son su única definición. Ver PLAN-RESIDENTES.md §3.
    /// </summary>
    public interface IResidenteProyectoResolver
    {
        /// <summary>Proyectos cuyo residente es la persona del usuario. No filtra el proyecto por
        /// estado: eso lo decide quien pregunta.</summary>
        Task<List<int>> ProyectosDelResidenteAsync(int userId);

        /// <summary>true si el usuario es el residente del proyecto.</summary>
        Task<bool> EsResidenteDelProyectoAsync(int userId, int projectId);

        /// <summary>El residente del proyecto, o null si no tiene o si su ficha está de baja.</summary>
        Task<ResidenteDto?> ResidenteDelProyectoAsync(int projectId);

        /// <summary>El universo de obras con residente (visible, tipo que es obra, ciclo ACTIVO y
        /// residente con usuario y rol RESIDENTE). Ver <c>ResidenteQueries.ObrasConResidente</c>.</summary>
        Task<List<int>> ObrasConResidenteAsync();
    }

    /// <summary>El residente de un proyecto: su ficha, su persona, su usuario (si tiene), su nombre y
    /// su correo corporativo, que es el de los avisos.</summary>
    public record ResidenteDto(int WorkerId, int? PersonId, int? UserId, string? Nombre, string? Email);

    /// <summary>Una obra del universo de obras con residente y su residente: la ficha que apunta el
    /// proyecto, su persona y su usuario. Clase con setters y no record posicional para que EF pueda
    /// seguir componiendo la consulta encima (Where/Select sobre sus propiedades).</summary>
    public class ObraConResidente
    {
        public int ProjectId { get; set; }
        public int WorkerId { get; set; }
        public int PersonId { get; set; }
        public int UserId { get; set; }
    }
}
