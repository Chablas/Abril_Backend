using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

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
            List<(string Url, string OriginalFileName)> quotationFiles,
            List<(string Url, string OriginalFileName)> comparativeFiles,
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
                where psc.State
                select new { psc, p, contractor, c, ct, co, pm, cur, wi, contract, pscs, wic, contractDoc, summarySheetDoc, budgetDoc, scheduleDoc, attachedQuotationDoc, serviceOrderDoc, promissoryNoteDoc };

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
                    Amount = x.psc.Amount,
                    CurrencyId = x.psc.CurrencyId,
                    CurrencyCode = x.cur.CurrencyCode,
                    AmountHasIgv = x.psc.HasIgv,
                    ContractorEmail = x.psc.ContractorEmail,
                    WorkItemId = x.psc.WorkItemId,
                    WorkItemDescription = x.wi.WorkItemDescription,
                    WorkItemCategoryId = x.psc.WorkItemCategoryId,
                    WorkItemCategoryDescription = x.wic.WorkItemCategoryDescription,
                    ProjectSubContractorStatusId = x.pscs.ProjectSubContractorStatusId,
                    ProjectSubContractorStatusDescription = x.pscs.ProjectSubContractorStatusDescription,
                    SigningDate = x.psc.SigningDate,
                    StartDate = x.psc.StartDate,
                    EndDate = x.psc.EndDate,
                    ContractNumber           = x.psc.ContractNumber,
                    ArrivedWithObservations  = x.psc.ArrivedWithObservations,
                    CreatedDateTime          = x.psc.CreatedDateTime,
                    Contract          = x.contractDoc == null          ? null : new ProjectSubContractorFileDto { FileUrl = x.contractDoc.FileUrl!,          OriginalFileName = x.contractDoc.OriginalFileName,          StatusId = x.contractDoc.ProjectSubContractorFileStatusId,          StatusDescription = x.contractDoc.FileStatus == null          ? null : x.contractDoc.FileStatus.ProjectSubContractorFileStatusDescription,          Observation = x.contractDoc.Observation },
                    SummarySheet      = x.summarySheetDoc == null      ? null : new ProjectSubContractorFileDto { FileUrl = x.summarySheetDoc.FileUrl!,      OriginalFileName = x.summarySheetDoc.OriginalFileName,      StatusId = x.summarySheetDoc.ProjectSubContractorFileStatusId,      StatusDescription = x.summarySheetDoc.FileStatus == null      ? null : x.summarySheetDoc.FileStatus.ProjectSubContractorFileStatusDescription,      Observation = x.summarySheetDoc.Observation },
                    Budget            = x.budgetDoc == null            ? null : new ProjectSubContractorFileDto { FileUrl = x.budgetDoc.FileUrl!,            OriginalFileName = x.budgetDoc.OriginalFileName,            StatusId = x.budgetDoc.ProjectSubContractorFileStatusId,            StatusDescription = x.budgetDoc.FileStatus == null            ? null : x.budgetDoc.FileStatus.ProjectSubContractorFileStatusDescription,            Observation = x.budgetDoc.Observation },
                    Schedule          = x.scheduleDoc == null          ? null : new ProjectSubContractorFileDto { FileUrl = x.scheduleDoc.FileUrl!,          OriginalFileName = x.scheduleDoc.OriginalFileName,          StatusId = x.scheduleDoc.ProjectSubContractorFileStatusId,          StatusDescription = x.scheduleDoc.FileStatus == null          ? null : x.scheduleDoc.FileStatus.ProjectSubContractorFileStatusDescription,          Observation = x.scheduleDoc.Observation },
                    AttachedQuotation = x.attachedQuotationDoc == null ? null : new ProjectSubContractorFileDto { FileUrl = x.attachedQuotationDoc.FileUrl!, OriginalFileName = x.attachedQuotationDoc.OriginalFileName, StatusId = x.attachedQuotationDoc.ProjectSubContractorFileStatusId, StatusDescription = x.attachedQuotationDoc.FileStatus == null ? null : x.attachedQuotationDoc.FileStatus.ProjectSubContractorFileStatusDescription, Observation = x.attachedQuotationDoc.Observation },
                    ServiceOrder      = x.serviceOrderDoc == null      ? null : new ProjectSubContractorFileDto { FileUrl = x.serviceOrderDoc.FileUrl!,      OriginalFileName = x.serviceOrderDoc.OriginalFileName,      StatusId = x.serviceOrderDoc.ProjectSubContractorFileStatusId,      StatusDescription = x.serviceOrderDoc.FileStatus == null      ? null : x.serviceOrderDoc.FileStatus.ProjectSubContractorFileStatusDescription,      Observation = x.serviceOrderDoc.Observation },
                    PromissoryNote    = x.promissoryNoteDoc == null    ? null : new ProjectSubContractorFileDto { FileUrl = x.promissoryNoteDoc.FileUrl!,    OriginalFileName = x.promissoryNoteDoc.OriginalFileName,    StatusId = x.promissoryNoteDoc.ProjectSubContractorFileStatusId,    StatusDescription = x.promissoryNoteDoc.FileStatus == null    ? null : x.promissoryNoteDoc.FileStatus.ProjectSubContractorFileStatusDescription,    Observation = x.promissoryNoteDoc.Observation },
                })
                .ToListAsync();

            var ids = items.Select(x => x.ProjectSubContractorId).ToList();

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

        public async Task<AdjudicacionNotificationDataDto> GetNotificationData(int projectSubContractorId)
        {
            var psc = await _context.ProjectSubContractor
                .Include(x => x.Project)
                .Include(x => x.QuotationFiles.Where(f => f.State))
                .Include(x => x.ComparativeFiles.Where(f => f.State))
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State);

            if (psc is null)
                throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 1)
                throw new AbrilException("La adjudicación ya fue notificada o no está en estado pendiente.");

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
                ProjectDescription           = psc.Project.ProjectDescription,
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
            var psc = await _context.ProjectSubContractor
                .Include(x => x.Project)
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 4)
                throw new AbrilException("La adjudicación no está en estado 'Por enviar al SC'.");

            var workItem = await _context.WorkItem
                .FirstOrDefaultAsync(w => w.WorkItemId == psc.WorkItemId);

            var contractorEmails = await _context.ContractorEmail
                .Where(ce => ce.ContractorId == psc.ContractorId && ce.State && ce.Active)
                .Select(ce => ce.Email)
                .ToListAsync();

            var staffObraEmails = await _context.StaffProjectEmail
                .Where(s => s.ProjectId == psc.ProjectId && s.StaffProjectEmailTypeId == 1 && s.State && s.Active)
                .Select(s => s.Email)
                .ToListAsync();

            return new ScNotificationDataDto
            {
                ProjectDescription  = psc.Project.ProjectDescription,
                WorkItemDescription = workItem?.WorkItemDescription ?? string.Empty,
                ContractorEmails    = contractorEmails,
                StaffObraEmails     = staffObraEmails
            };
        }

        public async Task<Step6NotificationDataDto> GetStep6NotificationDataAsync(int projectSubContractorId)
        {
            var psc = await _context.ProjectSubContractor
                .Include(x => x.Project)
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 6)
                throw new AbrilException("La adjudicación no está en el paso de procesos de firma.");

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
                ProjectDescription  = psc.Project.ProjectDescription,
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
                .Include(x => x.Project)
                .FirstOrDefaultAsync(x => x.ProjectSubContractorId == projectSubContractorId && x.State)
                ?? throw new AbrilException("La adjudicación no existe.");

            if (psc.ProjectSubContractorStatusId != 8)
                throw new AbrilException("La adjudicación no está en el paso de envío a obra.");

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
                ProjectDescription  = psc.Project.ProjectDescription,
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
            psc.ContractNumber = dto.ContractNumber;
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
            int userId)
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
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorContract { FileUrl = fileUrl, OriginalFileName = originalFileName, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorContractId);
                    break;

                case AdjudicacionDocumentType.SummarySheet:
                    psc.ProjectSubContractorSummarySheetId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorSummarySheet,
                        psc.ProjectSubContractorSummarySheetId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorSummarySheet { FileUrl = fileUrl, OriginalFileName = originalFileName, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorSummarySheetId);
                    break;

                case AdjudicacionDocumentType.Budget:
                    psc.ProjectSubContractorBudgetId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorBudget,
                        psc.ProjectSubContractorBudgetId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorBudget { FileUrl = fileUrl, OriginalFileName = originalFileName, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorBudgetId);
                    break;

                case AdjudicacionDocumentType.Schedule:
                    psc.ProjectSubContractorScheduleId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorSchedule,
                        psc.ProjectSubContractorScheduleId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorSchedule { FileUrl = fileUrl, OriginalFileName = originalFileName, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorScheduleId);
                    break;

                case AdjudicacionDocumentType.AttachedQuotation:
                    psc.ProjectSubContractorAttachedQuotationId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorAttachedQuotation,
                        psc.ProjectSubContractorAttachedQuotationId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorAttachedQuotation { FileUrl = fileUrl, OriginalFileName = originalFileName, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorAttachedQuotationId);
                    break;

                case AdjudicacionDocumentType.ServiceOrder:
                    psc.ProjectSubContractorServiceOrderId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorServiceOrder,
                        psc.ProjectSubContractorServiceOrderId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorServiceOrder { FileUrl = fileUrl, OriginalFileName = originalFileName, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorServiceOrderId);
                    break;

                case AdjudicacionDocumentType.PromissoryNote:
                    psc.ProjectSubContractorPromissoryNoteId = await UpsertDocumentAsync(
                        _context.ProjectSubContractorPromissoryNote,
                        psc.ProjectSubContractorPromissoryNoteId,
                        e => { e.FileUrl = fileUrl; e.OriginalFileName = originalFileName; e.UpdatedDatetime = now; e.UpdatedUserId = userId; },
                        () => new ProjectSubContractorPromissoryNote { FileUrl = fileUrl, OriginalFileName = originalFileName, CreatedDatetime = now, CreatedUserId = userId, Active = true, State = true },
                        e => e.ProjectSubContractorPromissoryNoteId);
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
                where psc.ProjectSubContractorId == projectSubContractorId && psc.State
                select new AdjudicacionSummarySheetDataDto
                {
                    ProjectSubContractorId  = psc.ProjectSubContractorId,
                    ProjectDescription      = p.ProjectDescription,
                    ContributorName         = contrib.ContributorName,
                    ContributorRuc          = contrib.ContributorRuc,
                    WorkItemDescription     = wi.WorkItemDescription,
                    ContractDescription     = contract.ContractDescription,
                    ContractTypeDescription = ctype.ContractTypeDescription,
                    ContractOriginDescription = co.ContractOriginDescription,
                    PaymentMethodDescription  = pm.PaymentMethodDescription,
                    CurrencyCode            = cur.CurrencyCode,
                    Amount                  = psc.Amount,
                    HasIgv                  = psc.HasIgv,
                    AdvancePercentage       = psc.AdvancePercentage,
                    SigningDate             = psc.SigningDate,
                    StartDate               = psc.StartDate,
                    EndDate                 = psc.EndDate,
                    ContractNumber          = psc.ContractNumber,
                }
            ).FirstOrDefaultAsync();

            if (data is null)
                throw new AbrilException("La adjudicación no existe.");

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

                default:
                    throw new AbrilException("Tipo de documento no válido.");
            }

            psc.UpdatedDateTime = DateTime.UtcNow;
            psc.UpdatedUserId = userId;
            await _context.SaveChangesAsync();
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