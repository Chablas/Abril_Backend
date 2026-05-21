using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Repositories
{
    public class ContractorRegistrationRepository : IContractorRegistrationRepository
    {
        private const int PendingContractorStateId = 1;

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<ContractorRegistrationRepository> _logger;

        public ContractorRegistrationRepository(
            IDbContextFactory<AppDbContext> factory,
            ILogger<ContractorRegistrationRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<List<ContractorPersonTypeDto>> GetPersonTypes()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.ContractorPersonType
                .Where(t => t.State)
                .OrderBy(t => t.ContractorPersonTypeId)
                .Select(t => new ContractorPersonTypeDto
                {
                    ContractorPersonTypeId = t.ContractorPersonTypeId,
                    Description            = t.Description,
                })
                .ToListAsync();
        }

        public async Task Create(ContributorCreateDto dto, int? userId, string? logoUrl, string? brochureUrl, string? fichaRucUrl, string? referencesUrl)
        {
            try
            {
            using var ctx = _factory.CreateDbContext();

            // Resolve legal representative → find or create Person by DNI
            int? legalRepresentativePersonId = null;
            if (!string.IsNullOrWhiteSpace(dto.LegalRepresentativeDni))
            {
                var person = await ctx.Person
                    .FirstOrDefaultAsync(p => p.DocumentIdentityCode == dto.LegalRepresentativeDni && p.State);

                if (person == null)
                {
                    person = new Person
                    {
                        DocumentIdentityCode = dto.LegalRepresentativeDni,
                        FullName = dto.LegalRepresentativeFullName,
                        Active = true,
                        State = true,
                        CreatedDateTime = DateTime.UtcNow,
                    };
                    ctx.Person.Add(person);
                    await ctx.SaveChangesAsync();
                }

                legalRepresentativePersonId = person.PersonId;
            }

            var contributor = await ctx.Contributor.FirstOrDefaultAsync(c => c.ContributorRuc == dto.ContributorRuc && c.State);
            if (contributor == null)
            {
                contributor = new Contributor
                {
                    ContributorRuc = dto.ContributorRuc,
                    ContributorName = dto.ContributorName,
                    ContributorAddress = dto.ContributorAddress,
                    ContributorEconomicActivityDescription = dto.ContributorEconomicActivityDescription,
                    ContributorDistrict             = dto.ContributorDistrict,
                    ContributorProvince             = dto.ContributorProvince,
                    ContributorDepartment           = dto.ContributorDepartment,
                    LegalRepresentativePersonId     = legalRepresentativePersonId,
                    LegalEntityRegistryNumber       = dto.LegalEntityRegistryNumber,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                };
                ctx.Contributor.Add(contributor);
                await ctx.SaveChangesAsync();
            }
            else
            {
                // Update legal representative data if provided
                if (legalRepresentativePersonId.HasValue)
                    contributor.LegalRepresentativePersonId = legalRepresentativePersonId;
                if (!string.IsNullOrWhiteSpace(dto.LegalEntityRegistryNumber))
                    contributor.LegalEntityRegistryNumber = dto.LegalEntityRegistryNumber;
                contributor.UpdatedDateTime = DateTimeOffset.UtcNow;
                await ctx.SaveChangesAsync();
            }

            var contractor = new Contractor
            {
                ContributorId         = contributor.ContributorId,
                ContractorStateId     = PendingContractorStateId,
                LogoFileUrl           = logoUrl,
                BrochureFileUrl       = brochureUrl,
                FichaRucFileUrl       = fichaRucUrl,
                ReferencesListFileUrl = referencesUrl,
                CreatedDateTime       = DateTimeOffset.UtcNow,
                CreatedUserId         = userId,
                Active = true,
                State  = true
            };

            for (int i = 0; i < dto.ContributorEmails.Count; i++)
            {
                int? personTypeId = null;
                if (i < dto.ContributorEmailPersonTypeIds.Count
                    && int.TryParse(dto.ContributorEmailPersonTypeIds[i], out var ptId))
                    personTypeId = ptId;

                var emailNorm = dto.ContributorEmails[i].Trim().ToLower();
                var linkedUser = await ctx.User
                    .FirstOrDefaultAsync(u => u.Email == emailNorm && u.Active && u.State);

                contractor.Emails.Add(new ContractorEmail
                {
                    Email                  = dto.ContributorEmails[i],
                    ContractorPersonTypeId = personTypeId,
                    CreatedDateTime        = DateTimeOffset.UtcNow,
                    CreatedUserId          = userId,
                    UserId                 = linkedUser?.UserId,
                    Active = true,
                    State  = true
                });
            }

            ctx.Contractor.Add(contractor);
            await ctx.SaveChangesAsync();
            } // end try
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR REGISTRO CONTRATISTA: {msg}", ex.ToString());
                throw;
            }
        }

        public async Task<int> ValidateAndGetAttemptNumberAsync(string ruc)
        {
            using var ctx = _factory.CreateDbContext();

            var contributor = await ctx.Contributor
                .FirstOrDefaultAsync(c => c.ContributorRuc == ruc && c.State);

            if (contributor == null)
                return 1; // Primera vez que este RUC se registra

            // Todos los contractor records para este contributor (históricos + activos)
            var allContractors = await ctx.Contractor
                .Where(c => c.ContributorId == contributor.ContributorId)
                .OrderByDescending(c => c.CreatedDateTime)
                .ToListAsync();

            if (!allContractors.Any())
                return 1;

            // Revisar el estado del más reciente activo
            var latestActive = allContractors.FirstOrDefault(c => c.State);
            if (latestActive != null)
            {
                if (latestActive.ContractorStateId == 1)
                    throw new AbrilException(
                        "Tu empresa ya tiene una solicitud de registro pendiente de revisión. " +
                        "Por favor espera a ser contactado por Abril Grupo Inmobiliario.", 400);

                if (latestActive.ContractorStateId == 2)
                    throw new AbrilException(
                        "Tu empresa ya se encuentra aprobada como contratista de Abril Grupo Inmobiliario. " +
                        "Si tienes alguna consulta, por favor contáctanos directamente.", 400);

                // ContractorStateId == 3 (No aprobado) → se permite un nuevo intento
            }

            // Número de intento = total de registros históricos + 1
            return allContractors.Count + 1;
        }
    }
}
