using Abril_Backend.Features.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Adjudicaciones.Infrastructure.Repositories;
using Abril_Backend.Features.Adjudicaciones.Application.Interfaces;
using Abril_Backend.Features.Adjudicaciones.Application.Services;

namespace Abril_Backend.Features.Adjudicaciones
{
    public static class AdjudicacionesModule
    {
        public static IServiceCollection AddAdjudicacionesModule(this IServiceCollection services)
        {
            services.AddScoped<IProjectSubContractorRepository, ProjectSubContractorRepository>();
            services.AddScoped<IProjectSubContractorService, ProjectSubContractorService>();
            return services;
        }
    }
}
