using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Services;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.PlaneamientoBimFeature
{
    public static class PlaneamientoBimModule
    {
        public static IServiceCollection AddPlaneamientoBimModule(this IServiceCollection services)
        {
            services.AddScoped<IPlaneamientoBimConfiguracionRepository, PlaneamientoBimConfiguracionRepository>();
            services.AddScoped<IPlaneamientoBimConfiguracionService, PlaneamientoBimConfiguracionService>();

            return services;
        }
    }
}
