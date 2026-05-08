using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Shared.Extensions;
using Dapper;
using System.Data;
using ProjectModel = Abril_Backend.Shared.Models.Project;

namespace Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Repositories {
    public class ProjectSubContractorRepository : IProjectSubContractorRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public ProjectSubContractorRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }

        public async Task<int> Create(ProjectSubContractorCreateDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var subContractor = new ProjectSubContractor
            {
                ProjectId = dto.ProjectId,
                ContractorId = dto.ContractorId,
                ContractId = dto.ContractId,
                ContractTypeId = dto.ContractTypeId,
                ContractOriginId = dto.ContractOriginId,
                PaymentMethodId = dto.PaymentMethodId,
                AdvancePercentage = dto.AdvancePercentage,
                AdvanceAmount = dto.AdvanceAmount,
                Amount = dto.Amount,
                CurrencyId  = dto.CurrencyId,
                HasIgv = dto.HasIgv,
                ContractorEmail = string.Empty,
                WorkItemId = dto.WorkItemId,
                WorkItemCategoryId = dto.WorkItemCategoryId,
                ProjectSubContractorStatusId = 1,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            ctx.ProjectSubContractor.Add(subContractor);
            await ctx.SaveChangesAsync();

            return subContractor.ProjectSubContractorId;
        }

        public async Task SaveInitialFilesAsync(
            int projectSubContractorId,
            List<(string Url, string OriginalFileName, string? ItemId)> quotationFiles,
            List<(string Url, string OriginalFileName, string? ItemId)> comparativeFiles,
            int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTime.UtcNow;

            foreach (var file in quotationFiles)
            {
                ctx.ProjectSubContractorQuotationFile.Add(new ProjectSubContractorQuotationFile
                {
                    ProjectSubContractorId = projectSubContractorId,
                    FileUrl = file.Url,
                    OriginalFileName = file.OriginalFileName,
                    SharepointItemId = file.ItemId,
                    CreatedDateTime = now,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            foreach (var file in comparativeFiles)
            {
                ctx.ProjectSubContractorComparativeFile.Add(new ProjectSubContractorComparativeFile
                {
                    ProjectSubContractorId = projectSubContractorId,
                    FileUrl = file.Url,
                    OriginalFileName = file.OriginalFileName,
                    SharepointItemId = file.ItemId,
                    CreatedDateTime = now,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

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
                .OrderBy(item => item.CurrencyCode)
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

        public async Task<List<WorkItemCategorySimpleDTO>> GetWorkItemCategoryFactory()
        {
            using var ctx = _factory.CreateDbContext();
            var registros = ctx.WorkItemCategory
                .Where(item => item.Active)
                .OrderBy(item => item.WorkItemCategoryDescription)
                .Select(item => new WorkItemCategorySimpleDTO
                {
                    WorkItemCategoryId = item.WorkItemCategoryId,
                    WorkItemCategoryDescription = item.WorkItemCategoryDescription,
                });
            return await registros.ToListAsync();
        }

        public async Task<List<ContributorFactoryDTO>> GetCompanyFactory()
        {
            using var ctx = _factory.CreateDbContext();

            const int approvedContractorStateId = 2;

            var contractors = await (
                from ct in ctx.Contractor
                join contrib in ctx.Contributor on ct.ContributorId equals contrib.ContributorId
                where ct.Active && ct.State && ct.ContractorStateId == approvedContractorStateId
                orderby contrib.ContributorName
                select new ContributorFactoryDTO
                {
                    ContractorId = ct.ContractorId,
                    ContributorId = contrib.ContributorId,
                    ContributorName = contrib.ContributorName,
                    ContributorRuc = contrib.ContributorRuc
                }
            ).ToListAsync();

            var ids = contractors.Select(contrib => contrib.ContractorId).ToList();

            var emails = await ctx.ContractorEmail
                .Where(e => ids.Contains(e.ContractorId) && e.Active)
                .Select(e => new { e.ContractorId, e.Email })
                .ToListAsync();

            var emailsByContractor = emails
                .GroupBy(e => e.ContractorId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Email).ToList());

            foreach (var contractor in contractors)
                contractor.Emails = emailsByContractor.GetValueOrDefault(contractor.ContractorId, new());


            return contractors;
        }

        /// <summary>
        /// Trae todos los catálogos del formulario de creación de adjudicaciones en
        /// UN SOLO round-trip a la BD usando Dapper con multi-statement.
        ///
        /// Refactor-safety: los nombres de tablas y columnas se obtienen del modelo de
        /// EF Core en runtime (vía <see cref="EfMetadataExtensions"/>), por lo que renombrar
        /// una propiedad o entidad mantiene el SQL sincronizado automáticamente.
        ///
        /// El binding columna→propiedad lo hace Dapper con
        /// <c>DefaultTypeMap.MatchNamesWithUnderscores = true</c> (configurado en Program.cs).
        /// </summary>
        public async Task<ProjectSubContractorFormDataDTO> GetFormDataAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // ----- Tablas y columnas resueltas desde EF (refactor-safe) -----

            // Project
            string tProject       = ctx.Table<ProjectModel>();
            string cProjectId     = ctx.Col<ProjectModel>(nameof(ProjectModel.ProjectId));
            string cProjectDesc   = ctx.Col<ProjectModel>(nameof(ProjectModel.ProjectDescription));
            string cProjectActive = ctx.Col<ProjectModel>(nameof(ProjectModel.Active));
            string cProjectState  = ctx.Col<ProjectModel>(nameof(ProjectModel.State));

            // Contract
            string tContract       = ctx.Table<Contract>();
            string cContractId     = ctx.Col<Contract>(nameof(Contract.ContractId));
            string cContractDesc   = ctx.Col<Contract>(nameof(Contract.ContractDescription));
            string cContractActive = ctx.Col<Contract>(nameof(Contract.Active));

            // ContractType
            string tContractType       = ctx.Table<ContractType>();
            string cContractTypeId     = ctx.Col<ContractType>(nameof(ContractType.ContractTypeId));
            string cContractTypeDesc   = ctx.Col<ContractType>(nameof(ContractType.ContractTypeDescription));
            string cContractTypeActive = ctx.Col<ContractType>(nameof(ContractType.Active));

            // ContractOrigin
            string tContractOrigin       = ctx.Table<ContractOrigin>();
            string cContractOriginId     = ctx.Col<ContractOrigin>(nameof(ContractOrigin.ContractOriginId));
            string cContractOriginDesc   = ctx.Col<ContractOrigin>(nameof(ContractOrigin.ContractOriginDescription));
            string cContractOriginActive = ctx.Col<ContractOrigin>(nameof(ContractOrigin.Active));

            // PaymentMethod
            string tPaymentMethod       = ctx.Table<PaymentMethod>();
            string cPaymentMethodId     = ctx.Col<PaymentMethod>(nameof(PaymentMethod.PaymentMethodId));
            string cPaymentMethodDesc   = ctx.Col<PaymentMethod>(nameof(PaymentMethod.PaymentMethodDescription));
            string cPaymentMethodActive = ctx.Col<PaymentMethod>(nameof(PaymentMethod.Active));

            // Currency
            string tCurrency        = ctx.Table<Currency>();
            string cCurrencyId      = ctx.Col<Currency>(nameof(Currency.CurrencyId));
            string cCurrencyDesc    = ctx.Col<Currency>(nameof(Currency.CurrencyDescription));
            string cCurrencyCode    = ctx.Col<Currency>(nameof(Currency.CurrencyCode));
            string cCurrencySymbol  = ctx.Col<Currency>(nameof(Currency.CurrencySymbol));
            string cCurrencyActive  = ctx.Col<Currency>(nameof(Currency.Active));

            // WorkItem
            string tWorkItem       = ctx.Table<WorkItem>();
            string cWorkItemId     = ctx.Col<WorkItem>(nameof(WorkItem.WorkItemId));
            string cWorkItemDesc   = ctx.Col<WorkItem>(nameof(WorkItem.WorkItemDescription));
            string cWorkItemActive = ctx.Col<WorkItem>(nameof(WorkItem.Active));

            // WorkItemCategory
            string tWorkItemCategory            = ctx.Table<WorkItemCategory>();
            string cWorkItemCategoryId          = ctx.Col<WorkItemCategory>(nameof(WorkItemCategory.WorkItemCategoryId));
            string cWorkItemCategoryDesc        = ctx.Col<WorkItemCategory>(nameof(WorkItemCategory.WorkItemCategoryDescription));
            string cWorkItemCategoryActive      = ctx.Col<WorkItemCategory>(nameof(WorkItemCategory.Active));
            string cWorkItemCategorySyncStatus  = ctx.Col<WorkItemCategory>(nameof(WorkItemCategory.InstructivosSyncStatus));

            // Contractor + Contributor
            string tContractor          = ctx.Table<Contractor>();
            string cContractorId        = ctx.Col<Contractor>(nameof(Contractor.ContractorId));
            string cContractorContribId = ctx.Col<Contractor>(nameof(Contractor.ContributorId));
            string cContractorActive    = ctx.Col<Contractor>(nameof(Contractor.Active));
            string cContractorState     = ctx.Col<Contractor>(nameof(Contractor.State));
            string cContractorStateId   = ctx.Col<Contractor>(nameof(Contractor.ContractorStateId));

            string tContributor       = ctx.Table<Contributor>();
            string cContributorId     = ctx.Col<Contributor>(nameof(Contributor.ContributorId));
            string cContributorName   = ctx.Col<Contributor>(nameof(Contributor.ContributorName));
            string cContributorRuc    = ctx.Col<Contributor>(nameof(Contributor.ContributorRuc));

            // ContractorEmail
            string tContractorEmail            = ctx.Table<ContractorEmail>();
            string cContractorEmailContractor  = ctx.Col<ContractorEmail>(nameof(ContractorEmail.ContractorId));
            string cContractorEmailEmail       = ctx.Col<ContractorEmail>(nameof(ContractorEmail.Email));
            string cContractorEmailActive      = ctx.Col<ContractorEmail>(nameof(ContractorEmail.Active));

            // ----- SQL multi-statement (un solo round-trip) -----

            const int approvedContractorStateId = 2;

            string sql = $@"
                SELECT {cProjectId}, {cProjectDesc}
                  FROM {tProject}
                 WHERE {cProjectActive} = TRUE AND {cProjectState} = TRUE
                 ORDER BY {cProjectDesc};

                SELECT {cContractId}, {cContractDesc}
                  FROM {tContract}
                 WHERE {cContractActive} = TRUE
                 ORDER BY {cContractDesc};

                SELECT {cContractTypeId}, {cContractTypeDesc}
                  FROM {tContractType}
                 WHERE {cContractTypeActive} = TRUE
                 ORDER BY {cContractTypeDesc};

                SELECT {cContractOriginId}, {cContractOriginDesc}
                  FROM {tContractOrigin}
                 WHERE {cContractOriginActive} = TRUE
                 ORDER BY {cContractOriginDesc};

                SELECT {cPaymentMethodId}, {cPaymentMethodDesc}
                  FROM {tPaymentMethod}
                 WHERE {cPaymentMethodActive} = TRUE
                 ORDER BY {cPaymentMethodDesc};

                SELECT {cCurrencyId}, {cCurrencyDesc}, {cCurrencyCode}, {cCurrencySymbol}
                  FROM {tCurrency}
                 WHERE {cCurrencyActive} = TRUE
                 ORDER BY {cCurrencyCode};

                SELECT {cWorkItemId}, {cWorkItemDesc}
                  FROM {tWorkItem}
                 WHERE {cWorkItemActive} = TRUE
                 ORDER BY {cWorkItemDesc};

                SELECT {cWorkItemCategoryId}, {cWorkItemCategoryDesc}, {cWorkItemCategorySyncStatus}
                  FROM {tWorkItemCategory}
                 WHERE {cWorkItemCategoryActive} = TRUE
                 ORDER BY {cWorkItemCategoryDesc};

                SELECT ct.{cContractorId} AS contractor_id,
                       contrib.{cContributorId} AS contributor_id,
                       contrib.{cContributorName} AS contributor_name,
                       contrib.{cContributorRuc} AS contributor_ruc
                  FROM {tContractor} ct
                  JOIN {tContributor} contrib ON contrib.{cContributorId} = ct.{cContractorContribId}
                 WHERE ct.{cContractorActive} = TRUE
                   AND ct.{cContractorState} = TRUE
                   AND ct.{cContractorStateId} = {approvedContractorStateId}
                 ORDER BY contrib.{cContributorName};

                SELECT {cContractorEmailContractor} AS contractor_id,
                       {cContractorEmailEmail} AS email
                  FROM {tContractorEmail}
                 WHERE {cContractorEmailActive} = TRUE;
            ";

            // ----- Ejecutar y leer los 10 result sets -----

            var connection = ctx.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var multi = await connection.QueryMultipleAsync(sql);

            var projects           = (await multi.ReadAsync<ProjectSimpleDTO>()).ToList();
            var contracts          = (await multi.ReadAsync<ContractSimpleDTO>()).ToList();
            var contractTypes      = (await multi.ReadAsync<ContractTypeSimpleDTO>()).ToList();
            var contractOrigins    = (await multi.ReadAsync<ContractOriginSimpleDTO>()).ToList();
            var paymentMethods     = (await multi.ReadAsync<PaymentMethodSimpleDTO>()).ToList();
            var currencies         = (await multi.ReadAsync<CurrencySimpleDTO>()).ToList();
            var workItems          = (await multi.ReadAsync<WorkItemSimpleDTO>()).ToList();
            var workItemCategories = (await multi.ReadAsync<WorkItemCategorySimpleDTO>()).ToList();
            var contractors        = (await multi.ReadAsync<ContributorFactoryDTO>()).ToList();
            var emails             = (await multi.ReadAsync<(int ContractorId, string Email)>()).ToList();

            // ----- Asociar emails a cada contratista -----

            var emailsByContractor = emails
                .GroupBy(e => e.ContractorId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Email).ToList());

            foreach (var contractor in contractors)
                contractor.Emails = emailsByContractor.GetValueOrDefault(contractor.ContractorId, new());

            return new ProjectSubContractorFormDataDTO
            {
                Projects           = projects,
                Contracts          = contracts,
                ContractTypes      = contractTypes,
                ContractOrigins    = contractOrigins,
                PaymentMethods     = paymentMethods,
                Currencies         = currencies,
                WorkItems          = workItems,
                WorkItemCategories = workItemCategories,
                Contributors       = contractors,
            };
        }

        public async Task<PagedResult<ProjectSubContractorDTO>> GetPaged(ProjectSubContractorFilterDTO filter)
        {
            using var ctx = _factory.CreateDbContext();

            const int pageSize = 10;

            var query =
                from psc in ctx.ProjectSubContractor
                join p in ctx.Project on psc.ProjectId equals p.ProjectId
                join contractor in ctx.Contractor on psc.ContractorId equals contractor.ContractorId
                join c in ctx.Contributor on contractor.ContributorId equals c.ContributorId
                join ct in ctx.ContractType on psc.ContractTypeId equals ct.ContractTypeId
                join co in ctx.ContractOrigin on psc.ContractOriginId equals co.ContractOriginId
                join pm in ctx.PaymentMethod on psc.PaymentMethodId equals pm.PaymentMethodId
                join cur in ctx.Currency on psc.CurrencyId equals cur.CurrencyId
                join wi in ctx.WorkItem on psc.WorkItemId equals wi.WorkItemId
                join contract in ctx.Contract on psc.ContractId equals contract.ContractId
                join pscs in ctx.ProjectSubContractorStatus on psc.ProjectSubContractorStatusId equals pscs.ProjectSubContractorStatusId
                join wic in ctx.WorkItemCategory on psc.WorkItemCategoryId equals wic.WorkItemCategoryId
                join contractDocJoin in ctx.ProjectSubContractorContract on psc.ProjectSubContractorContractId equals contractDocJoin.ProjectSubContractorContractId into contractDocGroup
                from contractDoc in contractDocGroup.DefaultIfEmpty()
                join summarySheetDocJoin in ctx.ProjectSubContractorSummarySheet on psc.ProjectSubContractorSummarySheetId equals summarySheetDocJoin.ProjectSubContractorSummarySheetId into summarySheetDocGroup
                from summarySheetDoc in summarySheetDocGroup.DefaultIfEmpty()
                join budgetDocJoin in ctx.ProjectSubContractorBudget on psc.ProjectSubContractorBudgetId equals budgetDocJoin.ProjectSubContractorBudgetId into budgetDocGroup
                from budgetDoc in budgetDocGroup.DefaultIfEmpty()
                join scheduleDocJoin in ctx.ProjectSubContractorSchedule on psc.ProjectSubContractorScheduleId equals scheduleDocJoin.ProjectSubContractorScheduleId into scheduleDocGroup
                from scheduleDoc in scheduleDocGroup.DefaultIfEmpty()
                join attachedQuotationDocJoin in ctx.ProjectSubContractorAttachedQuotation on psc.ProjectSubContractorAttachedQuotationId equals attachedQuotationDocJoin.ProjectSubContractorAttachedQuotationId into attachedQuotationDocGroup
                from attachedQuotationDoc in attachedQuotationDocGroup.DefaultIfEmpty()
                join serviceOrderDocJoin in ctx.ProjectSubContractorServiceOrder on psc.ProjectSubContractorServiceOrderId equals serviceOrderDocJoin.ProjectSubContractorServiceOrderId into serviceOrderDocGroup
                from serviceOrderDoc in serviceOrderDocGroup.DefaultIfEmpty()
                join promissoryNoteDocJoin in ctx.ProjectSubContractorPromissoryNote on psc.ProjectSubContractorPromissoryNoteId equals promissoryNoteDocJoin.ProjectSubContractorPromissoryNoteId into promissoryNoteDocGroup
                from promissoryNoteDoc in promissoryNoteDocGroup.DefaultIfEmpty()
                join packageDocJoin in ctx.ProjectSubContractorPackage on psc.ProjectSubContractorPackageId equals packageDocJoin.ProjectSubContractorPackageId into packageDocGroup
                from packageDoc in packageDocGroup.DefaultIfEmpty()
                join instructivoDocJoin in ctx.ProjectSubContractorInstructivo on psc.ProjectSubContractorInstructivoId equals instructivoDocJoin.ProjectSubContractorInstructivoId into instructivoDocGroup
                from instructivoDoc in instructivoDocGroup.DefaultIfEmpty()
                join nonConformingDocJoin in ctx.ProjectSubContractorNonConformingOutput on psc.ProjectSubContractorNonConformingOutputId equals nonConformingDocJoin.ProjectSubContractorNonConformingOutputId into nonConformingDocGroup
                from nonConformingDoc in nonConformingDocGroup.DefaultIfEmpty()
                join toleranceChartDocJoin in ctx.ProjectSubContractorToleranceChart on psc.ProjectSubContractorToleranceChartId equals toleranceChartDocJoin.ProjectSubContractorToleranceChartId into toleranceChartDocGroup
                from toleranceChartDoc in toleranceChartDocGroup.DefaultIfEmpty()
                where psc.State
                select new { psc, p, contractor, c, ct, co, pm, cur, wi, contract, pscs, wic, contractDoc, summarySheetDoc, budgetDoc, scheduleDoc, attachedQuotationDoc, serviceOrderDoc, promissoryNoteDoc, packageDoc, instructivoDoc, nonConformingDoc, toleranceChartDoc };

            if (filter.ProjectId.HasValue)
                query = query.Where(x => x.psc.ProjectId == filter.ProjectId.Value);

            if (!string.IsNullOrWhiteSpace(filter.ContributorName))
                query = query.Where(x => x.c.ContributorName.Contains(filter.ContributorName));

            if (!string.IsNullOrWhiteSpace(filter.ContributorRuc))
                query = query.Where(x => x.c.ContributorRuc.Contains(filter.ContributorRuc));

            if (filter.CreatedUserId.HasValue)
                query = query.Where(x => x.psc.CreatedUserId == filter.CreatedUserId.Value);

            query = query.OrderByDescending(x => x.psc.ProjectSubContractorId);

            var totalRecords = await query.CountAsync();

            var items = await query
                .Skip((filter.Page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProjectSubContractorDTO
                {
                    ProjectSubContractorId = x.psc.ProjectSubContractorId,
                    ProjectId = x.psc.ProjectId,
                    ProjectDescription = x.p.ProjectDescription,
                    ContractorId = x.psc.ContractorId,
                    ContributorId = x.c.ContributorId,
                    ContributorName = x.c.ContributorName,
                    ContractId = x.psc.ContractId,
                    ContractDescription = x.contract.ContractDescription,
                    ContractTypeId = x.psc.ContractTypeId,
                    ContractTypeDescription = x.ct.ContractTypeDescription,
                    ContractOriginId = x.psc.ContractOriginId,
                    ContractOriginDescription = x.co.ContractOriginDescription,
                    PaymentMethodId = x.psc.PaymentMethodId,
                    PaymentMethodDescription = x.pm.PaymentMethodDescription,
                    AdvancePercentage = x.psc.AdvancePercentage,
                    AdvanceAmount = x.psc.AdvanceAmount,
                    Amount = x.psc.Amount,
                    CurrencyId = x.psc.CurrencyId,
                    CurrencyCode = x.cur.CurrencyCode,
                    AmountHasIgv = x.psc.HasIgv,
                    WorkItemId = x.psc.WorkItemId,
                    WorkItemDescription = x.wi.WorkItemDescription,
                    WorkItemCategoryId = x.psc.WorkItemCategoryId,
                    WorkItemCategoryDescription = x.wic.WorkItemCategoryDescription,
                    ProjectSubContractorStatusId = x.pscs.ProjectSubContractorStatusId,
                    ProjectSubContractorStatusDescription = x.pscs.ProjectSubContractorStatusDescription,
                    SigningDate = x.psc.SigningDate,
                    StartDate = x.psc.StartDate,
                    EndDate = x.psc.EndDate,
                    TermDays = x.psc.TermDays,
                    ContractNumber           = x.psc.ContractNumber,
                    PromissoryNoteNumber     = x.psc.PromissoryNoteNumber,
                    ArrivedWithObservations  = x.psc.ArrivedWithObservations,
                    CreatedDateTime          = x.psc.CreatedDateTime,
                    Contract          = x.contractDoc == null          ? null : new ProjectSubContractorFileDto { FileUrl = x.contractDoc.FileUrl!,          OriginalFileName = x.contractDoc.OriginalFileName,          StatusId = x.contractDoc.ProjectSubContractorFileStatusId,          StatusDescription = x.contractDoc.FileStatus == null          ? null : x.contractDoc.FileStatus.ProjectSubContractorFileStatusDescription,          Observation = x.contractDoc.Observation },
                    SummarySheet      = x.summarySheetDoc == null      ? null : new ProjectSubContractorFileDto { FileUrl = x.summarySheetDoc.FileUrl!,      OriginalFileName = x.summarySheetDoc.OriginalFileName,      StatusId = x.summarySheetDoc.ProjectSubContractorFileStatusId,      StatusDescription = x.summarySheetDoc.FileStatus == null      ? null : x.summarySheetDoc.FileStatus.ProjectSubContractorFileStatusDescription,      Observation = x.summarySheetDoc.Observation },
                    Budget            = x.budgetDoc == null            ? null : new ProjectSubContractorFileDto { FileUrl = x.budgetDoc.FileUrl!,            OriginalFileName = x.budgetDoc.OriginalFileName,            StatusId = x.budgetDoc.ProjectSubContractorFileStatusId,            StatusDescription = x.budgetDoc.FileStatus == null            ? null : x.budgetDoc.FileStatus.ProjectSubContractorFileStatusDescription,            Observation = x.budgetDoc.Observation },
                    Schedule          = x.scheduleDoc == null          ? null : new ProjectSubContractorFileDto { FileUrl = x.scheduleDoc.FileUrl!,          OriginalFileName = x.scheduleDoc.OriginalFileName,          StatusId = x.scheduleDoc.ProjectSubContractorFileStatusId,          StatusDescription = x.scheduleDoc.FileStatus == null          ? null : x.scheduleDoc.FileStatus.ProjectSubContractorFileStatusDescription,          Observation = x.scheduleDoc.Observation },
                    AttachedQuotation = x.attachedQuotationDoc == null ? null : new ProjectSubContractorFileDto { FileUrl = x.attachedQuotationDoc.FileUrl!, OriginalFileName = x.attachedQuotationDoc.OriginalFileName, StatusId = x.attachedQuotationDoc.ProjectSubContractorFileStatusId, StatusDescription = x.attachedQuotationDoc.FileStatus == null ? null : x.attachedQuotationDoc.FileStatus.ProjectSubContractorFileStatusDescription, Observation = x.attachedQuotationDoc.Observation },
                    ServiceOrder      = x.serviceOrderDoc == null      ? null : new ProjectSubContractorFileDto { FileUrl = x.serviceOrderDoc.FileUrl!,      OriginalFileName = x.serviceOrderDoc.OriginalFileName,      StatusId = x.serviceOrderDoc.ProjectSubContractorFileStatusId,      StatusDescription = x.serviceOrderDoc.FileStatus == null      ? null : x.serviceOrderDoc.FileStatus.ProjectSubContractorFileStatusDescription,      Observation = x.serviceOrderDoc.Observation },
                    PromissoryNote    = x.promissoryNoteDoc == null    ? null : new ProjectSubContractorFileDto { FileUrl = x.promissoryNoteDoc.FileUrl!,    OriginalFileName = x.promissoryNoteDoc.OriginalFileName,    StatusId = x.promissoryNoteDoc.ProjectSubContractorFileStatusId,    StatusDescription = x.promissoryNoteDoc.FileStatus == null    ? null : x.promissoryNoteDoc.FileStatus.ProjectSubContractorFileStatusDescription,    Observation = x.promissoryNoteDoc.Observation },
                    Package           = x.packageDoc == null           ? null : new ProjectSubContractorFileDto { FileUrl = x.packageDoc.FileUrl!,           OriginalFileName = x.packageDoc.OriginalFileName },
                    Instructivo       = x.instructivoDoc == null       ? null : new ProjectSubContractorFileDto { FileUrl = x.instructivoDoc.FileUrl!,       OriginalFileName = x.instructivoDoc.OriginalFileName,       StatusId = x.instructivoDoc.ProjectSubContractorFileStatusId,       StatusDescription = x.instructivoDoc.FileStatus == null       ? null : x.instructivoDoc.FileStatus.ProjectSubContractorFileStatusDescription,       Observation = x.instructivoDoc.Observation },
                    NonConformingOutput = x.nonConformingDoc == null   ? null : new ProjectSubContractorFileDto { FileUrl = x.nonConformingDoc.FileUrl!,     OriginalFileName = x.nonConformingDoc.OriginalFileName,     StatusId = x.nonConformingDoc.ProjectSubContractorFileStatusId,     StatusDescription = x.nonConformingDoc.FileStatus == null     ? null : x.nonConformingDoc.FileStatus.ProjectSubContractorFileStatusDescription,     Observation = x.nonConformingDoc.Observation },
                    ToleranceChart    = x.toleranceChartDoc == null    ? null : new ProjectSubContractorFileDto { FileUrl = x.toleranceChartDoc.FileUrl!,    OriginalFileName = x.toleranceChartDoc.OriginalFileName,    StatusId = x.toleranceChartDoc.ProjectSubContractorFileStatusId,    StatusDescription = x.toleranceChartDoc.FileStatus == null    ? null : x.toleranceChartDoc.FileStatus.ProjectSubContractorFileStatusDescription,    Observation = x.toleranceChartDoc.Observation },
                })
                .ToListAsync();

            var ids = items.Select(x => x.ProjectSubContractorId).ToList();

            var contractorIds = items.Select(x => x.ContractorId).Distinct().ToList();
            var contractorEmails = await ctx.ContractorEmail
                .Where(e => contractorIds.Contains(e.ContractorId) && e.Active)
                .Select(e => new { e.ContractorId, e.Email })
                .ToListAsync();
            var emailsByContractorId = contractorEmails
                .GroupBy(e => e.ContractorId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Email).ToList());

            var quotationFiles = await ctx.ProjectSubContractorQuotationFile
                .Where(f => ids.Contains(f.ProjectSubContractorId) && f.State)
                .Select(f => new { f.ProjectSubContractorId, f.FileUrl, f.OriginalFileName })
                .ToListAsync();

            var comparativeFiles = await ctx.ProjectSubContractorComparativeFile
                .Where(f => ids.Contains(f.ProjectSubContractorId) && f.State)
                .Select(f => new { f.ProjectSubContractorId, f.FileUrl, f.OriginalFileName })
                .ToListAsync();

            var quotationByPsc = quotationFiles
                .GroupBy(f => f.ProjectSubContractorId)
                .ToDictionary(g => g.Key, g => g.Select(f => new ProjectSubContractorFileDto
                {
                    FileUrl = f.FileUrl,
                    OriginalFileName = f.OriginalFileName
                }).ToList());

            var comparativeByPsc = comparativeFiles
                .GroupBy(f => f.ProjectSubContractorId)
                .ToDictionary(g => g.Key, g => g.Select(f => new ProjectSubContractorFileDto
                {
                    FileUrl = f.FileUrl,
                    OriginalFileName = f.OriginalFileName
                }).ToList());

            var scannedDocs = await ctx.ProjectSubContractorScannedDoc
                .Where(f => ids.Contains(f.ProjectSubContractorId) && f.State)
                .Select(f => new { f.ProjectSubContractorId, f.Slot, f.FileUrl, f.OriginalFileName })
                .ToListAsync();

            var scannedByPsc = scannedDocs
                .GroupBy(f => f.ProjectSubContractorId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(f => f.Slot));

            foreach (var item in items)
            {
                item.ContractorEmails = emailsByContractorId.GetValueOrDefault(item.ContractorId, new());
                item.QuotationFiles   = quotationByPsc.GetValueOrDefault(item.ProjectSubContractorId, new());
                item.ComparativeFiles = comparativeByPsc.GetValueOrDefault(item.ProjectSubContractorId, new());

                if (scannedByPsc.TryGetValue(item.ProjectSubContractorId, out var slots))
                {
                    if (slots.TryGetValue(1, out var s1))
                        item.ScannedDoc1 = new ProjectSubContractorFileDto { FileUrl = s1.FileUrl!, OriginalFileName = s1.OriginalFileName };
                    if (slots.TryGetValue(2, out var s2))
                        item.ScannedDoc2 = new ProjectSubContractorFileDto { FileUrl = s2.FileUrl!, OriginalFileName = s2.OriginalFileName };
                    if (slots.TryGetValue(3, out var s3))
                        item.ScannedDoc3 = new ProjectSubContractorFileDto { FileUrl = s3.FileUrl!, OriginalFileName = s3.OriginalFileName };
                }
            }

            return new PagedResult<ProjectSubContractorDTO>
            {
                Page = filter.Page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = items
            };
        }

        // Helper: convierte cualquier valor de fecha (DateOnly, DateTime, string, null) a DateOnly?
        private static DateOnly? ToDateOnly(dynamic value)
        {
            if (value == null) return null;
            if (value is DateOnly d) return d;
            if (value is DateTime dt) return DateOnly.FromDateTime(dt);
            if (value is string s) return DateOnly.Parse(s);
            return null;
        }

        public async Task<ProjectSubContractorPagedWithFiltersDTO> GetPagedWithFiltersAsync(ProjectSubContractorFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;

            using var ctx = _factory.CreateDbContext();
            var connection = ctx.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            const int pageSize = 10;
            var offset = (filter.Page - 1) * pageSize;

            // Resolver tablas y columnas reales desde EF (refactor-safe + funciona en SQL Server y PostgreSQL/snake_case)
            string tPsc = ctx.Table<ProjectSubContractor>();
            string cPscId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorId));
            string cPscProjectId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectId));
            string cPscContractorId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ContractorId));
            string cPscContractId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ContractId));
            string cPscContractTypeId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ContractTypeId));
            string cPscContractOriginId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ContractOriginId));
            string cPscPaymentMethodId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.PaymentMethodId));
            string cPscAmount = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.Amount));
            string cPscCurrencyId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.CurrencyId));
            string cPscHasIgv = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.HasIgv));
            string cPscWorkItemId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.WorkItemId));
            string cPscWorkItemCategoryId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.WorkItemCategoryId));
            string cPscStatusId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorStatusId));
            string cPscState = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.State));
            string cPscSigningDate = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.SigningDate));
            string cPscStartDate = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.StartDate));
            string cPscEndDate = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.EndDate));
            string cPscTermDays = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.TermDays));
            string cPscContractNumber = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ContractNumber));
            string cPscPromissoryNoteNumber = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.PromissoryNoteNumber));
            string cPscArrivedWithObservations = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ArrivedWithObservations));
            string cPscCreatedDateTime = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.CreatedDateTime));
            string cPscAdvancePercentage = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.AdvancePercentage));
            string cPscAdvanceAmount = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.AdvanceAmount));
            string cPscContractDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorContractId));
            string cPscSummarySheetDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorSummarySheetId));
            string cPscBudgetDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorBudgetId));
            string cPscScheduleDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorScheduleId));
            string cPscAttachedQuotationDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorAttachedQuotationId));
            string cPscServiceOrderDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorServiceOrderId));
            string cPscPromissoryNoteDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorPromissoryNoteId));
            string cPscPackageDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorPackageId));
            string cPscInstructivoDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorInstructivoId));
            string cPscNonConformingOutputDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorNonConformingOutputId));
            string cPscToleranceChartDocId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorToleranceChartId));
            string cPscCreatedUserId = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.CreatedUserId));

            string tProject = ctx.Table<ProjectModel>();
            string cProjectId = ctx.Col<ProjectModel>(nameof(ProjectModel.ProjectId));
            string cProjectDesc = ctx.Col<ProjectModel>(nameof(ProjectModel.ProjectDescription));
            string cProjectActive = ctx.Col<ProjectModel>(nameof(ProjectModel.Active));

            string tContractor = ctx.Table<Contractor>();
            string cContractorId = ctx.Col<Contractor>(nameof(Contractor.ContractorId));
            string cContractorContribId = ctx.Col<Contractor>(nameof(Contractor.ContributorId));
            string cContractorActive = ctx.Col<Contractor>(nameof(Contractor.Active));
            string cContractorState = ctx.Col<Contractor>(nameof(Contractor.State));
            string cContractorStateId = ctx.Col<Contractor>(nameof(Contractor.ContractorStateId));

            string tContributor = ctx.Table<Contributor>();
            string cContributorId = ctx.Col<Contributor>(nameof(Contributor.ContributorId));
            string cContributorName = ctx.Col<Contributor>(nameof(Contributor.ContributorName));
            string cContributorRuc = ctx.Col<Contributor>(nameof(Contributor.ContributorRuc));

            string tContractType = ctx.Table<ContractType>();
            string cContractTypeId = ctx.Col<ContractType>(nameof(ContractType.ContractTypeId));
            string cContractTypeDesc = ctx.Col<ContractType>(nameof(ContractType.ContractTypeDescription));
            string cContractTypeActive = ctx.Col<ContractType>(nameof(ContractType.Active));

            string tContractOrigin = ctx.Table<ContractOrigin>();
            string cContractOriginId = ctx.Col<ContractOrigin>(nameof(ContractOrigin.ContractOriginId));
            string cContractOriginDesc = ctx.Col<ContractOrigin>(nameof(ContractOrigin.ContractOriginDescription));
            string cContractOriginActive = ctx.Col<ContractOrigin>(nameof(ContractOrigin.Active));

            string tPaymentMethod = ctx.Table<PaymentMethod>();
            string cPaymentMethodId = ctx.Col<PaymentMethod>(nameof(PaymentMethod.PaymentMethodId));
            string cPaymentMethodDesc = ctx.Col<PaymentMethod>(nameof(PaymentMethod.PaymentMethodDescription));
            string cPaymentMethodActive = ctx.Col<PaymentMethod>(nameof(PaymentMethod.Active));

            string tCurrency = ctx.Table<Currency>();
            string cCurrencyId = ctx.Col<Currency>(nameof(Currency.CurrencyId));
            string cCurrencyCode = ctx.Col<Currency>(nameof(Currency.CurrencyCode));
            string cCurrencyDesc = ctx.Col<Currency>(nameof(Currency.CurrencyDescription));
            string cCurrencySymbol = ctx.Col<Currency>(nameof(Currency.CurrencySymbol));
            string cCurrencyActive = ctx.Col<Currency>(nameof(Currency.Active));

            string tWorkItem = ctx.Table<WorkItem>();
            string cWorkItemId = ctx.Col<WorkItem>(nameof(WorkItem.WorkItemId));
            string cWorkItemDesc = ctx.Col<WorkItem>(nameof(WorkItem.WorkItemDescription));
            string cWorkItemActive = ctx.Col<WorkItem>(nameof(WorkItem.Active));

            string tWorkItemCategory = ctx.Table<WorkItemCategory>();
            string cWorkItemCategoryId = ctx.Col<WorkItemCategory>(nameof(WorkItemCategory.WorkItemCategoryId));
            string cWorkItemCategoryDesc = ctx.Col<WorkItemCategory>(nameof(WorkItemCategory.WorkItemCategoryDescription));
            string cWorkItemCategoryActive = ctx.Col<WorkItemCategory>(nameof(WorkItemCategory.Active));

            string tContract = ctx.Table<Contract>();
            string cContractId = ctx.Col<Contract>(nameof(Contract.ContractId));
            string cContractDesc = ctx.Col<Contract>(nameof(Contract.ContractDescription));
            string cContractActive = ctx.Col<Contract>(nameof(Contract.Active));

            string tStatus = ctx.Table<ProjectSubContractorStatus>();
            string cStatusId = ctx.Col<ProjectSubContractorStatus>(nameof(ProjectSubContractorStatus.ProjectSubContractorStatusId));
            string cStatusDesc = ctx.Col<ProjectSubContractorStatus>(nameof(ProjectSubContractorStatus.ProjectSubContractorStatusDescription));

            // Document tables
            string tContractDoc = ctx.Table<ProjectSubContractorContract>();
            string cContractDocId = ctx.Col<ProjectSubContractorContract>(nameof(ProjectSubContractorContract.ProjectSubContractorContractId));
            string cContractDocFileUrl = ctx.Col<ProjectSubContractorContract>(nameof(ProjectSubContractorContract.FileUrl));
            string cContractDocFileName = ctx.Col<ProjectSubContractorContract>(nameof(ProjectSubContractorContract.OriginalFileName));
            string cContractDocStatusId = ctx.Col<ProjectSubContractorContract>(nameof(ProjectSubContractorContract.ProjectSubContractorFileStatusId));
            string cContractDocObs = ctx.Col<ProjectSubContractorContract>(nameof(ProjectSubContractorContract.Observation));

            string tSummaryDoc = ctx.Table<ProjectSubContractorSummarySheet>();
            string cSummaryDocId = ctx.Col<ProjectSubContractorSummarySheet>(nameof(ProjectSubContractorSummarySheet.ProjectSubContractorSummarySheetId));
            string cSummaryDocFileUrl = ctx.Col<ProjectSubContractorSummarySheet>(nameof(ProjectSubContractorSummarySheet.FileUrl));
            string cSummaryDocFileName = ctx.Col<ProjectSubContractorSummarySheet>(nameof(ProjectSubContractorSummarySheet.OriginalFileName));
            string cSummaryDocStatusId = ctx.Col<ProjectSubContractorSummarySheet>(nameof(ProjectSubContractorSummarySheet.ProjectSubContractorFileStatusId));
            string cSummaryDocObs = ctx.Col<ProjectSubContractorSummarySheet>(nameof(ProjectSubContractorSummarySheet.Observation));

            string tBudgetDoc = ctx.Table<ProjectSubContractorBudget>();
            string cBudgetDocId = ctx.Col<ProjectSubContractorBudget>(nameof(ProjectSubContractorBudget.ProjectSubContractorBudgetId));
            string cBudgetDocFileUrl = ctx.Col<ProjectSubContractorBudget>(nameof(ProjectSubContractorBudget.FileUrl));
            string cBudgetDocFileName = ctx.Col<ProjectSubContractorBudget>(nameof(ProjectSubContractorBudget.OriginalFileName));
            string cBudgetDocStatusId = ctx.Col<ProjectSubContractorBudget>(nameof(ProjectSubContractorBudget.ProjectSubContractorFileStatusId));
            string cBudgetDocObs = ctx.Col<ProjectSubContractorBudget>(nameof(ProjectSubContractorBudget.Observation));

            string tScheduleDoc = ctx.Table<ProjectSubContractorSchedule>();
            string cScheduleDocId = ctx.Col<ProjectSubContractorSchedule>(nameof(ProjectSubContractorSchedule.ProjectSubContractorScheduleId));
            string cScheduleDocFileUrl = ctx.Col<ProjectSubContractorSchedule>(nameof(ProjectSubContractorSchedule.FileUrl));
            string cScheduleDocFileName = ctx.Col<ProjectSubContractorSchedule>(nameof(ProjectSubContractorSchedule.OriginalFileName));
            string cScheduleDocStatusId = ctx.Col<ProjectSubContractorSchedule>(nameof(ProjectSubContractorSchedule.ProjectSubContractorFileStatusId));
            string cScheduleDocObs = ctx.Col<ProjectSubContractorSchedule>(nameof(ProjectSubContractorSchedule.Observation));

            string tAttQuotDoc = ctx.Table<ProjectSubContractorAttachedQuotation>();
            string cAttQuotDocId = ctx.Col<ProjectSubContractorAttachedQuotation>(nameof(ProjectSubContractorAttachedQuotation.ProjectSubContractorAttachedQuotationId));
            string cAttQuotDocFileUrl = ctx.Col<ProjectSubContractorAttachedQuotation>(nameof(ProjectSubContractorAttachedQuotation.FileUrl));
            string cAttQuotDocFileName = ctx.Col<ProjectSubContractorAttachedQuotation>(nameof(ProjectSubContractorAttachedQuotation.OriginalFileName));
            string cAttQuotDocStatusId = ctx.Col<ProjectSubContractorAttachedQuotation>(nameof(ProjectSubContractorAttachedQuotation.ProjectSubContractorFileStatusId));
            string cAttQuotDocObs = ctx.Col<ProjectSubContractorAttachedQuotation>(nameof(ProjectSubContractorAttachedQuotation.Observation));

            string tSvcOrderDoc = ctx.Table<ProjectSubContractorServiceOrder>();
            string cSvcOrderDocId = ctx.Col<ProjectSubContractorServiceOrder>(nameof(ProjectSubContractorServiceOrder.ProjectSubContractorServiceOrderId));
            string cSvcOrderDocFileUrl = ctx.Col<ProjectSubContractorServiceOrder>(nameof(ProjectSubContractorServiceOrder.FileUrl));
            string cSvcOrderDocFileName = ctx.Col<ProjectSubContractorServiceOrder>(nameof(ProjectSubContractorServiceOrder.OriginalFileName));
            string cSvcOrderDocStatusId = ctx.Col<ProjectSubContractorServiceOrder>(nameof(ProjectSubContractorServiceOrder.ProjectSubContractorFileStatusId));
            string cSvcOrderDocObs = ctx.Col<ProjectSubContractorServiceOrder>(nameof(ProjectSubContractorServiceOrder.Observation));

            string tPNoteDoc = ctx.Table<ProjectSubContractorPromissoryNote>();
            string cPNoteDocId = ctx.Col<ProjectSubContractorPromissoryNote>(nameof(ProjectSubContractorPromissoryNote.ProjectSubContractorPromissoryNoteId));
            string cPNoteDocFileUrl = ctx.Col<ProjectSubContractorPromissoryNote>(nameof(ProjectSubContractorPromissoryNote.FileUrl));
            string cPNoteDocFileName = ctx.Col<ProjectSubContractorPromissoryNote>(nameof(ProjectSubContractorPromissoryNote.OriginalFileName));
            string cPNoteDocStatusId = ctx.Col<ProjectSubContractorPromissoryNote>(nameof(ProjectSubContractorPromissoryNote.ProjectSubContractorFileStatusId));
            string cPNoteDocObs = ctx.Col<ProjectSubContractorPromissoryNote>(nameof(ProjectSubContractorPromissoryNote.Observation));

            string tPackageDoc = ctx.Table<ProjectSubContractorPackage>();
            string cPackageDocId = ctx.Col<ProjectSubContractorPackage>(nameof(ProjectSubContractorPackage.ProjectSubContractorPackageId));
            string cPackageDocFileUrl = ctx.Col<ProjectSubContractorPackage>(nameof(ProjectSubContractorPackage.FileUrl));
            string cPackageDocFileName = ctx.Col<ProjectSubContractorPackage>(nameof(ProjectSubContractorPackage.OriginalFileName));

            string tInstructivoDoc = ctx.Table<ProjectSubContractorInstructivo>();
            string cInstructivoDocId = ctx.Col<ProjectSubContractorInstructivo>(nameof(ProjectSubContractorInstructivo.ProjectSubContractorInstructivoId));
            string cInstructivoDocFileUrl = ctx.Col<ProjectSubContractorInstructivo>(nameof(ProjectSubContractorInstructivo.FileUrl));
            string cInstructivoDocFileName = ctx.Col<ProjectSubContractorInstructivo>(nameof(ProjectSubContractorInstructivo.OriginalFileName));
            string cInstructivoDocStatusId = ctx.Col<ProjectSubContractorInstructivo>(nameof(ProjectSubContractorInstructivo.ProjectSubContractorFileStatusId));
            string cInstructivoDocObs = ctx.Col<ProjectSubContractorInstructivo>(nameof(ProjectSubContractorInstructivo.Observation));

            string tNonConformingDoc = ctx.Table<ProjectSubContractorNonConformingOutput>();
            string cNonConformingDocId = ctx.Col<ProjectSubContractorNonConformingOutput>(nameof(ProjectSubContractorNonConformingOutput.ProjectSubContractorNonConformingOutputId));
            string cNonConformingDocFileUrl = ctx.Col<ProjectSubContractorNonConformingOutput>(nameof(ProjectSubContractorNonConformingOutput.FileUrl));
            string cNonConformingDocFileName = ctx.Col<ProjectSubContractorNonConformingOutput>(nameof(ProjectSubContractorNonConformingOutput.OriginalFileName));
            string cNonConformingDocStatusId = ctx.Col<ProjectSubContractorNonConformingOutput>(nameof(ProjectSubContractorNonConformingOutput.ProjectSubContractorFileStatusId));
            string cNonConformingDocObs = ctx.Col<ProjectSubContractorNonConformingOutput>(nameof(ProjectSubContractorNonConformingOutput.Observation));

            string tToleranceChartDoc = ctx.Table<ProjectSubContractorToleranceChart>();
            string cToleranceChartDocId = ctx.Col<ProjectSubContractorToleranceChart>(nameof(ProjectSubContractorToleranceChart.ProjectSubContractorToleranceChartId));
            string cToleranceChartDocFileUrl = ctx.Col<ProjectSubContractorToleranceChart>(nameof(ProjectSubContractorToleranceChart.FileUrl));
            string cToleranceChartDocFileName = ctx.Col<ProjectSubContractorToleranceChart>(nameof(ProjectSubContractorToleranceChart.OriginalFileName));
            string cToleranceChartDocStatusId = ctx.Col<ProjectSubContractorToleranceChart>(nameof(ProjectSubContractorToleranceChart.ProjectSubContractorFileStatusId));
            string cToleranceChartDocObs = ctx.Col<ProjectSubContractorToleranceChart>(nameof(ProjectSubContractorToleranceChart.Observation));

            string tFileStatus = ctx.Table<ProjectSubContractorFileStatus>();
            string cFileStatusId = ctx.Col<ProjectSubContractorFileStatus>(nameof(ProjectSubContractorFileStatus.ProjectSubContractorFileStatusId));
            string cFileStatusDesc = ctx.Col<ProjectSubContractorFileStatus>(nameof(ProjectSubContractorFileStatus.ProjectSubContractorFileStatusDescription));

            string tContractorEmail = ctx.Table<ContractorEmail>();
            string cCEContractorId = ctx.Col<ContractorEmail>(nameof(ContractorEmail.ContractorId));
            string cCEEmail = ctx.Col<ContractorEmail>(nameof(ContractorEmail.Email));
            string cCEActive = ctx.Col<ContractorEmail>(nameof(ContractorEmail.Active));

            string tQuotFile = ctx.Table<ProjectSubContractorQuotationFile>();
            string cQFPscId = ctx.Col<ProjectSubContractorQuotationFile>(nameof(ProjectSubContractorQuotationFile.ProjectSubContractorId));
            string cQFFileUrl = ctx.Col<ProjectSubContractorQuotationFile>(nameof(ProjectSubContractorQuotationFile.FileUrl));
            string cQFFileName = ctx.Col<ProjectSubContractorQuotationFile>(nameof(ProjectSubContractorQuotationFile.OriginalFileName));
            string cQFState = ctx.Col<ProjectSubContractorQuotationFile>(nameof(ProjectSubContractorQuotationFile.State));

            string tCompFile = ctx.Table<ProjectSubContractorComparativeFile>();
            string cCFPscId = ctx.Col<ProjectSubContractorComparativeFile>(nameof(ProjectSubContractorComparativeFile.ProjectSubContractorId));
            string cCFFileUrl = ctx.Col<ProjectSubContractorComparativeFile>(nameof(ProjectSubContractorComparativeFile.FileUrl));
            string cCFFileName = ctx.Col<ProjectSubContractorComparativeFile>(nameof(ProjectSubContractorComparativeFile.OriginalFileName));
            string cCFState = ctx.Col<ProjectSubContractorComparativeFile>(nameof(ProjectSubContractorComparativeFile.State));

            var parameters = new DynamicParameters();
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@PageOffset", offset);

            var whereConditions = new List<string> { $"psc.{cPscState} = TRUE" };

            if (filter.ProjectId.HasValue)
            {
                whereConditions.Add($"psc.{cPscProjectId} = @ProjectId");
                parameters.Add("@ProjectId", filter.ProjectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.ContributorName))
            {
                whereConditions.Add($"c.{cContributorName} ILIKE @ContributorName");
                parameters.Add("@ContributorName", $"%{filter.ContributorName}%");
            }

            if (!string.IsNullOrWhiteSpace(filter.ContributorRuc))
            {
                whereConditions.Add($"c.{cContributorRuc} ILIKE @ContributorRuc");
                parameters.Add("@ContributorRuc", $"%{filter.ContributorRuc}%");
            }

            if (filter.CreatedUserId.HasValue)
            {
                whereConditions.Add($"psc.{cPscCreatedUserId} = @CreatedUserId");
                parameters.Add("@CreatedUserId", filter.CreatedUserId.Value);
            }

            var whereClause = string.Join(" AND ", whereConditions);

            string sql = $@"
