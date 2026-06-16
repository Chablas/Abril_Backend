using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Services;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.VecinosModule
{
    public static class VecinosModule
    {
        public static IServiceCollection AddVecinosModule(this IServiceCollection services)
        {
            services.AddScoped<IGestionVecinosRepository, GestionVecinosRepository>();
            services.AddScoped<IGestionVecinosService, GestionVecinosService>();
            return services;
        }
    }
}
