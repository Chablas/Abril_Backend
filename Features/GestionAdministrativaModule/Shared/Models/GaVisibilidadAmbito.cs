using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Catálogo de ámbitos de visibilidad por área: SALIDAS (Gestión de Salidas) y RENDICIONES
    /// (Gestión de Rendiciones). Ver <see cref="Abril_Backend.Shared.Constants.VisibilidadAmbitoIds"/>.
    /// </summary>
    [Table("ga_visibilidad_ambito")]
    public class GaVisibilidadAmbito
    {
        [Column("ga_visibilidad_ambito_id")]
        public int GaVisibilidadAmbitoId { get; set; }

        [Column("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("orden")]
        public int Orden { get; set; }

        [Column("active")]
        public bool Active { get; set; } = true;

        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