-- 1. Count
SELECT COUNT(DISTINCT psc.{cPscId}) AS ""Total""
FROM {tPsc} psc
JOIN {tContractor} contractor ON psc.{cPscContractorId} = contractor.{cContractorId}
JOIN {tContributor} c ON contractor.{cContractorContribId} = c.{cContributorId}
WHERE {whereClause};

-- 2. Paged Data (con aliases PascalCase para que Dapper mapee a dynamic / DTOs)
SELECT psc.{cPscId} AS ""ProjectSubContractorId"",
       psc.{cPscProjectId} AS ""ProjectId"",
       p.{cProjectDesc} AS ""ProjectDescription"",
       psc.{cPscContractorId} AS ""ContractorId"",
       c.{cContributorId} AS ""ContributorId"",
       c.{cContributorName} AS ""ContributorName"",
       psc.{cPscContractId} AS ""ContractId"",
       contract.{cContractDesc} AS ""ContractDescription"",
       psc.{cPscContractTypeId} AS ""ContractTypeId"",
       ct.{cContractTypeDesc} AS ""ContractTypeDescription"",
       psc.{cPscContractOriginId} AS ""ContractOriginId"",
       co.{cContractOriginDesc} AS ""ContractOriginDescription"",
       psc.{cPscPaymentMethodId} AS ""PaymentMethodId"",
       pm.{cPaymentMethodDesc} AS ""PaymentMethodDescription"",
       psc.{cPscAdvancePercentage} AS ""AdvancePercentage"",
       psc.{cPscAdvanceAmount} AS ""AdvanceAmount"",
       psc.{cPscAmount} AS ""Amount"",
       psc.{cPscCurrencyId} AS ""CurrencyId"",
       cur.{cCurrencyCode} AS ""CurrencyCode"",
       psc.{cPscHasIgv} AS ""HasIgv"",
       psc.{cPscWorkItemId} AS ""WorkItemId"",
       wi.{cWorkItemDesc} AS ""WorkItemDescription"",
       psc.{cPscWorkItemCategoryId} AS ""WorkItemCategoryId"",
       wic.{cWorkItemCategoryDesc} AS ""WorkItemCategoryDescription"",
       psc.{cPscStatusId} AS ""ProjectSubContractorStatusId"",
       pscs.{cStatusDesc} AS ""ProjectSubContractorStatusDescription"",
       psc.{cPscSigningDate} AS ""SigningDate"",
       psc.{cPscStartDate} AS ""StartDate"",
       psc.{cPscEndDate} AS ""EndDate"",
       psc.{cPscTermDays} AS ""TermDays"",
       psc.{cPscContractNumber} AS ""ContractNumber"",
       psc.{cPscPromissoryNoteNumber} AS ""PromissoryNoteNumber"",
       psc.{cPscArrivedWithObservations} AS ""ArrivedWithObservations"",
       psc.{cPscCreatedDateTime} AS ""CreatedDateTime"",
       contractDoc.{cContractDocFileUrl} AS contract_file_url,
       contractDoc.{cContractDocFileName} AS contract_file_name,
       contractDoc.{cContractDocStatusId} AS contract_status_id,
       fs_contract.{cFileStatusDesc} AS contract_status_desc,
       contractDoc.{cContractDocObs} AS contract_observation,
       summaryDoc.{cSummaryDocFileUrl} AS summary_sheet_file_url,
       summaryDoc.{cSummaryDocFileName} AS summary_sheet_file_name,
       summaryDoc.{cSummaryDocStatusId} AS summary_sheet_status_id,
       fs_summary.{cFileStatusDesc} AS summary_sheet_status_desc,
       summaryDoc.{cSummaryDocObs} AS summary_sheet_observation,
       budgetDoc.{cBudgetDocFileUrl} AS budget_file_url,
       budgetDoc.{cBudgetDocFileName} AS budget_file_name,
       budgetDoc.{cBudgetDocStatusId} AS budget_status_id,
       fs_budget.{cFileStatusDesc} AS budget_status_desc,
       budgetDoc.{cBudgetDocObs} AS budget_observation,
       scheduleDoc.{cScheduleDocFileUrl} AS schedule_file_url,
       scheduleDoc.{cScheduleDocFileName} AS schedule_file_name,
       scheduleDoc.{cScheduleDocStatusId} AS schedule_status_id,
       fs_schedule.{cFileStatusDesc} AS schedule_status_desc,
       scheduleDoc.{cScheduleDocObs} AS schedule_observation,
       attQuotDoc.{cAttQuotDocFileUrl} AS attached_quotation_file_url,
       attQuotDoc.{cAttQuotDocFileName} AS attached_quotation_file_name,
       attQuotDoc.{cAttQuotDocStatusId} AS attached_quotation_status_id,
       fs_att_quot.{cFileStatusDesc} AS attached_quotation_status_desc,
       attQuotDoc.{cAttQuotDocObs} AS attached_quotation_observation,
       svcOrderDoc.{cSvcOrderDocFileUrl} AS service_order_file_url,
       svcOrderDoc.{cSvcOrderDocFileName} AS service_order_file_name,
       svcOrderDoc.{cSvcOrderDocStatusId} AS service_order_status_id,
       fs_svc_order.{cFileStatusDesc} AS service_order_status_desc,
       svcOrderDoc.{cSvcOrderDocObs} AS service_order_observation,
       pNoteDoc.{cPNoteDocFileUrl} AS promissory_note_file_url,
       pNoteDoc.{cPNoteDocFileName} AS promissory_note_file_name,
       pNoteDoc.{cPNoteDocStatusId} AS promissory_note_status_id,
       fs_p_note.{cFileStatusDesc} AS promissory_note_status_desc,
       pNoteDoc.{cPNoteDocObs} AS promissory_note_observation,
       packageDoc.{cPackageDocFileUrl} AS package_file_url,
       packageDoc.{cPackageDocFileName} AS package_file_name,
       instructivoDoc.{cInstructivoDocFileUrl} AS instructivo_file_url,
       instructivoDoc.{cInstructivoDocFileName} AS instructivo_file_name,
       instructivoDoc.{cInstructivoDocStatusId} AS instructivo_status_id,
       fs_instructivo.{cFileStatusDesc} AS instructivo_status_desc,
       instructivoDoc.{cInstructivoDocObs} AS instructivo_observation,
       nonConformingDoc.{cNonConformingDocFileUrl} AS non_conforming_file_url,
       nonConformingDoc.{cNonConformingDocFileName} AS non_conforming_file_name,
       nonConformingDoc.{cNonConformingDocStatusId} AS non_conforming_status_id,
       fs_non_conforming.{cFileStatusDesc} AS non_conforming_status_desc,
       nonConformingDoc.{cNonConformingDocObs} AS non_conforming_observation,
       toleranceChartDoc.{cToleranceChartDocFileUrl} AS tolerance_chart_file_url,
       toleranceChartDoc.{cToleranceChartDocFileName} AS tolerance_chart_file_name,
       toleranceChartDoc.{cToleranceChartDocStatusId} AS tolerance_chart_status_id,
       fs_tolerance_chart.{cFileStatusDesc} AS tolerance_chart_status_desc,
       toleranceChartDoc.{cToleranceChartDocObs} AS tolerance_chart_observation
