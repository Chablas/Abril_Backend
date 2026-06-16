using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models
{
    public class Vecino
    {
        public int VecinoId { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        public string? Predio { get; set; }
        public string Direccion { get; set; } = null!;
        public string? InteriorDepartamento { get; set; }
        public string NombrePropietario { get; set; } = null!;
        public string Dni { get; set; } = null!;
        public string? Celular { get; set; }

        public int VecinoColindanciaId { get; set; }
        public VecinoColindancia? Colindancia { get; set; }

        public int VecinoTipoConstruccionId { get; set; }
        public VecinoTipoConstruccion? TipoConstruccion { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
