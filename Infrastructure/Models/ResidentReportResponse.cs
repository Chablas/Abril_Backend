namespace Abril_Backend.Infrastructure.Models {
    public class ResidentReportResponse {
        public int ResidentReportResponseId {get; set;}
        public int ResidentReportIncidenceId {get; set;}
        public string ResidentReportResponseDescription {get;set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
        public ResidentReportIncidence ResidentReportIncidence { get; set; }
    }
}