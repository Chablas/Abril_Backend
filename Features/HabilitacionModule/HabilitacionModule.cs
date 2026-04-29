using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Application.Services;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Repositories;

namespace Abril_Backend.Features.Habilitacion
{
    public static class HabilitacionModule
    {
        public static IServiceCollection AddHabilitacionModule(this IServiceCollection services)
        {
            services.AddHttpClient();

            services.AddScoped<IContratistaAuthService, ContratistaAuthService>();
            services.AddScoped<ICatalogosHabilitacionRepository, CatalogosHabilitacionRepository>();
            services.AddScoped<IEmpresaContratistaRepository, EmpresaContratistaRepository>();
            services.AddScoped<IHabTrabajadorRepository, HabTrabajadorRepository>();
            services.AddScoped<IBandejaRepository, BandejaRepository>();
            services.AddScoped<IReglasTrabajadorRepository, ReglasTrabajadorRepository>();
            services.AddScoped<IHabEmpresaRepository, HabEmpresaRepository>();
            services.AddScoped<ISctrVidaLeyRepository, SctrVidaLeyRepository>();
            services.AddScoped<IEquipoRepository, EquipoRepository>();
            services.AddScoped<ISharePointHabService, SharePointHabService>();
            return services;
        }
    }
}
