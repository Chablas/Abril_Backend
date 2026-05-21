using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces
{
    public interface IContractorRegistrationRepository
    {
        Task<List<ContractorPersonTypeDto>> GetPersonTypes();
        /// <summary>
        /// Verifica si el RUC puede enviar una nueva solicitud y devuelve el número de intento
        /// (1 para primer registro, 2+ para re-intentos tras rechazo).
        /// Lanza AbrilException si el contratista ya tiene una solicitud en espera o aprobada.
        /// </summary>
        Task<int> ValidateAndGetAttemptNumberAsync(string ruc);
        Task Create(ContributorCreateDto dto, int? userId, string? logoUrl, string? brochureUrl, string? fichaRucUrl, string? referencesUrl);
    }
}
