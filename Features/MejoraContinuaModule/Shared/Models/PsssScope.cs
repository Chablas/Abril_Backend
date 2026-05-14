namespace Abril_Backend.Infrastructure.Models
{
    public class PsssScope
    {
        public int PsssScopeId { get; set; }
        public int PhaseStageSubStageSubSpecialtyId { get; set; }
        public int? AreaId { get; set; }
        public int? SubAreaId { get; set; }
        public bool State { get; set; }
    }
}