FROM {tPsc} psc
JOIN {tProject} p ON psc.{cPscProjectId} = p.{cProjectId}
JOIN {tContractor} contractor ON psc.{cPscContractorId} = contractor.{cContractorId}
JOIN {tContributor} c ON contractor.{cContractorContribId} = c.{cContributorId}
JOIN {tContractType} ct ON psc.{cPscContractTypeId} = ct.{cContractTypeId}
JOIN {tContractOrigin} co ON psc.{cPscContractOriginId} = co.{cContractOriginId}
JOIN {tPaymentMethod} pm ON psc.{cPscPaymentMethodId} = pm.{cPaymentMethodId}
JOIN {tCurrency} cur ON psc.{cPscCurrencyId} = cur.{cCurrencyId}
JOIN {tWorkItem} wi ON psc.{cPscWorkItemId} = wi.{cWorkItemId}
JOIN {tContract} contract ON psc.{cPscContractId} = contract.{cContractId}
JOIN {tStatus} pscs ON psc.{cPscStatusId} = pscs.{cStatusId}
JOIN {tWorkItemCategory} wic ON psc.{cPscWorkItemCategoryId} = wic.{cWorkItemCategoryId}
LEFT JOIN {tContractDoc} contractDoc ON psc.{cPscContractDocId} = contractDoc.{cContractDocId}
LEFT JOIN {tFileStatus} fs_contract ON contractDoc.{cContractDocStatusId} = fs_contract.{cFileStatusId}
LEFT JOIN {tSummaryDoc} summaryDoc ON psc.{cPscSummarySheetDocId} = summaryDoc.{cSummaryDocId}
LEFT JOIN {tFileStatus} fs_summary ON summaryDoc.{cSummaryDocStatusId} = fs_summary.{cFileStatusId}
LEFT JOIN {tBudgetDoc} budgetDoc ON psc.{cPscBudgetDocId} = budgetDoc.{cBudgetDocId}
LEFT JOIN {tFileStatus} fs_budget ON budgetDoc.{cBudgetDocStatusId} = fs_budget.{cFileStatusId}
LEFT JOIN {tScheduleDoc} scheduleDoc ON psc.{cPscScheduleDocId} = scheduleDoc.{cScheduleDocId}
LEFT JOIN {tFileStatus} fs_schedule ON scheduleDoc.{cScheduleDocStatusId} = fs_schedule.{cFileStatusId}
LEFT JOIN {tAttQuotDoc} attQuotDoc ON psc.{cPscAttachedQuotationDocId} = attQuotDoc.{cAttQuotDocId}
LEFT JOIN {tFileStatus} fs_att_quot ON attQuotDoc.{cAttQuotDocStatusId} = fs_att_quot.{cFileStatusId}
LEFT JOIN {tSvcOrderDoc} svcOrderDoc ON psc.{cPscServiceOrderDocId} = svcOrderDoc.{cSvcOrderDocId}
LEFT JOIN {tFileStatus} fs_svc_order ON svcOrderDoc.{cSvcOrderDocStatusId} = fs_svc_order.{cFileStatusId}
LEFT JOIN {tPNoteDoc} pNoteDoc ON psc.{cPscPromissoryNoteDocId} = pNoteDoc.{cPNoteDocId}
LEFT JOIN {tFileStatus} fs_p_note ON pNoteDoc.{cPNoteDocStatusId} = fs_p_note.{cFileStatusId}
LEFT JOIN {tPackageDoc} packageDoc ON psc.{cPscPackageDocId} = packageDoc.{cPackageDocId}
LEFT JOIN {tInstructivoDoc} instructivoDoc ON psc.{cPscInstructivoDocId} = instructivoDoc.{cInstructivoDocId}
LEFT JOIN {tFileStatus} fs_instructivo ON instructivoDoc.{cInstructivoDocStatusId} = fs_instructivo.{cFileStatusId}
LEFT JOIN {tNonConformingDoc} nonConformingDoc ON psc.{cPscNonConformingOutputDocId} = nonConformingDoc.{cNonConformingDocId}
LEFT JOIN {tFileStatus} fs_non_conforming ON nonConformingDoc.{cNonConformingDocStatusId} = fs_non_conforming.{cFileStatusId}
LEFT JOIN {tToleranceChartDoc} toleranceChartDoc ON psc.{cPscToleranceChartDocId} = toleranceChartDoc.{cToleranceChartDocId}
LEFT JOIN {tFileStatus} fs_tolerance_chart ON toleranceChartDoc.{cToleranceChartDocStatusId} = fs_tolerance_chart.{cFileStatusId}
WHERE {whereClause}
ORDER BY psc.{cPscId} DESC
LIMIT @PageSize OFFSET @PageOffset;

