using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Services;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Repositories;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Services;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.ConfigurationModule
{
    public static class ConfigurationModule
    {
        public static IServiceCollection AddConfigurationModule(this IServiceCollection services)
        {
            // Project
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IProjectService, ProjectService>();

            // Area
            services.AddScoped<IAreaTypeRepository, AreaTypeRepository>();
            services.AddScoped<IAreaTypeService, AreaTypeService>();
            services.AddScoped<IAreaItemRepository, AreaItemRepository>();
            services.AddScoped<IAreaItemService, AreaItemService>();

            return services;
        }
    }
}
