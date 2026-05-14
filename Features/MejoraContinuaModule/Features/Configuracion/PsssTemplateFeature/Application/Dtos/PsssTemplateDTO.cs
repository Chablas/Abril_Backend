namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Dtos
{
    public class PsssTemplateDTO
    {
        public int PsssTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Active { get; set; }
        public int PsssCount { get; set; }
    }

    public class PsssTemplateSimpleDTO
    {
        public int PsssTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
    }

    public class PsssTemplateCreateDTO
    {
        public string TemplateName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class PsssTemplatePagedDTO
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public List<PsssTemplateDTO> Data { get; set; } = new();
    }

    public class UpdateTemplatePsssDTO
    {
        public List<int> PsssIds { get; set; } = new();
    }
}