-- 3-11. Form data queries (9 simple selects con aliases PascalCase para mapeo a DTOs)
SELECT {cProjectId} AS ""ProjectId"", {cProjectDesc} AS ""ProjectDescription"" FROM {tProject} WHERE {cProjectActive} = TRUE ORDER BY {cProjectDesc};
SELECT {cContractId} AS ""ContractId"", {cContractDesc} AS ""ContractDescription"" FROM {tContract} WHERE {cContractActive} = TRUE ORDER BY {cContractDesc};
SELECT {cContractTypeId} AS ""ContractTypeId"", {cContractTypeDesc} AS ""ContractTypeDescription"" FROM {tContractType} WHERE {cContractTypeActive} = TRUE ORDER BY {cContractTypeDesc};
SELECT {cContractOriginId} AS ""ContractOriginId"", {cContractOriginDesc} AS ""ContractOriginDescription"" FROM {tContractOrigin} WHERE {cContractOriginActive} = TRUE ORDER BY {cContractOriginDesc};
SELECT {cPaymentMethodId} AS ""PaymentMethodId"", {cPaymentMethodDesc} AS ""PaymentMethodDescription"" FROM {tPaymentMethod} WHERE {cPaymentMethodActive} = TRUE ORDER BY {cPaymentMethodDesc};
SELECT {cCurrencyId} AS ""CurrencyId"", {cCurrencyDesc} AS ""CurrencyDescription"", {cCurrencyCode} AS ""CurrencyCode"", {cCurrencySymbol} AS ""CurrencySymbol"" FROM {tCurrency} WHERE {cCurrencyActive} = TRUE ORDER BY {cCurrencyCode};
SELECT {cWorkItemId} AS ""WorkItemId"", {cWorkItemDesc} AS ""WorkItemDescription"" FROM {tWorkItem} WHERE {cWorkItemActive} = TRUE ORDER BY {cWorkItemDesc};
SELECT {cWorkItemCategoryId} AS ""WorkItemCategoryId"", {cWorkItemCategoryDesc} AS ""WorkItemCategoryDescription"" FROM {tWorkItemCategory} WHERE {cWorkItemCategoryActive} = TRUE ORDER BY {cWorkItemCategoryDesc};
SELECT ct.{cContractorId} AS ""ContractorId"", contrib.{cContributorId} AS ""ContributorId"", contrib.{cContributorName} AS ""ContributorName"", contrib.{cContributorRuc} AS ""ContributorRuc""
FROM {tContractor} ct
JOIN {tContributor} contrib ON contrib.{cContributorId} = ct.{cContractorContribId}
WHERE ct.{cContractorActive} = TRUE AND ct.{cContractorState} = TRUE AND ct.{cContractorStateId} = 2
ORDER BY contrib.{cContributorName};

