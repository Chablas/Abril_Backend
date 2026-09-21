using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models
{
    /// <summary>
    /// Vocabulario plano de áreas. NO contiene jerarquía — el árbol vive en area_scope.
    /// </summary>
    [Table("area_item")]
    public class AreaItem
    {
        [Column("area_item_id")]
        public int AreaItemId { get; set; }

        [Column("area_item_name")]
        public string AreaItemName { get; set; } = string.Empty;

        [Column("area_type_id")]
        public int AreaTypeId { get; set; }

        /// <summary>
        /// Sigla del área (GTH, SSOMA, UDP...). Arma el código de la rendición grupal de Gestión
        /// Administrativa: <c>CONS-&lt;abreviatura&gt;-AAAA-NNN</c>. Se mantiene por base de datos; sin
        /// sigla, el código la deduce de las iniciales del nombre.
        /// </summary>
        [Column("abreviatura")]
        public string? Abreviatura { get; set; }

        [Column("active")]
        public bool Active { get; set; }

        /// <summary>Soft-delete flag. false = registro eliminado (se mantiene para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        public AreaType? AreaType { get; set; }
    }
}
