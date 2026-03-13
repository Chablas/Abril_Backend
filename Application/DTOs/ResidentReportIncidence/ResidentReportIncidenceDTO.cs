namespace Abril_Backend.Application.DTOs {
    public class ResidentReportIncidenceDTO {
        public int ResidentReportIncidenceId {get;set;}
        public string ResidentReportIncidenceDescription {get; set;}
        public int ProjectId {get;set;}
        public string ProjectDescription {get; set;}
        public int StateId {get;set;}
        public string StateDescription { get; set; }
        public List<ResidentReportIncidenceImageDTO> Images {get;set;}
    }

    public class ResidentReportIncidenceImageDTO
    {
        public string ImageUrl {get;set;}
    }
}