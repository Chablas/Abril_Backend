using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Repositories {
    public class ProjectSubContractorRepository : IProjectSubContractorRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public ProjectSubContractorRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task Create(ProjectSubContractorCreateDTO dto, List<string> quotationFileUrls, List<string> comparativeFileUrls, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var subContractor = new ProjectSubContractor
            {
                ProjectId = dto.ProjectId,
                CompanyId = dto.CompanyId,
                ContractId = dto.ContractId,
                ContractTypeId = dto.ContractTypeId,
                ContractOriginId = dto.ContractOriginId,
                PaymentMethodId = dto.PaymentMethodId,
                Amount = dto.Amount,
                CurrencyId  = dto.CurrencyId,
                HasIgv = dto.HasIgv,
                ContractorEmail = dto.ContractorEmail,
                WorkItemId = dto.WorkItemId,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            foreach (var url in quotationFileUrls)
            {
                subContractor.QuotationFiles.Add(new ProjectSubContractorQuotationFile
                {
                    FileUrl = url,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            foreach (var url in comparativeFileUrls)
            {
                subContractor.ComparativeFiles.Add(new ProjectSubContractorComparativeFile
                {
                    FileUrl = url,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            ctx.ProjectSubContractor.Add(subContractor);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<ContractSimpleDTO>> GetContractsFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Contract
                .Where(item => item.Active)
                .OrderBy(item => item.ContractDescription)
                .Select(item => new ContractSimpleDTO
                {
                    ContractId = item.ContractId,
                    ContractDescription = item.ContractDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<ContractTypeSimpleDTO>> GetContractTypeFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.ContractType
                .Where(item => item.Active)
                .OrderBy(item => item.ContractTypeDescription)
                .Select(item => new ContractTypeSimpleDTO
                {
                    ContractTypeId = item.ContractTypeId,
                    ContractTypeDescription = item.ContractTypeDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<ContractOriginSimpleDTO>> GetContractOriginFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.ContractOrigin
                .Where(item => item.Active)
                .OrderBy(item => item.ContractOriginDescription)
                .Select(item => new ContractOriginSimpleDTO
                {
                    ContractOriginId = item.ContractOriginId,
                    ContractOriginDescription = item.ContractOriginDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<PaymentMethodSimpleDTO>> GetPaymentMethodFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.PaymentMethod
                .Where(item => item.Active)
                .OrderBy(item => item.PaymentMethodDescription)
                .Select(item => new PaymentMethodSimpleDTO
                {
                    PaymentMethodId = item.PaymentMethodId,
                    PaymentMethodDescription = item.PaymentMethodDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<CurrencySimpleDTO>> GetCurrencyFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Currency
                .Where(item => item.Active)
                .OrderBy(item => item.CurrencyDescription)
                .Select(item => new CurrencySimpleDTO
                {
                    CurrencyId = item.CurrencyId,
                    CurrencyDescription = item.CurrencyDescription,
                    CurrencyCode = item.CurrencyCode,
                    CurrencySymbol = item.CurrencySymbol,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<WorkItemSimpleDTO>> GetWorkItemFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.WorkItem
                .Where(item => item.Active)
                .OrderBy(item => item.WorkItemDescription)
                .Select(item => new WorkItemSimpleDTO
                {
                    WorkItemId = item.WorkItemId,
                    WorkItemDescription = item.WorkItemDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<CompanySimpleDTO>> GetCompanyFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.Company
                .Where(item => item.Active)
                .OrderBy(item => item.CompanyName)
                .Select(item => new CompanySimpleDTO
                {
                    CompanyId = item.CompanyId,
                    CompanyName = item.CompanyName,
                    CompanyRuc = item.CompanyRuc,
                });
            return await registros.ToListAsync();
        }
    }
}