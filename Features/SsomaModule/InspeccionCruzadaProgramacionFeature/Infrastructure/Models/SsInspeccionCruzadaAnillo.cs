using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Models
{
    /// <summary>Un grupo de proyectos que se inspeccionan entre ellos (ver
    /// <see cref="SsInspeccionCruzadaRotacion"/>). Ej: "Anillo Norte", "Anillo Lima".</summary>
    [Table("ss_inspeccion_cruzada_anillo")]
    public class SsInspeccionCruzadaAnillo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
