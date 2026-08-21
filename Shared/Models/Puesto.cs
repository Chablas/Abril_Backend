using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Catálogo único de puestos de trabajo. Es el campo de PRESENTACIÓN: es lo que se
    /// muestra en pantalla, PDFs y correos, y no debe usarse para ninguna decisión de
    /// negocio — para eso está <see cref="Categoria"/>.
    ///
    /// Unifica los tres orígenes que existían antes:
    /// <list type="bullet">
    ///   <item><c>workers.puesto</c> — texto libre, autocompletado como "Categoría + Ocupación".</item>
    ///   <item><c>cat_ocupacion</c> + el texto libre de <c>workers.ocupacion</c> que nunca
    ///   llegó a ese catálogo (Gasfitero, Cajero, Tecnico II.EE…).</item>
    ///   <item><c>gth_puesto</c> — el desplegable de Solicitud de Personal.</item>
    /// </list>
    /// Al migrar, cuando un trabajador tenía puesto Y ocupación prevaleció el puesto.
    ///
    /// Los nombres se guardan siempre en MAYÚSCULAS.
    /// </summary>
    [Table("puesto")]
    public class Puesto
    {
        [Column("puesto_id")]
        public int PuestoId { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Categoría a la que pertenece el puesto. Obligatoria: es el único camino a la
        /// categoría de un trabajador (<c>workers.puesto_id → puesto.categoria_id</c>),
        /// así que un puesto sin categoría dejaría a sus fichas fuera de todo filtro y
        /// de toda regla. La columna es NOT NULL desde
        /// <c>Migrations_Manual/2026-08-21_workers_categoria_desde_puesto.sql</c>.
        ///
        /// El valor original se derivó de los datos reales: la categoría más frecuente
        /// entre quienes ejercían el puesto.
        /// </summary>
        [Column("categoria_id")]
        public int CategoriaId { get; set; }

        [ForeignKey(nameof(CategoriaId))]
        public Categoria? Categoria { get; set; }

        [Column("orden")]
        public int Orden { get; set; }

        [Column("created_date_time")]
        public DateTime CreatedDateTime { get; set; }

        [Column("created_user_id")]
        public int? CreatedUserId { get; set; }

        [Column("updated_date_time")]
        public DateTime? UpdatedDateTime { get; set; }

        [Column("updated_user_id")]
        public int? UpdatedUserId { get; set; }

        /// <summary>Habilitar/inhabilitar en desplegables.</summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        /// <summary>Soft-delete: false = eliminado (se conserva para histórico).</summary>
        [Column("state")]
        public bool State { get; set; } = true;
    }
}
