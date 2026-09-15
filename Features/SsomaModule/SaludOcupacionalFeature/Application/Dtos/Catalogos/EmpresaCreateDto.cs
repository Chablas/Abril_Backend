namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Catalogos
{
    /// <summary>
    /// Alta de una razón social (contribuyente) desde el catálogo de SSOMA.
    ///
    /// El alta desde Configuración → Razones Sociales usa su propio DTO en
    /// <c>Features/ConfigurationModule/Features/RazonSocialFeature</c>: ahí sí se decide si la
    /// empresa es del grupo y con qué banco trabaja.
    /// </summary>
    public class EmpresaCreateDto
    {
        public string Ruc { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string Direccion { get; set; } = null!;
        public string TipoActividad { get; set; } = null!;
        public string Distrito { get; set; } = null!;
        public string Provincia { get; set; } = null!;
        public string Departamento { get; set; } = null!;
        /// <summary>Partida registral (opcional).</summary>
        public string? PartidaRegistral { get; set; }
    }
}
