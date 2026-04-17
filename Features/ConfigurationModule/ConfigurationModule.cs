using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Services;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.ConfigurationModule
{
    public static class ConfigurationModule
    {
        public static IServiceCollection AddConfigurationModule(this IServiceCollection services)
        {
            // Project
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IProjectService, ProjectService>();

            return services;
        }
    }
}
