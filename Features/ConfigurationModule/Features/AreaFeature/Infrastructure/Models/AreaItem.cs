using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models
{
    [Table("area_item")]
    public class AreaItem
    {
        [Column("area_item_id")]
        public int AreaItemId { get; set; }

        [Column("area_item_name")]
        public string AreaItemName { get; set; } = string.Empty;

        [Column("area_type_id")]
        public int AreaTypeId { get; set; }

        [Column("area_item_parent_id")]
        public int? AreaItemParentId { get; set; }

        [Column("active")]
        public bool Active { get; set; }

        public AreaType? AreaType { get; set; }
        public AreaItem? Parent { get; set; }
    }
}
