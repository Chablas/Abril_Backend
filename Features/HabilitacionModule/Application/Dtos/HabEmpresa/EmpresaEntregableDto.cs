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
        public string? MotivoRechazo { get; set; }
        public bool RequiereVigencia { get; set; }
        public bool EsMensual { get; set; }
        public string Responsable { get; set; } = string.Empty;
        public int? Mes { get; set; }
        public int? Anio { get; set; }
        public List<EntregableMesDto> Meses { get; set; } = [];
    }

    public class EntregableMesDto
    {
        public int Id { get; set; }
        public int Mes { get; set; }
        public int Anio { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime? Vigencia { get; set; }
        public string? ArchivoUrl { get; set; }
        public string? ObsAbril { get; set; }
        public string? ObsContratista { get; set; }
        public string? MotivoRechazo { get; set; }
    }
}
