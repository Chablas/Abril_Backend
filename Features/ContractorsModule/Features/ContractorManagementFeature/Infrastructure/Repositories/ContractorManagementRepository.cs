using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Repositories
{
    public class ContractorManagementRepository : IContractorManagementRepository
    {
        private const int PendingContractorStateId = 1;
        private const int ApprovedContractorStateId = 2;
        private const int RejectedContractorStateId = 3;

        private readonly IDbContextFactory<AppDbContext> _factory;

        public ContractorManagementRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PagedResult<ContributorPagedDto>> GetPaged(ContributorFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            const int pageSize = 10;

            var query =
                from ct in ctx.Contractor
                join c in ctx.Contributor on ct.ContributorId equals c.ContributorId
                join cs in ctx.ContractorState on ct.ContractorStateId equals cs.ContractorStateId
                join p in ctx.Person on c.LegalRepresentativePersonId equals p.PersonId into personJoin
                from p in personJoin.DefaultIfEmpty()
                where ct.Active && ct.State
                select new { ct, c, cs, p };

            if (!string.IsNullOrWhiteSpace(filter.ContributorName))
                query = query.Where(x => x.c.ContributorName.ToLower().Contains(filter.ContributorName.ToLower()));

            if (!string.IsNullOrWhiteSpace(filter.ContributorRuc))
                query = query.Where(x => x.c.ContributorRuc.Contains(filter.ContributorRuc));

            if (filter.ContractorStateId.HasValue)
                query = query.Where(x => x.ct.ContractorStateId == filter.ContractorStateId.Value);

            if (!string.IsNullOrWhiteSpace(filter.LegalRepresentativeDni))
                query = query.Where(x => x.p != null && x.p.DocumentIdentityCode != null &&
                    x.p.DocumentIdentityCode.Contains(filter.LegalRepresentativeDni));

            if (!string.IsNullOrWhiteSpace(filter.LegalRepresentativeName))
                query = query.Where(x => x.p != null && x.p.FullName != null &&
                    x.p.FullName.ToLower().Contains(filter.LegalRepresentativeName.ToLower()));

            if (!string.IsNullOrWhiteSpace(filter.LegalEntityRegistryNumber))
                query = query.Where(x => x.c.LegalEntityRegistryNumber != null &&
                    x.c.LegalEntityRegistryNumber.Contains(filter.LegalEntityRegistryNumber));

            var totalRecords = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.ct.ContractorId)
                .Skip((filter.Page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ContributorPagedDto
                {
                    ContractorId = x.ct.ContractorId,
                    ContributorId = x.c.ContributorId,
                    ContributorRuc = x.c.ContributorRuc,
                    ContributorName = x.c.ContributorName,
                    ContributorAddress = x.c.ContributorAddress,
                    ContributorEconomicActivityDescription = x.c.ContributorEconomicActivityDescription,
                    ContributorDistrict   = x.c.ContributorDistrict,
                    ContributorProvince   = x.c.ContributorProvince,
                    ContributorDepartment = x.c.ContributorDepartment,
                    LegalRepresentativeDni      = x.p != null ? x.p.DocumentIdentityCode : null,
                    LegalRepresentativeFullName = x.p != null ? x.p.FullName : null,
                    LegalEntityRegistryNumber   = x.c.LegalEntityRegistryNumber,
                    ContractorStateId = x.cs.ContractorStateId,
                    ContractorStateDescription = x.cs.ContractorStateDescription,
                    LogoFileUrl           = x.ct.LogoFileUrl,
                    BrochureFileUrl       = x.ct.BrochureFileUrl,
                    FichaRucFileUrl       = x.ct.FichaRucFileUrl,
                    ReferencesListFileUrl = x.ct.ReferencesListFileUrl,
                    HasUser = ctx.ContractorUser.Any(cu => cu.ContractorId == x.ct.ContractorId && cu.Active),
                    CreatedDateTime = x.ct.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime
                })
                .ToListAsync();

            var ids = items.Select(c => c.ContractorId).ToList();

            var emails = await ctx.ContractorEmail
                .Where(e => ids.Contains(e.ContractorId) && e.State)
                .Select(e => new { e.ContractorId, e.Email })
                .ToListAsync();

            var emailsByContractor = emails
                .GroupBy(e => e.ContractorId)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Email).ToList());

            foreach (var item in items)
                item.Emails = emailsByContractor.GetValueOrDefault(item.ContractorId, new());

            var users = await (
                from cu in ctx.ContractorUser
                join u in ctx.User on cu.UserId equals u.UserId
                where ids.Contains(cu.ContractorId) && cu.Active
                select new { cu.ContractorId, cu.UserId, u.Email, cu.CreatedDateTime }
            ).ToListAsync();

            var usersByContractor = users
                .GroupBy(u => u.ContractorId)
                .ToDictionary(g => g.Key, g => g.Select(u => new ContractorUserItemDto
                {
                    UserId = u.UserId,
                    Email = u.Email,
                    CreatedDateTime = u.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime
                }).ToList());

            foreach (var item in items)
                item.Users = usersByContractor.GetValueOrDefault(item.ContractorId, new());

            return new PagedResult<ContributorPagedDto>
            {
                Page = filter.Page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = items
            };
        }

        public async Task Approve(int contractorId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var contractor = await ctx.Contractor
                .FirstOrDefaultAsync(c => c.ContractorId == contractorId && c.ContractorStateId == PendingContractorStateId);
            if (contractor is null) return;

            contractor.ContractorStateId = ApprovedContractorStateId;
            contractor.UpdatedDateTime = DateTimeOffset.UtcNow;
            contractor.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        public async Task Reject(int contractorId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contractor = await ctx.Contractor.FirstOrDefaultAsync(c => c.ContractorId == contractorId && c.ContractorStateId == PendingContractorStateId);
            if (contractor is null) return;
            contractor.ContractorStateId = RejectedContractorStateId;
            contractor.UpdatedDateTime = DateTimeOffset.UtcNow;
            contractor.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task<ContractorWithEmailsDto?> GetWithEmails(int contractorId)
        {
            using var ctx = _factory.CreateDbContext();

            var result = await (
                from ct in ctx.Contractor
                join c in ctx.Contributor on ct.ContributorId equals c.ContributorId
                where ct.ContractorId == contractorId && ct.Active && ct.State
                select new ContractorWithEmailsDto
                {
                    ContractorId = ct.ContractorId,
                    ContributorName = c.ContributorName,
                    ContributorRuc = c.ContributorRuc,
                    ContractorStateId = ct.ContractorStateId
                }
            ).FirstOrDefaultAsync();

            if (result == null) return null;

            result.Emails = await ctx.ContractorEmail
                .Where(e => e.ContractorId == contractorId && e.State)
                .Select(e => e.Email)
                .ToListAsync();

            return result;
        }

        public async Task Update(
            int contractorId,
            ContractorUpdateDto dto,
            string? logoUrl,
            string? brochureUrl,
            string? fichaRucUrl,
            string? referencesUrl,
            int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var contractor = await ctx.Contractor
                .FirstOrDefaultAsync(c => c.ContractorId == contractorId && c.Active && c.State)
                ?? throw new Exception("Contratista no encontrado.");

            var contributor = await ctx.Contributor
                .FirstOrDefaultAsync(c => c.ContributorId == contractor.ContributorId)
                ?? throw new Exception("Contribuyente no encontrado.");

            // ── Campos texto del Contributor ─────────────────────────────────────
            contributor.ContributorName                         = dto.ContributorName.Trim();
            contributor.ContributorAddress                      = dto.ContributorAddress.Trim();
            contributor.ContributorEconomicActivityDescription  = dto.ContributorEconomicActivityDescription.Trim();
            contributor.ContributorDistrict                     = dto.ContributorDistrict?.Trim();
            contributor.ContributorProvince                     = dto.ContributorProvince?.Trim();
            contributor.ContributorDepartment                   = dto.ContributorDepartment?.Trim();
            contributor.LegalEntityRegistryNumber               = dto.LegalEntityRegistryNumber?.Trim();
            contributor.UpdatedDateTime                         = DateTimeOffset.UtcNow;
            contributor.UpdatedUserId                           = userId;

            // ── Representante legal (Person) ──────────────────────────────────────
            if (contributor.LegalRepresentativePersonId.HasValue)
            {
                var person = await ctx.Person
                    .FirstOrDefaultAsync(p => p.PersonId == contributor.LegalRepresentativePersonId.Value);
                if (person is not null)
                {
                    person.DocumentIdentityCode = dto.LegalRepresentativeDni?.Trim();
                    person.FullName             = dto.LegalRepresentativeFullName?.Trim();
                    person.UpdatedDateTime      = DateTime.UtcNow;
                    person.UpdatedUserId        = userId;
                }
            }
            else if (!string.IsNullOrWhiteSpace(dto.LegalRepresentativeDni)
                  || !string.IsNullOrWhiteSpace(dto.LegalRepresentativeFullName))
            {
                var dni = dto.LegalRepresentativeDni?.Trim();

                // Si ya existe una persona con ese DNI (aunque no esté asociada a este contratista),
                // se reutiliza y se le actualiza el nombre — así no se viola la restricción única
                // person_document_identity_code_key al intentar insertar un duplicado.
                var existingPerson = !string.IsNullOrWhiteSpace(dni)
                    ? await ctx.Person.FirstOrDefaultAsync(p => p.DocumentIdentityCode == dni)
                    : null;

                if (existingPerson is not null)
                {
                    existingPerson.FullName        = dto.LegalRepresentativeFullName?.Trim();
                    existingPerson.UpdatedDateTime = DateTime.UtcNow;
                    existingPerson.UpdatedUserId   = userId;
                    contributor.LegalRepresentativePersonId = existingPerson.PersonId;
                }
                else
                {
                    var newPerson = new Abril_Backend.Infrastructure.Models.Person
                    {
                        DocumentIdentityCode = dni,
                        FullName             = dto.LegalRepresentativeFullName?.Trim(),
                        CreatedDateTime      = DateTime.UtcNow,
                        CreatedUserId        = userId,
                        Active               = true,
                        State                = true
                    };
                    ctx.Person.Add(newPerson);
                    await ctx.SaveChangesAsync();   // flush para obtener PersonId
                    contributor.LegalRepresentativePersonId = newPerson.PersonId;
                }
            }

            // ── Archivos (null = conservar el existente) ──────────────────────────
            if (logoUrl       is not null) contractor.LogoFileUrl          = logoUrl;
            if (brochureUrl   is not null) contractor.BrochureFileUrl      = brochureUrl;
            if (fichaRucUrl   is not null) contractor.FichaRucFileUrl      = fichaRucUrl;
            if (referencesUrl is not null) contractor.ReferencesListFileUrl = referencesUrl;

            contractor.UpdatedDateTime = DateTimeOffset.UtcNow;
            contractor.UpdatedUserId   = userId;

            // ── Sincronizar correos ───────────────────────────────────────────────
            // 'State' es el flag de soft-delete: ningún registro se elimina físicamente.
            //   - Quitar un correo  → State = false (queda en BD para auditoría).
            //   - Volver a agregarlo → se INSERTA un registro nuevo (no se reactiva el viejo).
            // Solo los registros vigentes (State == true) cuentan como "la lista actual".
            // Existe un índice único parcial (contractor_id, correo) WHERE state = true que
            // garantiza un único registro vigente por correo EXACTO, permitiendo múltiples
            // históricos con State = false.
            // La comparación es sensible a mayúsculas (Ordinal): 'Correo@x.pe' y 'correo@x.pe'
            // se tratan como correos distintos.
            var activeEmails = await ctx.ContractorEmail
                .Where(e => e.ContractorId == contractorId && e.State)
                .ToListAsync();

            var activeSet = activeEmails
                .Select(e => e.Email.Trim())
                .ToHashSet(StringComparer.Ordinal);

            var newSet = dto.Emails
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            var newSetExact = newSet.ToHashSet(StringComparer.Ordinal);

            // Soft-delete: los vigentes que ya no están en la nueva lista → State = false
            foreach (var email in activeEmails.Where(e => !newSetExact.Contains(e.Email.Trim())))
            {
                email.State            = false;
                email.Active           = false;
                email.UpdatedDateTime  = DateTimeOffset.UtcNow;
                email.UpdatedUserId    = userId;
            }

            // Insertar los nuevos (nunca reactivar un registro soft-deleted)
            foreach (var newEmail in newSet)
            {
                if (activeSet.Contains(newEmail))
                    continue;   // ya existe vigente exacto → sin cambios

                ctx.ContractorEmail.Add(new Abril_Backend.Features.CostsModule.Shared.Models.ContractorEmail
                {
                    ContractorId    = contractorId,
                    Email           = newEmail,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId   = userId,
                    Active          = true,
                    State           = true
                });
            }

            await ctx.SaveChangesAsync();
        }

        public async Task SetActivationToken(int contractorId, string token, DateTime expiry)
        {
            using var ctx = _factory.CreateDbContext();
            var contractor = await ctx.Contractor
                .FirstOrDefaultAsync(c => c.ContractorId == contractorId && c.Active && c.State);
            if (contractor is null) return;
            contractor.ActivationToken = token;
            contractor.ActivationTokenExpiry = expiry;
            contractor.UpdatedDateTime = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }
    }
}
