namespace Abril_Backend.Infrastructure.Models
{
    public class Partida
    {
        public int PartidaId { get; set; }
        public string PartidaDescription { get; set; }
        public bool State { get; set; }
        public bool Active { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
    }
}
