using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Repositories;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Services;

namespace Abril_Backend.Features.Costs
{
    public static class CostsModule
    {
        public static IServiceCollection AddCostsModule(this IServiceCollection services)
        {
            services.AddScoped<IProjectSubContractorRepository, ProjectSubContractorRepository>();
            services.AddScoped<IProjectSubContractorService, ProjectSubContractorService>();
            return services;
        }
    }
}
