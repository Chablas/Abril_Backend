using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Repositories;
using Abril_Backend.Features.GestionAdministrativa.Lugares.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Lugares.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.Lugares.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Lugares.Infrastructure.Repositories;
using Abril_Backend.Features.GestionAdministrativa.MotivosSalida.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.MotivosSalida.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.MotivosSalida.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.MotivosSalida.Infrastructure.Repositories;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Repositories;

namespace Abril_Backend.Features.GestionAdministrativa
{
    public static class GestionAdministrativaModule
    {
        public static IServiceCollection AddGestionAdministrativaModule(this IServiceCollection services)
        {
            // Solicitud Salidas
            services.AddScoped<ISolicitudSalidaRepository, SolicitudSalidaRepository>();
            services.AddScoped<IApproverResolver, ApproverResolver>();
            services.AddScoped<ISolicitudSalidaTokenService, SolicitudSalidaTokenService>();
            services.AddScoped<ISolicitudSalidaService, SolicitudSalidaService>();

            // Gestión de Salidas
            services.AddScoped<IGestionSalidaRepository, GestionSalidaRepository>();
            services.AddScoped<IGestionSalidaService, GestionSalidaService>();

            // Lugares (configuración)
            services.AddScoped<IGaLugarRepository, GaLugarRepository>();
            services.AddScoped<IGaLugarService, GaLugarService>();

            // Motivos de salida (configuración)
            services.AddScoped<IGaMotivoSalidaRepository, GaMotivoSalidaRepository>();
            services.AddScoped<IGaMotivoSalidaService, GaMotivoSalidaService>();

            return services;
        }
    }
}
