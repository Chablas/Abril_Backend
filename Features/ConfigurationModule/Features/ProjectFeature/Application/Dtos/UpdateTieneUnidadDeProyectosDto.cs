namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    /// <summary>
    /// Body del PATCH que activa/desactiva la pertenencia de un proyecto al módulo Unidad
    /// de Proyectos, sin pasar por el PUT completo de edición (que exige reconstruir todo
    /// el <see cref="ProjectEditDto"/> y arriesga sobreescribir campos no enviados).
    /// </summary>
    public class UpdateTieneUnidadDeProyectosDto
    {
        public bool Value { get; set; }
    }
}
