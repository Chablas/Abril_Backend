namespace Abril_Backend.Infrastructure.Models
{
    public class AreaSubarea
    {
        public int AreaSubareaId { get; set; }
        public int AreaId { get; set; }
        public int? SubAreaId { get; set; }

        public Area Area { get; set; } = null!;
        public SubArea? SubArea { get; set; }
    }
}
