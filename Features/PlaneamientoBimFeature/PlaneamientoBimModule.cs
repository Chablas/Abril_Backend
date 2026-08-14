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

            services.AddScoped<IPlaneamientoBimCargaDiariaRepository, PlaneamientoBimCargaDiariaRepository>();
            services.AddScoped<IPlaneamientoBimCargaDiariaService, PlaneamientoBimCargaDiariaService>();

            services.AddScoped<IPlaneamientoBimBloqueoRepository, PlaneamientoBimBloqueoRepository>();
            services.AddScoped<IPlaneamientoBimBloqueoService, PlaneamientoBimBloqueoService>();

            return services;
        }
    }
}
