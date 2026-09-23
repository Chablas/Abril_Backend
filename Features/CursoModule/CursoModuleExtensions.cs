using Abril_Backend.Features.CursoModule.Application.Interfaces;
using Abril_Backend.Features.CursoModule.Application.Services;
using Abril_Backend.Features.CursoModule.Infrastructure.Repositories;

namespace Abril_Backend.Features.CursoModule
{
    /// <summary>
    /// Cursos de capacitación interactivos (tipo Genially) con evaluación auditable para
    /// SUNAFIL. Registra repositorios y el servicio de corrección/finalización de intentos.
    /// </summary>
    public static class CursoModuleExtensions
    {
        public static IServiceCollection AddCursoModule(this IServiceCollection services)
        {
            services.AddScoped<ICursoRepository, CursoRepository>();
            services.AddScoped<ICursoIntentoRepository, CursoIntentoRepository>();
            services.AddScoped<ICursoIntentoService, CursoIntentoService>();
            return services;
        }
    }
}
