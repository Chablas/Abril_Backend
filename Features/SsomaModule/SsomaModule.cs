using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories;
using Abril_Backend.Features.Ssoma.Paso.Services;
using Abril_Backend.Features.Ssoma.Rac.Services;

namespace Abril_Backend.Features.Ssoma
{
    public static class SsomaModule
    {
        public static IServiceCollection AddSsomaModule(this IServiceCollection services)
        {
            // Catalogos
            services.AddScoped<ICatalogosRepository, CatalogosRepository>();
            services.AddScoped<ICatalogosService, CatalogosService>();

            // Clinica usuarios
            services.AddScoped<IClinicaUsuarioService, ClinicaUsuarioService>();

            // EMO
            services.AddScoped<IEmoRepository, EmoRepository>();
            services.AddScoped<IEmoService, EmoService>();

            // Convalidacion
            services.AddScoped<IConvalidacionRepository, ConvalidacionRepository>();
            services.AddScoped<IConvalidacionService, ConvalidacionService>();

            // Programacion EMO
            services.AddScoped<IProgramacionEmoRepository, ProgramacionEmoRepository>();
            services.AddScoped<IProgramacionEmoService, ProgramacionEmoService>();

            // Interconsulta
            services.AddScoped<IInterconsultaRepository, InterconsultaRepository>();
            services.AddScoped<IInterconsultaService, InterconsultaService>();

            // Dashboard
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<IDashboardService, DashboardService>();

            // Workers search
            services.AddScoped<IWorkerSearchRepository, WorkerSearchRepository>();
            services.AddScoped<IWorkerSearchService, WorkerSearchService>();

            // Alertas EMO (cron)
            services.AddScoped<IEmoAlertaService, EmoAlertaService>();

            // Auto-programación EMO (cron)
            services.AddScoped<IEmoAutoProgramacionService, EmoAutoProgramacionService>();

            // Resumen diario EMO (cron 4:30pm)
            services.AddScoped<IEmoResumenDiarioService, EmoResumenDiarioService>();

            // PASO — Programa Anual de Seguridad
            services.AddScoped<IPasoService, PasoService>();

            // RAC — Reporte de Actos y Condiciones Subestándar
            services.AddScoped<IRacService, RacService>();
            services.AddScoped<IPenalidadService, PenalidadService>();

            return services;
        }
    }
}
