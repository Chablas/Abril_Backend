using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Interfaces;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Services;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.PropietariosModule
{
    /// <summary>
    /// Propietarios de inmuebles de Abril: la intranet los da de alta (persona, usuario con el rol
    /// PROPIETARIO y sus propiedades) para que entren a la app Convivir Abril (ConvivirModule).
    /// </summary>
    public static class PropietariosModule
    {
        public static IServiceCollection AddPropietariosModule(this IServiceCollection services)
        {
            // GestionPropietariosFeature
            services.AddScoped<IGestionPropietariosRepository, GestionPropietariosRepository>();
            services.AddScoped<IGestionPropietariosService, GestionPropietariosService>();

            return services;
        }
    }
}
