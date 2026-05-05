using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Models
{
    [Table("ss_empresa_contratista")]
    public class SsEmpresaContratista
    {
        public int Id { get; set; }
        public string? Ruc { get; set; }
        public string RazonSocial { get; set; } = string.Empty;
        public string? NombreComercial { get; set; }
        public string? Rubro { get; set; }
        public string? Direccion { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public string? EmailGerente { get; set; }
        public string? EmailAdmin { get; set; }
        public string? EmailResidente { get; set; }
        public string? EmailSsoma { get; set; }
        public string? LogoUrl { get; set; }
        public string? PartidaRegistral { get; set; }
        public string Tipo { get; set; } = "CONTRATISTA";
        public bool Activo { get; set; } = true;
        public string? ActivoRetirado { get; set; }
        public int? ProyectoId { get; set; }
        public int? IdLegacy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(ProyectoId))]
        public Project? Proyecto { get; set; }
    }
}
