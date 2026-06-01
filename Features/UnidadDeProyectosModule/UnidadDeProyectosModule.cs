using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Services;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Repositories;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Services;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Repositories;

namespace Abril_Backend.Features.UnidadDeProyectosModule
{
    public static class UnidadDeProyectosModule
    {
        public static IServiceCollection AddUnidadDeProyectosModule(this IServiceCollection services)
        {
            // ProjectsDashboard
            services.AddScoped<IProjectsDashboardRepository, ProjectsDashboardRepository>();
            services.AddScoped<IProjectsDashboardService, ProjectsDashboardService>();

            // CronogramaActividades
            services.AddScoped<ICronogramaActividadesRepository, CronogramaActividadesRepository>();
            services.AddScoped<ICronogramaActividadesService, CronogramaActividadesService>();

            return services;
        }
    }
}
