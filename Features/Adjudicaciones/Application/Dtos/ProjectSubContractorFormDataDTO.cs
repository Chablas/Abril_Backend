using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.Adjudicaciones.Application.Dtos {
    public class ProjectSubContractorFormDataDTO {
        public List<ProjectSimpleDTO> Projects {get;set;}
        public List<ContractSimpleDTO> Contracts {get;set;}
        public List<ContractTypeSimpleDTO> ContractTypes {get;set;}
        public List<ContractOriginSimpleDTO> ContractOrigins {get;set;}
        public List<PaymentMethodSimpleDTO> PaymentMethods {get;set;}
        public List<CurrencySimpleDTO> Currencies {get;set;}
        public List<WorkItemSimpleDTO> WorkItems {get;set;}
        public List<CompanySimpleDTO> Companies {get;set;}
    }
}