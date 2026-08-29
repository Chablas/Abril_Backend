using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models
{
    /// <summary>
    /// Único por (project_id, zona_id, nivel_id, sector_id, actividad_id, fecha) —
    /// ver índice único en AppContext. Una corrección dentro de la ventana de edición
    /// es UPDATE sobre esa fila, no un INSERT nuevo.
    /// </summary>
    [Table("bim_registro_diario")]
    public class BimRegistroDiario
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        [Column("torre_id")]
        public int TorreId { get; set; }
        public BimProyectoTorre Torre { get; set; } = null!;

        [NotMapped]
        public int ZonaId { get => TorreId; set => TorreId = value; }

        [NotMapped]
        public BimProyectoTorre Zona { get => Torre; set => Torre = value; }

        [Column("nivel_id")]
        public int NivelId { get; set; }
        public BimTorreNivel Nivel { get; set; } = null!;

        /// <summary>Número de sector derivado (1..N), no FK — ver BimZonaSector.cs.
        /// Válido para el nivel si está entre 1 y la cantidad de sectores que le
        /// corresponde en la torre según TipoEstructura del nivel; se valida en
        /// PlaneamientoBimCargaDiariaService, no hay constraint de BD.</summary>
        [Column("sector_id")]
        public int SectorId { get; set; }

        [Column("actividad_id")]
        public int ActividadId { get; set; }
        public BimActividad Actividad { get; set; } = null!;

        [Column("fecha")]
        public DateOnly Fecha { get; set; }

        /// <summary>% de avance de la actividad ese día. Desde el toggle binario de Carga
        /// Diaria (Torre→Nivel→Sector) el service solo escribe 100 (hecho) o 0 (no hecho).
        /// Valores intermedios (25/50/75) son datos históricos de un modelo anterior —
        /// no se generan más pero no se tocan.</summary>
        [Column("porcentaje_avance")]
        public decimal PorcentajeAvance { get; set; }

        [Column("causa_id")]
        public int? CausaId { get; set; }
        public BimCausaNoCumplimiento? Causa { get; set; }

        [Column("causa_detalle")]
        public string? CausaDetalle { get; set; }

        [Column("created_user_id")]
        public int CreatedUserId { get; set; }

        [Column("created_date_time")]
        public DateTimeOffset CreatedDateTime { get; set; }

        [Column("updated_user_id")]
        public int? UpdatedUserId { get; set; }

        [Column("updated_date_time")]
        public DateTimeOffset? UpdatedDateTime { get; set; }
    }
}