-- 12-14. Supporting data
SELECT {cCEContractorId} AS ""ContractorId"", {cCEEmail} AS ""Email"" FROM {tContractorEmail} WHERE {cCEActive} = TRUE;
SELECT {cQFPscId} AS ""ProjectSubContractorId"", {cQFFileUrl} AS ""FileUrl"", {cQFFileName} AS ""OriginalFileName"" FROM {tQuotFile} WHERE {cQFState} = TRUE;
SELECT {cCFPscId} AS ""ProjectSubContractorId"", {cCFFileUrl} AS ""FileUrl"", {cCFFileName} AS ""OriginalFileName"" FROM {tCompFile} WHERE {cCFState} = TRUE;
            ";

            using var multi = await connection.QueryMultipleAsync(sql, parameters);

            // Read COUNT (PostgreSQL devuelve bigint, por eso casteamos desde long)
            var countResult = await multi.ReadFirstOrDefaultAsync<dynamic>();
            int totalRecords = countResult == null ? 0 : Convert.ToInt32(countResult.Total);

            // Read paged data
            var itemsRaw = (await multi.ReadAsync<dynamic>()).ToList();

            // Read form data queries
            var projects = (await multi.ReadAsync<ProjectSimpleDTO>()).ToList();
            var contracts = (await multi.ReadAsync<ContractSimpleDTO>()).ToList();
            var contractTypes = (await multi.ReadAsync<ContractTypeSimpleDTO>()).ToList();
            var contractOrigins = (await multi.ReadAsync<ContractOriginSimpleDTO>()).ToList();
            var paymentMethods = (await multi.ReadAsync<PaymentMethodSimpleDTO>()).ToList();
            var currencies = (await multi.ReadAsync<CurrencySimpleDTO>()).ToList();
            var workItems = (await multi.ReadAsync<WorkItemSimpleDTO>()).ToList();
            var workItemCategories = (await multi.ReadAsync<WorkItemCategorySimpleDTO>()).ToList();
            var contractors = (await multi.ReadAsync<ContributorFactoryDTO>()).ToList();

            // Read supporting data (using dynamic to avoid tuple-naming issues)
            var emailsRaw = (await multi.ReadAsync<dynamic>()).ToList();
            var quotationFilesRaw = (await multi.ReadAsync<dynamic>()).ToList();
            var comparativeFilesRaw = (await multi.ReadAsync<dynamic>()).ToList();

            var emails = emailsRaw.Select(e => new { ContractorId = (int)e.ContractorId, Email = (string)e.Email }).ToList();
            var quotationFiles = quotationFilesRaw.Select(f => new { ProjectSubContractorId = (int)f.ProjectSubContractorId, FileUrl = (string)f.FileUrl, OriginalFileName = (string)f.OriginalFileName }).ToList();
            var comparativeFiles = comparativeFilesRaw.Select(f => new { ProjectSubContractorId = (int)f.ProjectSubContractorId, FileUrl = (string)f.FileUrl, OriginalFileName = (string)f.OriginalFileName }).ToList();

            // Build dictionaries for supporting data
            var emailsByContractor = emails.GroupBy(e => e.ContractorId).ToDictionary(g => g.Key, g => g.Select(e => e.Email).ToList());
            var quotationByPsc = quotationFiles.GroupBy(f => f.ProjectSubContractorId).ToDictionary(g => g.Key, g => g.Select(f => new ProjectSubContractorFileDto { FileUrl = f.FileUrl, OriginalFileName = f.OriginalFileName }).ToList());
            var comparativeByPsc = comparativeFiles.GroupBy(f => f.ProjectSubContractorId).ToDictionary(g => g.Key, g => g.Select(f => new ProjectSubContractorFileDto { FileUrl = f.FileUrl, OriginalFileName = f.OriginalFileName }).ToList());

            // Map contractors with emails
            foreach (var contractor in contractors)
                contractor.Emails = emailsByContractor.GetValueOrDefault(contractor.ContractorId, new());

            // Map items from dynamic to DTO
            var items = new List<ProjectSubContractorDTO>();
            foreach (var raw in itemsRaw)
            {
                items.Add(new ProjectSubContractorDTO
                {
                    ProjectSubContractorId = (int)raw.ProjectSubContractorId,
                    ProjectId = (int)raw.ProjectId,
                    ProjectDescription = raw.ProjectDescription ?? "",
                    ContractorId = (int)raw.ContractorId,
                    ContributorId = (int)raw.ContributorId,
                    ContributorName = raw.ContributorName ?? "",
                    ContractId = (int)raw.ContractId,
                    ContractDescription = raw.ContractDescription ?? "",
                    ContractTypeId = (int)raw.ContractTypeId,
                    ContractTypeDescription = raw.ContractTypeDescription ?? "",
                    ContractOriginId = (int)raw.ContractOriginId,
                    ContractOriginDescription = raw.ContractOriginDescription ?? "",
                    PaymentMethodId = (int)raw.PaymentMethodId,
                    PaymentMethodDescription = raw.PaymentMethodDescription ?? "",
                    AdvancePercentage = (decimal?)raw.AdvancePercentage,
                    AdvanceAmount = (decimal?)raw.AdvanceAmount,
                    Amount = (decimal?)raw.Amount ?? 0m,
                    CurrencyId = (int)raw.CurrencyId,
                    CurrencyCode = raw.CurrencyCode ?? "",
                    AmountHasIgv = (bool)raw.HasIgv,
                    WorkItemId = (int)raw.WorkItemId,
                    WorkItemDescription = raw.WorkItemDescription ?? "",
                    WorkItemCategoryId = (int)raw.WorkItemCategoryId,
                    WorkItemCategoryDescription = raw.WorkItemCategoryDescription ?? "",
                    ProjectSubContractorStatusId = (int)raw.ProjectSubContractorStatusId,
                    ProjectSubContractorStatusDescription = raw.ProjectSubContractorStatusDescription ?? "",
                    SigningDate = ToDateOnly(raw.SigningDate),
                    StartDate = ToDateOnly(raw.StartDate),
                    EndDate = ToDateOnly(raw.EndDate),
                    TermDays = (int?)raw.TermDays,
                    ContractNumber = (int?)raw.ContractNumber,
                    PromissoryNoteNumber = (int?)raw.PromissoryNoteNumber,
                    ArrivedWithObservations = (bool?)raw.ArrivedWithObservations,
                    CreatedDateTime = (DateTime)raw.CreatedDateTime,
                    Contract = raw.contract_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.contract_file_url, OriginalFileName = raw.contract_file_name, StatusId = (int?)raw.contract_status_id, StatusDescription = raw.contract_status_desc, Observation = raw.contract_observation } : null,
                    SummarySheet = raw.summary_sheet_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.summary_sheet_file_url, OriginalFileName = raw.summary_sheet_file_name, StatusId = (int?)raw.summary_sheet_status_id, StatusDescription = raw.summary_sheet_status_desc, Observation = raw.summary_sheet_observation } : null,
                    Budget = raw.budget_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.budget_file_url, OriginalFileName = raw.budget_file_name, StatusId = (int?)raw.budget_status_id, StatusDescription = raw.budget_status_desc, Observation = raw.budget_observation } : null,
                    Schedule = raw.schedule_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.schedule_file_url, OriginalFileName = raw.schedule_file_name, StatusId = (int?)raw.schedule_status_id, StatusDescription = raw.schedule_status_desc, Observation = raw.schedule_observation } : null,
                    AttachedQuotation = raw.attached_quotation_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.attached_quotation_file_url, OriginalFileName = raw.attached_quotation_file_name, StatusId = (int?)raw.attached_quotation_status_id, StatusDescription = raw.attached_quotation_status_desc, Observation = raw.attached_quotation_observation } : null,
                    ServiceOrder = raw.service_order_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.service_order_file_url, OriginalFileName = raw.service_order_file_name, StatusId = (int?)raw.service_order_status_id, StatusDescription = raw.service_order_status_desc, Observation = raw.service_order_observation } : null,
                    PromissoryNote = raw.promissory_note_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.promissory_note_file_url, OriginalFileName = raw.promissory_note_file_name, StatusId = (int?)raw.promissory_note_status_id, StatusDescription = raw.promissory_note_status_desc, Observation = raw.promissory_note_observation } : null,
                    Package = raw.package_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.package_file_url, OriginalFileName = raw.package_file_name } : null,
                    Instructivo = raw.instructivo_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.instructivo_file_url, OriginalFileName = raw.instructivo_file_name, StatusId = (int?)raw.instructivo_status_id, StatusDescription = raw.instructivo_status_desc, Observation = raw.instructivo_observation } : null,
                    NonConformingOutput = raw.non_conforming_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.non_conforming_file_url, OriginalFileName = raw.non_conforming_file_name, StatusId = (int?)raw.non_conforming_status_id, StatusDescription = raw.non_conforming_status_desc, Observation = raw.non_conforming_observation } : null,
                    ToleranceChart = raw.tolerance_chart_file_url != null ? new ProjectSubContractorFileDto { FileUrl = raw.tolerance_chart_file_url, OriginalFileName = raw.tolerance_chart_file_name, StatusId = (int?)raw.tolerance_chart_status_id, StatusDescription = raw.tolerance_chart_status_desc, Observation = raw.tolerance_chart_observation } : null,
                });
            }

            var formDataDto = new ProjectSubContractorFormDataDTO
            {
                Projects = projects,
                Contracts = contracts,
                ContractTypes = contractTypes,
                ContractOrigins = contractOrigins,
                PaymentMethods = paymentMethods,
                Currencies = currencies,
                WorkItems = workItems,
                WorkItemCategories = workItemCategories,
                Contributors = contractors
            };

            int totalPages = (totalRecords + pageSize - 1) / pageSize;

            return new ProjectSubContractorPagedWithFiltersDTO
            {
                Paged = new PagedResult<ProjectSubContractorDTO>
                {
                    Page = filter.Page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    TotalPages = totalPages,
                    Data = items
                },
                Filters = formDataDto
            };
        }

        public async Task<AdjudicacionNotificationDataDto> GetNotificationData(int projectSubContractorId)
        {
            var psc = await _context.ProjectSubContractor
                .Include(x => x.QuotationFiles.Where(f => f.State))
                .Include(x => x.ComparativeFiles.Where(f => f.State))
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State);

            if (psc is null)
                throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 1)
                throw new AbrilException("La adjudicación ya fue notificada o no está en estado pendiente.");

            var projectDescription = await _context.Project
                .Where(p => p.ProjectId == psc.ProjectId)
                .Select(p => p.ProjectDescription)
                .FirstOrDefaultAsync() ?? string.Empty;

            var workItem = await _context.WorkItem
                .FirstOrDefaultAsync(w => w.WorkItemId == psc.WorkItemId);

            var contributor = await (
                from ct in _context.Contractor
                join contrib in _context.Contributor on ct.ContributorId equals contrib.ContributorId
                where ct.ContractorId == psc.ContractorId
                select contrib
            ).FirstOrDefaultAsync();

            var allEmails = await _context.StaffProjectEmail
                .Where(s => s.ProjectId == psc.ProjectId && s.State && s.Active)
                .Select(s => new { s.Email, s.StaffProjectEmailTypeId })
                .ToListAsync();

            // Tipo 1 = Staff de obra → matriz + CC | Tipo 2 = Oficina central → solo CC
            // Tipo 3 = Oficina Técnica → no se incluye en la notificación del paso 1
            var staffEmails          = allEmails.Where(e => e.StaffProjectEmailTypeId == 1).Select(e => e.Email).ToList();
            var oficinaCentralEmails = allEmails.Where(e => e.StaffProjectEmailTypeId == 2).Select(e => e.Email).ToList();

            // Todos los correos registrados en la tabla contractor_email para este contratista
            var contractorEmails = await _context.ContractorEmail
                .Where(ce => ce.ContractorId == psc.ContractorId && ce.State && ce.Active)
                .Select(ce => ce.Email)
                .ToListAsync();

            return new AdjudicacionNotificationDataDto
            {
                ProjectSubContractorId       = psc.ProjectSubContractorId,
                ProjectSubContractorStatusId = psc.ProjectSubContractorStatusId,
                ProjectDescription           = projectDescription,
                WorkItemDescription          = workItem?.WorkItemDescription ?? string.Empty,
                ContributorName              = contributor?.ContributorName ?? string.Empty,
                ContractorEmails             = contractorEmails,
                StaffEmails                  = staffEmails,
                OficinaCentralEmails         = oficinaCentralEmails,
                QuotationFiles = psc.QuotationFiles.Select(f => new ProjectSubContractorFileDto
                {
                    FileUrl = f.FileUrl,
                    OriginalFileName = f.OriginalFileName
                }).ToList(),
                ComparativeFiles = psc.ComparativeFiles.Select(f => new ProjectSubContractorFileDto
                {
                    FileUrl = f.FileUrl,
                    OriginalFileName = f.OriginalFileName
                }).ToList()
            };
        }

        public async Task UpdateStatusToSent(int projectSubContractorId, int userId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State);

            if (psc is null)
                throw new AbrilException("La adjudicación no existe.");

            psc.ProjectSubContractorStatusId = 2;
            psc.UpdatedDateTime = DateTime.UtcNow;
            psc.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
        }

        public async Task UpdateStatus(int projectSubContractorId, int statusId, int userId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            psc.ProjectSubContractorStatusId = statusId;
            psc.UpdatedDateTime = DateTime.UtcNow;
            psc.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
        }

        public async Task SetArrivalOptionAsync(int projectSubContractorId, bool arrivedWithObservations, int userId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 5)
                throw new AbrilException("La adjudicación no está en el paso de llegada a oficina central.");

            psc.ArrivedWithObservations = arrivedWithObservations;
            psc.UpdatedDateTime         = DateTime.UtcNow;
            psc.UpdatedUserId           = userId;

            await _context.SaveChangesAsync();
        }

        public async Task ConfirmStep5Async(int projectSubContractorId, bool arrivedWithObservations, int userId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 5)
                throw new AbrilException("La adjudicación no está en el paso de llegada a oficina central.");

            psc.ArrivedWithObservations    = arrivedWithObservations;
            psc.ProjectSubContractorStatusId = 6;
            psc.UpdatedDateTime            = DateTime.UtcNow;
            psc.UpdatedUserId              = userId;

            await _context.SaveChangesAsync();
        }

        public async Task<ScNotificationDataDto> GetScNotificationDataAsync(int projectSubContractorId)
        {
            using var ctx = _factory.CreateDbContext();

            // ----- Tablas y columnas resueltas desde EF (refactor-safe) -----

            // ProjectSubContractor
            string tPsc       = ctx.Table<ProjectSubContractor>();
            string cPscId     = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorId));
            string cPscStatus = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectSubContractorStatusId));
            string cPscCont   = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ContractorId));
            string cPscProj   = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.ProjectId));
            string cPscWi     = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.WorkItemId));
            string cPscState  = ctx.Col<ProjectSubContractor>(nameof(ProjectSubContractor.State));

            // Project
            string tProject  = ctx.Table<ProjectModel>();
            string cProjId   = ctx.Col<ProjectModel>(nameof(ProjectModel.ProjectId));
            string cProjDesc = ctx.Col<ProjectModel>(nameof(ProjectModel.ProjectDescription));

            // WorkItem
            string tWorkItem = ctx.Table<WorkItem>();
            string cWiId     = ctx.Col<WorkItem>(nameof(WorkItem.WorkItemId));
            string cWiDesc   = ctx.Col<WorkItem>(nameof(WorkItem.WorkItemDescription));

            // ContractorEmail
            string tCe       = ctx.Table<ContractorEmail>();
            string cCeContId = ctx.Col<ContractorEmail>(nameof(ContractorEmail.ContractorId));
            string cCeEmail  = ctx.Col<ContractorEmail>(nameof(ContractorEmail.Email));
            string cCeActive = ctx.Col<ContractorEmail>(nameof(ContractorEmail.Active));
            string cCeState  = ctx.Col<ContractorEmail>(nameof(ContractorEmail.State));

            // StaffProjectEmail
            string tSpe       = ctx.Table<StaffProjectEmail>();
            string cSpeProjId = ctx.Col<StaffProjectEmail>(nameof(StaffProjectEmail.ProjectId));
            string cSpeEmail  = ctx.Col<StaffProjectEmail>(nameof(StaffProjectEmail.Email));
            string cSpeTypeId = ctx.Col<StaffProjectEmail>(nameof(StaffProjectEmail.StaffProjectEmailTypeId));
            string cSpeState  = ctx.Col<StaffProjectEmail>(nameof(StaffProjectEmail.State));
            string cSpeActive = ctx.Col<StaffProjectEmail>(nameof(StaffProjectEmail.Active));

            // ----- SQL multi-statement (un solo round-trip) -----

            string sql = $@"
                SELECT psc.{cPscStatus} AS StatusId,
                       p.{cProjDesc}    AS ProjectDescription,
                       wi.{cWiDesc}     AS WorkItemDescription
                  FROM {tPsc} psc
                  JOIN {tProject}  p  ON p.{cProjId} = psc.{cPscProj}
                  JOIN {tWorkItem} wi ON wi.{cWiId}   = psc.{cPscWi}
                 WHERE psc.{cPscId} = @Id AND psc.{cPscState} = TRUE;

                SELECT ce.{cCeEmail}
                  FROM {tCe} ce
                  JOIN {tPsc} psc ON psc.{cPscCont} = ce.{cCeContId}
                 WHERE psc.{cPscId} = @Id AND psc.{cPscState} = TRUE
                   AND ce.{cCeActive} = TRUE AND ce.{cCeState} = TRUE;

                SELECT spe.{cSpeEmail}
                  FROM {tSpe} spe
                  JOIN {tPsc} psc ON psc.{cPscProj} = spe.{cSpeProjId}
                 WHERE psc.{cPscId} = @Id AND psc.{cPscState} = TRUE
                   AND spe.{cSpeTypeId} = 1 AND spe.{cSpeState} = TRUE AND spe.{cSpeActive} = TRUE;";

            // ----- Ejecutar y leer los 3 result sets -----

            var connection = ctx.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var multi = await connection.QueryMultipleAsync(sql, new { Id = projectSubContractorId });

            var head = await multi.ReadSingleOrDefaultAsync<ScNotifHeadDto>()
                ?? throw new AbrilException("La adjudicación no existe.");

            if (head.StatusId != 4)
                throw new AbrilException("La adjudicación no está en estado 'Por enviar al SC'.");

            var contractorEmails = (await multi.ReadAsync<string>()).ToList();
            var staffObraEmails  = (await multi.ReadAsync<string>()).ToList();

            return new ScNotificationDataDto
            {
                ProjectDescription  = head.ProjectDescription,
                WorkItemDescription = head.WorkItemDescription,
                ContractorEmails    = contractorEmails,
                StaffObraEmails     = staffObraEmails
            };
        }

        private sealed class ScNotifHeadDto
        {
            public int    StatusId            { get; init; }
            public string ProjectDescription  { get; init; } = "";
            public string WorkItemDescription { get; init; } = "";
        }

        public async Task<Step6NotificationDataDto> GetStep6NotificationDataAsync(int projectSubContractorId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 6)
                throw new AbrilException("La adjudicación no está en el paso de procesos de firma.");

            var projectDescription = await _context.Project
                .Where(p => p.ProjectId == psc.ProjectId)
                .Select(p => p.ProjectDescription)
                .FirstOrDefaultAsync() ?? string.Empty;

            var contributor = await (
                from ct in _context.Contractor
                join contrib in _context.Contributor on ct.ContributorId equals contrib.ContributorId
                where ct.ContractorId == psc.ContractorId
                select contrib
            ).FirstOrDefaultAsync();

            var contract = await _context.Contract
                .FirstOrDefaultAsync(c => c.ContractId == psc.ContractId);

            var workItem = await _context.WorkItem
                .FirstOrDefaultAsync(w => w.WorkItemId == psc.WorkItemId);

            var staffObraEmails = await _context.StaffProjectEmail
                .Where(s => s.ProjectId == psc.ProjectId && s.StaffProjectEmailTypeId == 1 && s.State && s.Active)
                .Select(s => s.Email)
                .ToListAsync();

            return new Step6NotificationDataDto
            {
                ProjectDescription  = projectDescription,
                ContractDescription = contract?.ContractDescription  ?? string.Empty,
                ContributorName     = contributor?.ContributorName   ?? string.Empty,
                WorkItemDescription = workItem?.WorkItemDescription  ?? string.Empty,
                ContractNumber      = psc.ContractNumber,
                StaffObraEmails     = staffObraEmails
            };
        }

        public async Task<Step8NotificationDataDto> GetStep8NotificationDataAsync(int projectSubContractorId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 8)
                throw new AbrilException("La adjudicación no está en el paso de envío a obra.");

            var projectDescription = await _context.Project
                .Where(p => p.ProjectId == psc.ProjectId)
                .Select(p => p.ProjectDescription)
                .FirstOrDefaultAsync() ?? string.Empty;

            var contributor = await (
                from ct in _context.Contractor
                join contrib in _context.Contributor on ct.ContributorId equals contrib.ContributorId
                where ct.ContractorId == psc.ContractorId
                select contrib
            ).FirstOrDefaultAsync();

            var contract = await _context.Contract
                .FirstOrDefaultAsync(c => c.ContractId == psc.ContractId);

            var ofTecnicaEmails = await _context.StaffProjectEmail
                .Where(s => s.ProjectId == psc.ProjectId && s.StaffProjectEmailTypeId == 3 && s.State && s.Active)
                .Select(s => s.Email)
                .ToListAsync();

            var scannedDocs = await _context.ProjectSubContractorScannedDoc
                .Where(f => f.ProjectSubContractorId == projectSubContractorId && f.State)
                .OrderBy(f => f.Slot)
                .Select(f => new ProjectSubContractorFileDto { FileUrl = f.FileUrl!, OriginalFileName = f.OriginalFileName })
                .ToListAsync();

            return new Step8NotificationDataDto
            {
                ProjectDescription  = projectDescription,
                ContractDescription = contract?.ContractDescription ?? string.Empty,
                ContributorName     = contributor?.ContributorName  ?? string.Empty,
                OfTecnicaEmails     = ofTecnicaEmails,
                ScannedDocs         = scannedDocs
            };
        }

        public async Task SaveDates(int projectSubContractorId, UpdateDatesDTO dto, int userId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State);

            if (psc is null)
                throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 2)
                throw new AbrilException("La adjudicación no está en el paso de datos del contrato.");

            psc.SigningDate = dto.SigningDate;
            psc.StartDate = dto.StartDate;
            psc.EndDate = dto.EndDate;
            psc.ContractNumber       = dto.ContractNumber;
            psc.PromissoryNoteNumber = dto.PromissoryNoteNumber;
            psc.TermDays = (dto.StartDate != default && dto.EndDate != default)
                ? (int)(dto.EndDate.ToDateTime(TimeOnly.MinValue) - dto.StartDate.ToDateTime(TimeOnly.MinValue)).TotalDays
                : null;
            psc.ProjectSubContractorStatusId = 3;
            psc.UpdatedDateTime = DateTime.UtcNow;
            psc.UpdatedUserId = userId;

            await _context.SaveChangesAsync();
        }

        public async Task<AdjudicacionPathDataDto> GetPathDataAsync(int projectSubContractorId)
        {
            using var ctx = _factory.CreateDbContext();

            var data = await (
                from psc in ctx.ProjectSubContractor
                join p      in ctx.Project      on psc.ProjectId    equals p.ProjectId
                join ct     in ctx.Contractor   on psc.ContractorId equals ct.ContractorId
                join contrib in ctx.Contributor on ct.ContributorId equals contrib.ContributorId
                join wi     in ctx.WorkItem     on psc.WorkItemId   equals wi.WorkItemId
                where psc.ProjectSubContractorId == projectSubContractorId && psc.State
                select new AdjudicacionPathDataDto
                {
                    ProjectSubContractorId = psc.ProjectSubContractorId,
                    ProjectDescription     = p.ProjectDescription,
                    Abbreviation           = p.Abbreviation,
                    ContributorRuc         = contrib.ContributorRuc,
                    ContributorName        = contrib.ContributorName,
                    WorkItemDescription    = wi.WorkItemDescription,
                }
            ).FirstOrDefaultAsync();

            if (data is null)
                throw new AbrilException("La adjudicación no existe.");

            return data;
        }

        public async Task SaveDocumentAsync(
            int projectSubContractorId,
            AdjudicacionDocumentType documentType,
            string fileUrl,
            string originalFileName,
            int userId,
            string? sharepointItemId = null)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            var now = DateTimeOffset.UtcNow;

            switch (documentType)
            {
                case AdjudicacionDocumentType.Contract:
                    psc.ProjectSubContractorContractId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorContract,
                        psc.ProjectSubContractorContractId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorContract { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorContractId);
                    break;

                case AdjudicacionDocumentType.SummarySheet:
                    psc.ProjectSubContractorSummarySheetId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorSummarySheet,
                        psc.ProjectSubContractorSummarySheetId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorSummarySheet { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorSummarySheetId);
                    break;

                case AdjudicacionDocumentType.Budget:
                    psc.ProjectSubContractorBudgetId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorBudget,
                        psc.ProjectSubContractorBudgetId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorBudget { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorBudgetId);
                    break;

                case AdjudicacionDocumentType.Schedule:
                    psc.ProjectSubContractorScheduleId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorSchedule,
                        psc.ProjectSubContractorScheduleId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorSchedule { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorScheduleId);
                    break;

                case AdjudicacionDocumentType.AttachedQuotation:
                    psc.ProjectSubContractorAttachedQuotationId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorAttachedQuotation,
                        psc.ProjectSubContractorAttachedQuotationId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorAttachedQuotation { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorAttachedQuotationId);
                    break;

                case AdjudicacionDocumentType.ServiceOrder:
                    psc.ProjectSubContractorServiceOrderId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorServiceOrder,
                        psc.ProjectSubContractorServiceOrderId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorServiceOrder { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorServiceOrderId);
                    break;

                case AdjudicacionDocumentType.PromissoryNote:
                    psc.ProjectSubContractorPromissoryNoteId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorPromissoryNote,
                        psc.ProjectSubContractorPromissoryNoteId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorPromissoryNote { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorPromissoryNoteId);
                    break;

                case AdjudicacionDocumentType.ContractPackage:
                    psc.ProjectSubContractorPackageId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorPackage,
                        psc.ProjectSubContractorPackageId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorPackage { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorPackageId);
                    break;

                case AdjudicacionDocumentType.Instructivo:
                    psc.ProjectSubContractorInstructivoId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorInstructivo,
                        psc.ProjectSubContractorInstructivoId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorInstructivo { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorInstructivoId);
                    break;

                case AdjudicacionDocumentType.NonConformingOutput:
                    psc.ProjectSubContractorNonConformingOutputId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorNonConformingOutput,
                        psc.ProjectSubContractorNonConformingOutputId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorNonConformingOutput { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorNonConformingOutputId);
                    break;

                case AdjudicacionDocumentType.ToleranceChart:
                    psc.ProjectSubContractorToleranceChartId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorToleranceChart,
                        psc.ProjectSubContractorToleranceChartId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.SharepointItemId = sharepointItemId; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorToleranceChart { FileUrl = fileUrl, OriginalFileName = originalFileName, SharepointItemId = sharepointItemId, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorToleranceChartId);
                    break;

                case AdjudicacionDocumentType.ScannedDoc1:
                case AdjudicacionDocumentType.ScannedDoc2:
                case AdjudicacionDocumentType.ScannedDoc3:
                    int scannedSlotSave = documentType switch {
                        AdjudicacionDocumentType.ScannedDoc1 => 1,
                        AdjudicacionDocumentType.ScannedDoc2 => 2,
                        AdjudicacionDocumentType.ScannedDoc3 => 3,
                        _ => throw new AbrilException("Slot inválido.")
                    };
                    var existingScanned = await _context.ProjectSubContractorScannedDoc
                        .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.Slot == scannedSlotSave && x.State);
                    if (existingScanned != null)
                    {
                        existingScanned.FileUrl = fileUrl;
                        existingScanned.OriginalFileName = originalFileName;
                        existingScanned.SharepointItemId = sharepointItemId;
                        existingScanned.UpdatedDatetime = now;
                        existingScanned.UpdatedUserId = userId;
                    }
                    else
                    {
                        _context.ProjectSubContractorScannedDoc.Add(new ProjectSubContractorScannedDoc {
                            ProjectSubContractorId = projectSubContractorId,
                            Slot = scannedSlotSave,
                            FileUrl = fileUrl,
                            OriginalFileName = originalFileName,
                            SharepointItemId = sharepointItemId,
                            CreatedDatetime = now,
                            CreatedUserId = userId,
                            Active = true,
                            State = true
                        });
                    }
                    break;

                default:
                    throw new AbrilException("Tipo de documento no válido.");
            }

            psc.UpdatedDateTime = DateTime.UtcNow;
            psc.UpdatedUserId = userId;
            await _context.SaveChangesAsync();
        }

        public async Task<AdjudicacionSummarySheetDataDto> GetSummarySheetDataAsync(int projectSubContractorId)
        {
            using var ctx = _factory.CreateDbContext();

            var data = await (
                from psc      in ctx.ProjectSubContractor
                join p        in ctx.Project        on psc.ProjectId        equals p.ProjectId
                join ct       in ctx.Contractor     on psc.ContractorId     equals ct.ContractorId
                join contrib  in ctx.Contributor    on ct.ContributorId     equals contrib.ContributorId
                join wi       in ctx.WorkItem       on psc.WorkItemId       equals wi.WorkItemId
                join contract in ctx.Contract       on psc.ContractId       equals contract.ContractId
                join ctype    in ctx.ContractType   on psc.ContractTypeId   equals ctype.ContractTypeId
                join co       in ctx.ContractOrigin on psc.ContractOriginId equals co.ContractOriginId
                join pm       in ctx.PaymentMethod  on psc.PaymentMethodId  equals pm.PaymentMethodId
                join cur      in ctx.Currency       on psc.CurrencyId       equals cur.CurrencyId
                // representante legal del contratista (opcional)
                join personJoin in ctx.Person on contrib.LegalRepresentativePersonId equals personJoin.PersonId into personGroup
                from legalRep in personGroup.DefaultIfEmpty()
                // contributor del proyecto (razón social de Abril — opcional)
                join projContribJoin in ctx.Contributor on p.ContributorId equals projContribJoin.ContributorId into projContribGroup
                from projContrib in projContribGroup.DefaultIfEmpty()
                where psc.ProjectSubContractorId == projectSubContractorId && psc.State
                select new AdjudicacionSummarySheetDataDto
                {
                    ProjectSubContractorId    = psc.ProjectSubContractorId,
                    ProjectDescription        = p.ProjectDescription,
                    Abbreviation              = p.Abbreviation,
                    ProjectDistrict           = p.ProjectDistrict,
                    ProjectRazonSocial                  = projContrib != null ? projContrib.ContributorName : null,
                    ProjectContributorRuc               = projContrib != null ? projContrib.ContributorRuc : null,
                    ProjectLegalEntityRegistryNumber    = projContrib != null ? projContrib.LegalEntityRegistryNumber : null,
                    ContributorName           = contrib.ContributorName,
                    ContributorRuc            = contrib.ContributorRuc,
                    ContributorAddress        = contrib.ContributorAddress,
                    ContributorDistrict       = contrib.ContributorDistrict,
                    ContributorProvince       = contrib.ContributorProvince,
                    ContributorDepartment     = contrib.ContributorDepartment,
                    LegalRepresentativeFullName = legalRep != null ? legalRep.FullName : null,
                    LegalRepresentativeDni    = legalRep != null ? legalRep.DocumentIdentityCode : null,
                    LegalEntityRegistryNumber = contrib.LegalEntityRegistryNumber,
                    WorkItemDescription       = wi.WorkItemDescription,
                    ContractDescription       = contract.ContractDescription,
                    ContractTypeDescription   = ctype.ContractTypeDescription,
                    ContractOriginDescription = co.ContractOriginDescription,
                    PaymentMethodDescription  = pm.PaymentMethodDescription,
                    CurrencyCode              = cur.CurrencyCode,
                    Amount                    = psc.Amount,
                    HasIgv                    = psc.HasIgv,
                    AdvancePercentage         = psc.AdvancePercentage,
                    AdvanceAmount             = psc.AdvanceAmount,
                    TermDays                  = psc.TermDays,
                    SigningDate               = psc.SigningDate,
                    StartDate                 = psc.StartDate,
                    EndDate                   = psc.EndDate,
                    ContractNumber            = psc.ContractNumber,
                    PromissoryNoteNumber      = psc.PromissoryNoteNumber,
                }
            ).FirstOrDefaultAsync();

            if (data is null)
                throw new AbrilException("La adjudicación no existe.");

            // Cargar cláusulas especiales de la partida de control asociada
            var wicId = await ctx.ProjectSubContractor
                .Where(p => p.ProjectSubContractorId == projectSubContractorId && p.State)
                .Select(p => p.WorkItemCategoryId)
                .FirstOrDefaultAsync();

            if (wicId > 0)
            {
                data.SpecialClauses = await ctx.WorkItemCategoryClause
                    .Where(c => c.WorkItemCategoryId == wicId && c.State)
                    .OrderBy(c => c.SortOrder)
                    .Select(c => c.ClauseText)
                    .ToListAsync();
            }

            return data;
        }

        public async Task UpdateDocumentStatusAsync(
            int projectSubContractorId,
            AdjudicacionDocumentType documentType,
            int? statusId,
            string? observation,
            int userId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            var now = DateTimeOffset.UtcNow;

            switch (documentType)
            {
                case AdjudicacionDocumentType.Contract:
                    if (!psc.ProjectSubContractorContractId.HasValue) throw new AbrilException("No existe un registro de Contrato para actualizar.");
                    var contract = await _context.ProjectSubContractorContract.FindAsync(psc.ProjectSubContractorContractId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    contract.ProjectSubContractorFileStatusId = statusId; contract.Observation = observation; contract.UpdatedDatetime = now; contract.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.SummarySheet:
                    if (!psc.ProjectSubContractorSummarySheetId.HasValue) throw new AbrilException("No existe un registro de Hoja Resumen para actualizar.");
                    var summarySheet = await _context.ProjectSubContractorSummarySheet.FindAsync(psc.ProjectSubContractorSummarySheetId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    summarySheet.ProjectSubContractorFileStatusId = statusId; summarySheet.Observation = observation; summarySheet.UpdatedDatetime = now; summarySheet.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.Budget:
                    if (!psc.ProjectSubContractorBudgetId.HasValue) throw new AbrilException("No existe un registro de Presupuesto para actualizar.");
                    var budget = await _context.ProjectSubContractorBudget.FindAsync(psc.ProjectSubContractorBudgetId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    budget.ProjectSubContractorFileStatusId = statusId; budget.Observation = observation; budget.UpdatedDatetime = now; budget.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.Schedule:
                    if (!psc.ProjectSubContractorScheduleId.HasValue) throw new AbrilException("No existe un registro de Cronograma para actualizar.");
                    var schedule = await _context.ProjectSubContractorSchedule.FindAsync(psc.ProjectSubContractorScheduleId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    schedule.ProjectSubContractorFileStatusId = statusId; schedule.Observation = observation; schedule.UpdatedDatetime = now; schedule.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.AttachedQuotation:
                    if (!psc.ProjectSubContractorAttachedQuotationId.HasValue) throw new AbrilException("No existe un registro de Cotización Adjunta para actualizar.");
                    var attachedQuotation = await _context.ProjectSubContractorAttachedQuotation.FindAsync(psc.ProjectSubContractorAttachedQuotationId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    attachedQuotation.ProjectSubContractorFileStatusId = statusId; attachedQuotation.Observation = observation; attachedQuotation.UpdatedDatetime = now; attachedQuotation.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.ServiceOrder:
                    if (!psc.ProjectSubContractorServiceOrderId.HasValue) throw new AbrilException("No existe un registro de Orden de Servicio para actualizar.");
                    var serviceOrder = await _context.ProjectSubContractorServiceOrder.FindAsync(psc.ProjectSubContractorServiceOrderId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    serviceOrder.ProjectSubContractorFileStatusId = statusId; serviceOrder.Observation = observation; serviceOrder.UpdatedDatetime = now; serviceOrder.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.PromissoryNote:
                    if (!psc.ProjectSubContractorPromissoryNoteId.HasValue) throw new AbrilException("No existe un registro de Pagaré para actualizar.");
                    var promissoryNote = await _context.ProjectSubContractorPromissoryNote.FindAsync(psc.ProjectSubContractorPromissoryNoteId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    promissoryNote.ProjectSubContractorFileStatusId = statusId; promissoryNote.Observation = observation; promissoryNote.UpdatedDatetime = now; promissoryNote.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.Instructivo:
                    if (!psc.ProjectSubContractorInstructivoId.HasValue) throw new AbrilException("No existe un registro de Instructivo para actualizar.");
                    var instructivo = await _context.ProjectSubContractorInstructivo.FindAsync(psc.ProjectSubContractorInstructivoId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    instructivo.ProjectSubContractorFileStatusId = statusId; instructivo.Observation = observation; instructivo.UpdatedDatetime = now; instructivo.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.NonConformingOutput:
                    if (!psc.ProjectSubContractorNonConformingOutputId.HasValue) throw new AbrilException("No existe un registro de Salidas No Conforme para actualizar.");
                    var nonConformingOutput = await _context.ProjectSubContractorNonConformingOutput.FindAsync(psc.ProjectSubContractorNonConformingOutputId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    nonConformingOutput.ProjectSubContractorFileStatusId = statusId; nonConformingOutput.Observation = observation; nonConformingOutput.UpdatedDatetime = now; nonConformingOutput.UpdatedUserId = userId;
                    break;

                case AdjudicacionDocumentType.ToleranceChart:
                    if (!psc.ProjectSubContractorToleranceChartId.HasValue) throw new AbrilException("No existe un registro de Cuadro de Tolerancias para actualizar.");
                    var toleranceChart = await _context.ProjectSubContractorToleranceChart.FindAsync(psc.ProjectSubContractorToleranceChartId.Value) ?? throw new AbrilException("Documento no encontrado.");
                    toleranceChart.ProjectSubContractorFileStatusId = statusId; toleranceChart.Observation = observation; toleranceChart.UpdatedDatetime = now; toleranceChart.UpdatedUserId = userId;
                    break;

                default:
                    throw new AbrilException("Tipo de documento no válido.");
            }

            psc.UpdatedDateTime = DateTime.UtcNow;
            psc.UpdatedUserId = userId;
            await _context.SaveChangesAsync();
        }

        public async Task<Step3ApprovalDataDto> GetStep3ApprovalDataAsync(int projectSubContractorId)
        {
            var psc = await _context.ProjectSubContractor
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 3)
                throw new AbrilException("La adjudicación no está en el paso de preparación de documentos.");

            var projectDescription = await _context.Project
                .Where(p => p.ProjectId == psc.ProjectId)
                .Select(p => p.ProjectDescription)
                .FirstOrDefaultAsync() ?? string.Empty;

            var workItem = await _context.WorkItem
                .FirstOrDefaultAsync(w => w.WorkItemId == psc.WorkItemId);

            var contributor = await (
                from ct in _context.Contractor
                join contrib in _context.Contributor on ct.ContributorId equals contrib.ContributorId
                where ct.ContractorId == psc.ContractorId
                select contrib
            ).FirstOrDefaultAsync();

            var ofTecnicaEmails = await _context.StaffProjectEmail
                .Where(s => s.ProjectId == psc.ProjectId && s.StaffProjectEmailTypeId == 3 && s.State && s.Active)
                .Select(s => s.Email)
                .ToListAsync();

            return new Step3ApprovalDataDto
            {
                ProjectDescription  = projectDescription,
                ContributorName     = contributor?.ContributorName ?? string.Empty,
                WorkItemDescription = workItem?.WorkItemDescription ?? string.Empty,
                OfTecnicaEmails     = ofTecnicaEmails,
            };
        }

        public async Task<ContractPackageUrlsDto> GetContractPackageUrlsAsync(int projectSubContractorId)
        {
            using var ctx = _factory.CreateDbContext();

            var ids = await ctx.ProjectSubContractor
                .Where(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                .Select(x => new
                {
                    x.ProjectSubContractorSummarySheetId,
                    x.ProjectSubContractorContractId,
                    x.ProjectSubContractorNonConformingOutputId,
                    x.ProjectSubContractorToleranceChartId,
                    x.ProjectSubContractorInstructivoId,
                    x.ProjectSubContractorPromissoryNoteId,
                    x.ContractNumber,
                })
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("La adjudicación no existe.");

            var summarySheet = ids.ProjectSubContractorSummarySheetId.HasValue
                ? await ctx.ProjectSubContractorSummarySheet
                    .Where(x => x.ProjectSubContractorSummarySheetId == ids.ProjectSubContractorSummarySheetId.Value)
                    .Select(x => new { x.FileUrl, x.SharepointItemId })
                    .FirstOrDefaultAsync()
                : null;

            var contract = ids.ProjectSubContractorContractId.HasValue
                ? await ctx.ProjectSubContractorContract
                    .Where(x => x.ProjectSubContractorContractId == ids.ProjectSubContractorContractId.Value)
                    .Select(x => new { x.FileUrl, x.SharepointItemId })
                    .FirstOrDefaultAsync()
                : null;

            var nonConformingOutput = ids.ProjectSubContractorNonConformingOutputId.HasValue
                ? await ctx.ProjectSubContractorNonConformingOutput
                    .Where(x => x.ProjectSubContractorNonConformingOutputId == ids.ProjectSubContractorNonConformingOutputId.Value)
                    .Select(x => new { x.FileUrl, x.SharepointItemId })
                    .FirstOrDefaultAsync()
                : null;

            var toleranceChart = ids.ProjectSubContractorToleranceChartId.HasValue
                ? await ctx.ProjectSubContractorToleranceChart
                    .Where(x => x.ProjectSubContractorToleranceChartId == ids.ProjectSubContractorToleranceChartId.Value)
                    .Select(x => new { x.FileUrl, x.SharepointItemId })
                    .FirstOrDefaultAsync()
                : null;

            var instructivo = ids.ProjectSubContractorInstructivoId.HasValue
                ? await ctx.ProjectSubContractorInstructivo
                    .Where(x => x.ProjectSubContractorInstructivoId == ids.ProjectSubContractorInstructivoId.Value)
                    .Select(x => new { x.FileUrl, x.SharepointItemId })
                    .FirstOrDefaultAsync()
                : null;

            var promissoryNote = ids.ProjectSubContractorPromissoryNoteId.HasValue
                ? await ctx.ProjectSubContractorPromissoryNote
                    .Where(x => x.ProjectSubContractorPromissoryNoteId == ids.ProjectSubContractorPromissoryNoteId.Value)
                    .Select(x => new { x.FileUrl, x.SharepointItemId })
                    .FirstOrDefaultAsync()
                : null;

            if (string.IsNullOrEmpty(summarySheet?.FileUrl))
                throw new AbrilException("La hoja resumen no ha sido generada. Genérela primero en el paso 3.");

            if (string.IsNullOrEmpty(contract?.FileUrl))
                throw new AbrilException("El contrato no ha sido generado. Genérelo primero en el paso 3.");

            return new ContractPackageUrlsDto
            {
                SummarySheetUrl             = summarySheet.FileUrl,
                SummarySheetItemId          = summarySheet.SharepointItemId,
                ContractUrl                 = contract.FileUrl,
                ContractItemId              = contract.SharepointItemId,
                NonConformingOutputUrl      = nonConformingOutput?.FileUrl,
                NonConformingOutputItemId   = nonConformingOutput?.SharepointItemId,
                ToleranceChartUrl           = toleranceChart?.FileUrl,
                ToleranceChartItemId        = toleranceChart?.SharepointItemId,
                InstructivoUrl              = instructivo?.FileUrl,
                InstructivoItemId           = instructivo?.SharepointItemId,
                PromissoryNoteUrl           = promissoryNote?.FileUrl,
                PromissoryNoteItemId        = promissoryNote?.SharepointItemId,
                ContractNumber              = ids.ContractNumber,
            };
        }

        public async Task<(string FileUrl, string OriginalFileName)?> GetPackageFileInfoAsync(int projectSubContractorId)
        {
            using var ctx = _factory.CreateDbContext();

            var result = await (
                from psc in ctx.ProjectSubContractor
                join pkg in ctx.ProjectSubContractorPackage
                    on psc.ProjectSubContractorPackageId equals pkg.ProjectSubContractorPackageId
                where psc.ProjectSubContractorId == projectSubContractorId && psc.State
                select new { pkg.FileUrl, pkg.OriginalFileName }
            ).FirstOrDefaultAsync();

            return result != null
                ? (result.FileUrl, result.OriginalFileName)
                : null;
        }

        public async Task<(string? FolderId, string? FolderName)?> GetInstructivosFolderAsync(int projectSubContractorId)
        {
            using var ctx = _factory.CreateDbContext();

            var result = await (
                from psc in ctx.ProjectSubContractor
                join wic in ctx.WorkItemCategory
                    on psc.WorkItemCategoryId equals wic.WorkItemCategoryId
                where psc.ProjectSubContractorId == projectSubContractorId && psc.State
                select new { wic.InstructivosFolderId, wic.InstructivosFolderName }
            ).FirstOrDefaultAsync();

            if (result is null) return null;
            return (result.InstructivosFolderId, result.InstructivosFolderName);
        }

        /// <summary>
        /// Actualiza el registro existente si ya hay un ID, o crea uno nuevo y devuelve su ID.
        /// </summary>
        private async Task<int> UpsertDocumentAsync<T>(
            Microsoft.EntityFrameworkCore.DbSet<T> dbSet,
            int? existingId,
            Action<T> update,
            Func<T> create,
            Func<T, int> getId) where T : class
        {
            if (existingId.HasValue)
            {
                var existing = await dbSet.FindAsync(existingId.Value)
                    ?? throw new AbrilException("El documento referenciado no existe.");
                update(existing);
                await _context.SaveChangesAsync();
                return existingId.Value;
            }
            else
            {
                var newDoc = create();
                dbSet.Add(newDoc);
                await _context.SaveChangesAsync();
                return getId(newDoc);
            }
        }
    }
}
