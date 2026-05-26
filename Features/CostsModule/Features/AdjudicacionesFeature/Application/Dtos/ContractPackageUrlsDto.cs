namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    public class ContractPackageUrlsDto
    {
        public string? SummarySheetUrl     { get; init; }
        public string? SummarySheetItemId  { get; init; }
        public string? ContractUrl         { get; init; }
        public string? ContractItemId      { get; init; }
        /// <summary>Cotización adjunta del paso 3 (PDF). Se inserta dentro del contrato, después del marcador.</summary>
        public string? AttachedQuotationUrl    { get; init; }
        public string? AttachedQuotationItemId { get; init; }
        public string? NonConformingOutputUrl    { get; init; }
        public string? NonConformingOutputItemId { get; init; }
        public string? ToleranceChartUrl         { get; init; }
        public string? ToleranceChartItemId      { get; init; }
        public string? InstructivoUrl            { get; init; }
        public string? InstructivoItemId         { get; init; }
        /// <summary>Presente solo cuando la adjudicación tiene contrato con adelanto.</summary>
        public string? PromissoryNoteUrl    { get; init; }
        public string? PromissoryNoteItemId { get; init; }
        /// <summary>Número de contrato para armar el nombre del archivo (ej. 17 → _C017).</summary>
        public int? ContractNumber { get; init; }
    }
}
