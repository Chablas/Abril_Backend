using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Services;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Repositories;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Services;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Repositories;
using Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Application.Services;
using Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.HolidayFeature.Infrastructure.Repositories;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Services;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Repositories;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Services;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Infrastructure.Repositories;

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
            services.AddScoped<IAreaScopeRepository, AreaScopeRepository>();
            services.AddScoped<IAreaScopeService, AreaScopeService>();

            // Holiday (Feriados y días no laborables)
            services.AddScoped<IHolidayRepository, HolidayRepository>();
            services.AddScoped<IHolidayService, HolidayService>();

            // Bancos: el catálogo del que sale el banco de cada razón social del grupo, que es lo
            // que el formulario de bienvenida le muestra al nuevo colaborador.
            services.AddScoped<IBancoRepository, BancoRepository>();
            services.AddScoped<IBancoService, BancoService>();

            // Razones sociales: el alta (con consulta a SUNAT) y la edición desde Configuración.
            // El catálogo de empresas de SSOMA lee la misma tabla, pero desde su propio controller.
            services.AddScoped<IRazonSocialRepository, RazonSocialRepository>();
            services.AddScoped<IRazonSocialService, RazonSocialService>();

            return services;
        }
    }
}
