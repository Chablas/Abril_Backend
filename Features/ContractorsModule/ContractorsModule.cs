using Abril_Backend.Features.Contractors.ContractorManagement.Application.Interfaces;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Services;
using Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Interfaces;
using Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Repositories;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Interfaces;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Services;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Interfaces;
using Abril_Backend.Features.Contractors.ContractorRegistration.Infrastructure.Repositories;

namespace Abril_Backend.Features.Contractors
{
    public static class ContractorsModule
    {
        public static IServiceCollection AddContractorsModule(this IServiceCollection services)
        {
            // ContractorRegistration
            services.AddScoped<IContractorRegistrationRepository, ContractorRegistrationRepository>();
            services.AddScoped<IContractorRegistrationService, ContractorRegistrationService>();

            // ContractorManagement
            services.AddScoped<IContractorManagementRepository, ContractorManagementRepository>();
            services.AddScoped<IContractorManagementService, ContractorManagementService>();

            return services;
        }
    }
}
