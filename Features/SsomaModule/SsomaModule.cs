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
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Services;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Interfaces;

using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AuditoriaAtsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AuditoriaAtsFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.AuditoriaAtsFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ProyectoHabilitadoFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Application.Services;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.HorasHombreFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.Ssoma
{
    public static class SsomaModule
    {
        public static IServiceCollection AddSsomaModule(this IServiceCollection services)
        {
            // Checklist SSOMA
            services.AddScoped<IChecklistRepository, ChecklistRepository>();
            services.AddScoped<IChecklistService, ChecklistService>();

            // Proyectos habilitados para SSOMA
            services.AddScoped<IProyectoHabilitadoRepository, ProyectoHabilitadoRepository>();
            services.AddScoped<IProyectoHabilitadoService, ProyectoHabilitadoService>();

            // Inhabilitaciones y Escuelitas
            services.AddScoped<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Services.SsomaInhabilitacionService>();
            services.AddScoped<Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Services.SsomaEscuelitaService>();

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

            // Alertas SSOMA (cron) — accidentes, descansos, reinducción, casos sociales
            services.AddScoped<ISsomaReminderService, SsomaReminderService>();

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

            // Seguimiento Médico de Accidentes (citas, equipos, alta)
            services.AddScoped<ICitaMedicaRepository, CitaMedicaRepository>();
            services.AddScoped<ICitaMedicaService, CitaMedicaService>();
            services.AddScoped<IEquipoPrestadoRepository, EquipoPrestadoRepository>();
            services.AddScoped<IEquipoPrestadoService, EquipoPrestadoService>();
            services.AddScoped<IAltaMedicaRepository, AltaMedicaRepository>();
            services.AddScoped<IAltaMedicaService, AltaMedicaService>();

            // Descansos Médicos
            services.AddScoped<IDescansoMedicoRepository, DescansoMedicoRepository>();
            services.AddScoped<IDescansoMedicoService, DescansoMedicoService>();

            // SCTR — Asistente Social
            services.AddScoped<ISctrGestionRepository, SctrGestionRepository>();
            services.AddScoped<ISctrGestionService, SctrGestionService>();

            // Mi Salud (self-service staff)
            services.AddScoped<IMiSaludRepository, MiSaludRepository>();
            services.AddScoped<IMiSaludService, MiSaludService>();

            // Asistente Social — Casos Sociales
            services.AddScoped<ICasoSocialRepository, CasoSocialRepository>();
            services.AddScoped<ISeguimientoRepository, SeguimientoRepository>();
            services.AddScoped<ICasoSocialService, CasoSocialService>();

            // Charlas y Capacitaciones
            services.AddScoped<ICharlaService, CharlaService>();

            // Accidentes e Incidentes
            services.AddScoped<IAccidenteIncidenteRepository, AccidenteIncidenteRepository>();
            services.AddScoped<IAccidenteIncidenteService, AccidenteIncidenteService>();

            // Auditoría ATS
            services.AddScoped<IAuditoriaAtsRepository, AuditoriaAtsRepository>();
            services.AddScoped<IAuditoriaAtsService, AuditoriaAtsService>();

            // Amonestaciones y Suspensiones
            services.AddScoped<IAmonestacionRepository, AmonestacionRepository>();
            services.AddScoped<IAmonestacionService, AmonestacionService>();
            services.AddScoped<AmonestacionNotificationService>();

            // Indicadores Proactivos
            services.AddScoped<IIndicadoresProactivosRepository, IndicadoresProactivosRepository>();
            services.AddScoped<IIndicadoresProactivosService, IndicadoresProactivosService>();

            // Desempeño Supervisor
            services.AddScoped<DesempenoSupervisorRepository>();

            // Presupuesto de Materiales SSOMA
            services.AddScoped<ICatalogoMaterialesRepository, CatalogoMaterialesRepository>();
            services.AddScoped<ICatalogoMaterialesService, CatalogoMaterialesService>();
            services.AddScoped<IConsumoRepository, ConsumoRepository>();
            services.AddScoped<IEstandarizacionRepository, EstandarizacionRepository>();
            services.AddScoped<IEstandarizacionService, EstandarizacionService>();
            services.AddScoped<IConsumoService, ConsumoService>();
            services.AddScoped<IRevisionMaterialesService, RevisionMaterialesService>();
            services.AddScoped<IRatioRepository, RatioRepository>();
            services.AddScoped<IRatioService, RatioService>();
            services.AddScoped<IDriversService, DriversService>();
            services.AddScoped<IPresupuestoRepository, PresupuestoRepository>();
            services.AddScoped<IPresupuestoService, PresupuestoService>();
            services.AddScoped<IControlConsumoRepository, ControlConsumoRepository>();
            services.AddScoped<IControlConsumoService, ControlConsumoService>();

            // Horas Hombre (a partir del Tareo de Control de Acceso)
            services.AddScoped<IHorasHombreRepository, HorasHombreRepository>();
            services.AddScoped<IHorasHombreService, HorasHombreService>();

            return services;
        }
    }
}
