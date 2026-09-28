using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models
{
    /// <summary>Antes "Bloqueo". La tabla física sigue siendo bim_bloqueo — solo se
    /// renombró la clase/rutas C#, no el esquema.</summary>
    [Table("bim_bloqueo")]
    public class BimRestriccion
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        [Column("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [Column("estado")]
        public string Estado { get; set; } = string.Empty;

        [Column("fecha_creacion")]
        public DateTimeOffset FechaCreacion { get; set; }

        [Column("fecha_actualizacion")]
        public DateTimeOffset? FechaActualizacion { get; set; }

        /// <summary>Fecha real de levantamiento — ya cubría este rol antes del rename,
        /// se completa en Cerrar().</summary>
        [Column("fecha_cierre")]
        public DateTimeOffset? FechaCierre { get; set; }

        /// <summary>Fecha estimada/objetivo de levantamiento. Nueva.</summary>
        [Column("fecha_levantamiento_prevista")]
        public DateOnly? FechaLevantamientoPrevista { get; set; }

        [Column("created_user_id")]
        public int CreatedUserId { get; set; }

        // ── Ubicación afectada (todas nullable) ─────────────────────────────
        [Column("torre_id")]
        public int? TorreId { get; set; }
        public BimProyectoTorre? Torre { get; set; }

        [Column("nivel_id")]
        public int? NivelId { get; set; }
        public BimTorreNivel? Nivel { get; set; }

        [Column("sector")]
        public int? Sector { get; set; }

        [Column("actividad_id")]
        public int? ActividadId { get; set; }
        public BimActividad? Actividad { get; set; }

        // ── Compatibilidad retroactiva (propiedades obsoletas no mapeadas) ──
        [NotMapped]
        public int? ZonaId { get => TorreId; set => TorreId = value; }

        [NotMapped]
        public BimProyectoTorre? Zona { get => Torre; set => Torre = value; }

        [NotMapped]
        public int? ZonaNivelId { get => NivelId; set => NivelId = value; }

        [NotMapped]
        public BimTorreNivel? ZonaNivel { get => Nivel; set => Nivel = value; }

        [NotMapped]
        public int? ZonaSectorId { get => Sector; set => Sector = value; }
    }
}
