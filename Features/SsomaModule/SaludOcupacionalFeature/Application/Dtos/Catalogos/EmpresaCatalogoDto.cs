namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Catalogos
{
    /// <summary>
    /// DTO de razón social (contribuyente) usado por el catálogo de empresas en SSOMA.
    /// Los datos se leen desde la tabla <c>contributor</c>.
    ///
    /// La pantalla Configuración → Razones Sociales ya NO lo usa: tiene su propio DTO en
    /// <c>Features/ConfigurationModule/Features/RazonSocialFeature</c>, que además trae el banco.
    /// </summary>
    public class EmpresaCatalogoDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Ruc { get; set; }
        public string? Direccion { get; set; }
        public string? PartidaRegistral { get; set; }
        public string? TipoActividad { get; set; }
        public bool? Activo { get; set; }
        public bool EsAbril { get; set; }
    }
}
