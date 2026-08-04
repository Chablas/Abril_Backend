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
            return services;
        }
    }
}
