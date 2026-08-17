using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Services;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Repositories;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.GestionGthModule
{
    /// <summary>
    /// Módulo Gestión GTH (Talento Humano): Reclutamiento, Onboarding y Base maestra.
    /// Por ahora solo registra la feature de Reclutamiento (formulario de solicitud de personal).
    /// </summary>
    public static class GestionGthModule
    {
        public static IServiceCollection AddGestionGthModule(this IServiceCollection services)
        {
            // Reclutamiento
            services.AddScoped<IReclutamientoRepository, ReclutamientoRepository>();
            services.AddScoped<IReclutamientoService, ReclutamientoService>();

            // Formulario de información del postulante (público por token + revisión de GTH)
            services.AddScoped<IPostulanteFormularioRepository, PostulanteFormularioRepository>();
            services.AddScoped<IPostulanteFormularioService, PostulanteFormularioService>();

            // Aprobación de la solicitud (gerente del área + Gerencia General). El resolver define
            // qué solicitudes ve cada usuario y con qué poder decide, según la categoría de su
            // ficha de trabajador.
            services.AddScoped<IAprobacionScopeResolver, AprobacionScopeResolver>();
            services.AddScoped<IAprobacionGgRepository, AprobacionGgRepository>();
            services.AddScoped<IAprobacionGgService, AprobacionGgService>();

            // Configuración de los correos del flujo (pantalla /solicitud-personal/configuracion)
            services.AddScoped<ICorreoConfigRepository, CorreoConfigRepository>();
            services.AddScoped<ICorreoConfigService, CorreoConfigService>();

            // Destinatarios efectivos de cada correo: lo comparten el envío real y la
            // previsualización del modal, así que no puede haber dos versiones.
            services.AddScoped<ICorreoDestinatariosResolver, CorreoDestinatariosResolver>();

            // Onboarding: la fase que sigue a Reclutamiento (carta oferta → base maestra).
            services.AddScoped<IOnboardingRepository, OnboardingRepository>();
            services.AddScoped<IOnboardingService, OnboardingService>();
            return services;
        }
    }
}
