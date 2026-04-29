namespace Abril_Backend.Features.Habilitacion.Application.Dtos.HabEmpresa
{
    public class EmpresaEntregableDto
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string NombreItem { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime? Vigencia { get; set; }
        public string? ArchivoUrl { get; set; }
        public string? ObsAbril { get; set; }
        public string? ObsContratista { get; set; }
        public bool RequiereVigencia { get; set; }
        public string Responsable { get; set; } = string.Empty;
        public int? Mes { get; set; }
        public int? Anio { get; set; }
    }
}
