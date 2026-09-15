using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Models
{
    /// <summary>
    /// Un proyecto dentro de un anillo de rotación de inspecciones cruzadas. Los proyectos de
    /// un mismo <see cref="AnilloId"/> solo se inspeccionan entre ellos — dos anillos nunca se
    /// mezclan. El <see cref="Orden"/> define la posición dentro del anillo;
    /// <see cref="InspeccionCruzadaProgramacionService"/> la recorre en forma circular
    /// (desplazando el punto de partida un puesto cada mes) para asignar quién inspecciona a
    /// quién.
    /// </summary>
    [Table("ss_inspeccion_cruzada_rotacion")]
    public class SsInspeccionCruzadaRotacion
    {
        public int Id { get; set; }
        public int AnilloId { get; set; }
        public int ProyectoId { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(ProyectoId))]
        public Project? Proyecto { get; set; }
    }
}
