using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Services;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Repositories;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Services;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Infrastructure.Repositories;
using Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Services;
using Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Services;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Services;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Infrastructure.Repositories;
using Abril_Backend.Features.ConvivirModule.Shared.Interfaces;
using Abril_Backend.Features.ConvivirModule.Shared.Repositories;
using Abril_Backend.Shared.Services.Convivir.Configuration;
using Abril_Backend.Shared.Services.Convivir.Interfaces;
using Abril_Backend.Shared.Services.Convivir.Services;

namespace Abril_Backend.Features.ConvivirModule
{
    /// <summary>
    /// Backend de la app móvil Convivir Abril (repo aparte, React Native + Expo): la usan los
    /// propietarios, que entran con su DNI y el rol PROPIETARIO. Cada feature es una pantalla de
    /// la app. Los propietarios y sus propiedades se dan de alta en la intranet (PropietariosModule).
    /// </summary>
    public static class ConvivirModule
    {
        public static IServiceCollection AddConvivirModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ConvivirOptions>(configuration.GetSection("Convivir"));
            services.PostConfigure<ConvivirOptions>(ConvivirOptions.ReemplazarIpDeLaPc);

            // Global (Shared/Services/Convivir) porque también lo usan Seguridad → Usuarios, el
            // módulo Propietarios y el AuthService de la intranet para mandarle al propietario el
            // enlace de la app y no el de la intranet. Se registra acá para no repartir lo de
            // Convivir por Program.cs.
            services.AddScoped<IConvivirEnlaceService, ConvivirEnlaceService>();

            // Global por lo mismo: los documentos del propietario los sube el módulo Propietarios
            // y los descarga la app.
            services.AddScoped<IPropietarioDocumentoStorage, PropietarioDocumentoStorage>();

            // Shared del módulo: propiedades y avance del propietario (Inicio y Mi Proyecto)
            services.AddScoped<IConvivirPropiedadesRepository, ConvivirPropiedadesRepository>();

            // AuthFeature
            services.AddScoped<IConvivirAuthRepository, ConvivirAuthRepository>();
            services.AddScoped<IConvivirAuthService, ConvivirAuthService>();

            // InicioFeature
            services.AddScoped<IConvivirInicioService, ConvivirInicioService>();

            // MiProyectoFeature
            services.AddScoped<IConvivirMiProyectoService, ConvivirMiProyectoService>();

            // DocumentosFeature
            services.AddScoped<IConvivirDocumentosRepository, ConvivirDocumentosRepository>();
            services.AddScoped<IConvivirDocumentosService, ConvivirDocumentosService>();

            // NotificacionesFeature (la campana)
            services.AddScoped<IConvivirNotificacionesRepository, ConvivirNotificacionesRepository>();
            services.AddScoped<IConvivirNotificacionesService, ConvivirNotificacionesService>();

            return services;
        }
    }
}
