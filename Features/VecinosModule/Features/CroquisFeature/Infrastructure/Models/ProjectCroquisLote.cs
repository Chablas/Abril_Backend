namespace Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Models
{
    /// <summary>
    /// Un lote dibujado a mano sobre el croquis de un proyecto. El polígono se guarda como
    /// JSON con coordenadas relativas (0–1) al tamaño de la imagen, para que escale con el zoom.
    /// </summary>
    public class ProjectCroquisLote
    {
        public int ProjectCroquisLoteId { get; set; }

        public int ProjectCroquisId { get; set; }
        public ProjectCroquis? ProjectCroquis { get; set; }

        public string NumeroLote { get; set; } = null!;

        /// <summary>JSON: lista de puntos [[x,y], …] con x,y en rango 0–1 relativos a la imagen.</summary>
        public string Poligono { get; set; } = null!;

        /// <summary>Vecino asignado a este lote (opcional). Se asigna desde la vista de Gestión.</summary>
        public int? VecinoId { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
