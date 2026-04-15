using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Repositories;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Interfaces;
using Abril_Backend.Features.Costs.Adjudicaciones.Application.Services;
using Abril_Backend.Features.CostsModule.Features.Configuration.StaffProjectEmailFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.CostsModule.Features.Configuration.StaffProjectEmailFeature.Infrastructure.Repositories;
using Abril_Backend.Features.CostsModule.Features.Configuration.StaffProjectEmailFeature.Application.Interfaces;
using Abril_Backend.Features.CostsModule.Features.Configuration.StaffProjectEmailFeature.Application.Services;

namespace Abril_Backend.Features.Costs
{
    public static class CostsModule
    {
        public static IServiceCollection AddCostsModule(this IServiceCollection services)
        {
            services.AddScoped<IProjectSubContractorRepository, ProjectSubContractorRepository>();
            services.AddScoped<IProjectSubContractorService, ProjectSubContractorService>();

            services.AddScoped<IStaffProjectEmailRepository, StaffProjectEmailRepository>();
            services.AddScoped<IStaffProjectEmailService, StaffProjectEmailService>();

            return services;
        }
    }
}
