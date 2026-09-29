using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Services;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Repositories;
using Abril_Backend.Shared.Services.Convivir.Configuration;
using Abril_Backend.Shared.Services.Convivir.Interfaces;
using Abril_Backend.Shared.Services.Convivir.Services;

namespace Abril_Backend.Features.ConvivirModule
{
    /// <summary>
    /// Backend de la app móvil Convivir Abril (repo aparte, React Native + Expo): la usan los
    /// propietarios, que entran con el rol VECINO. Hoy solo tiene el acceso (AuthFeature).
    /// </summary>
    public static class ConvivirModule
    {
        public static IServiceCollection AddConvivirModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ConvivirOptions>(configuration.GetSection("Convivir"));

            // Global (Shared/Services/Convivir) porque también lo usan Seguridad → Usuarios y el
            // AuthService de la intranet para mandarle al vecino el enlace de la app y no el de la
            // intranet. Se registra acá para no repartir lo de Convivir por Program.cs.
            services.AddScoped<IConvivirEnlaceService, ConvivirEnlaceService>();

            // AuthFeature
            services.AddScoped<IConvivirAuthRepository, ConvivirAuthRepository>();
            services.AddScoped<IConvivirAuthService, ConvivirAuthService>();

            return services;
        }
    }
}
