using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories;
using Abril_Backend.Features.Ssoma.Paso.Services;
using Abril_Backend.Features.Ssoma.Rac.Services;
using Abril_Backend.Features.SsomaModule.OptFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.OptFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.OptFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Infrastructure.Repositories;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Repositories;

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
            services.AddScoped<IRacSharePointService, RacSharePointService>();
            services.AddScoped<IRacNotificationService, RacNotificationService>();

            // OPT — Observación Planeada de Tarea
            services.AddScoped<IOptRepository, OptRepository>();
            services.AddScoped<IOptSharePointService, OptSharePointService>();
            services.AddScoped<IOptService, OptService>();

            // Inspecciones
            services.AddScoped<IInspeccionSharePointService, InspeccionSharePointService>();
            services.AddScoped<IInspeccionRepository, InspeccionRepository>();
            services.AddScoped<IInspeccionService, InspeccionService>();
            services.AddScoped<InspeccionPdfService>();

            // Tópico Médico
            services.AddScoped<ITopicoRepository, TopicoRepository>();
            services.AddScoped<ITopicoService, TopicoService>();

            // Accidentes de Trabajo
            services.AddScoped<IAccidenteTrabajoRepository, AccidenteTrabajoRepository>();
            services.AddScoped<IAccidenteTrabajoService, AccidenteTrabajoService>();

            // Descansos Médicos
            services.AddScoped<IDescansoMedicoRepository, DescansoMedicoRepository>();
            services.AddScoped<IDescansoMedicoService, DescansoMedicoService>();

            // Mi Salud (self-service staff)
            services.AddScoped<IMiSaludRepository, MiSaludRepository>();
            services.AddScoped<IMiSaludService, MiSaludService>();

            // Asistente Social — Casos Sociales
            services.AddScoped<ICasoSocialRepository, CasoSocialRepository>();
            services.AddScoped<ISeguimientoRepository, SeguimientoRepository>();
            services.AddScoped<ICasoSocialService, CasoSocialService>();

            return services;
        }
    }
}
