using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Services;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Repositories;
using Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Application.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Application.Services;
using Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.CroquisFeature.Infrastructure.Repositories;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Services;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.VecinosModule
{
    public static class VecinosModule
    {
        public static IServiceCollection AddVecinosModule(this IServiceCollection services)
        {
            services.AddScoped<IGestionVecinosRepository, GestionVecinosRepository>();
            services.AddScoped<IGestionVecinosService, GestionVecinosService>();
            services.AddScoped<ICroquisRepository, CroquisRepository>();
            services.AddScoped<ICroquisService, CroquisService>();
            services.AddScoped<IControlVencimientosRepository, ControlVencimientosRepository>();
            services.AddScoped<IControlVencimientosService, ControlVencimientosService>();
            return services;
        }
    }
}
