using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Application.Services;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Infrastructure.Repositories;

namespace Abril_Backend.Features.UnidadDeProyectosModule
{
    public static class UnidadDeProyectosModule
    {
        public static IServiceCollection AddUnidadDeProyectosModule(this IServiceCollection services)
        {
            // LessonsLearnedDashboard
            services.AddScoped<ILessonsLearnedDashboardRepository, LessonsLearnedDashboardRepository>();
            services.AddScoped<ILessonsLearnedDashboardService, LessonsLearnedDashboardService>();

            return services;
        }
    }
}
