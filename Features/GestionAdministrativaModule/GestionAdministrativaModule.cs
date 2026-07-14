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
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Repositories;
using Abril_Backend.Features.GestionAdministrativa.CarpetaAdjuntos.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.CarpetaAdjuntos.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.CarpetaAdjuntos.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.CarpetaAdjuntos.Infrastructure.Repositories;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Repositories;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadSalidas.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadSalidas.Infrastructure.Repositories;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Repositories;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Application.Services;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Infrastructure.Repositories;

namespace Abril_Backend.Features.GestionAdministrativa
{
    public static class GestionAdministrativaModule
    {
        public static IServiceCollection AddGestionAdministrativaModule(this IServiceCollection services)
        {
            // Solicitud Salidas
            services.AddScoped<ISolicitudSalidaRepository, SolicitudSalidaRepository>();
            // Revisor por tabla workers_revisores (prioridad) con fallback al área GTH.
            services.AddScoped<ISalidaRevisorResolver, SalidaRevisorResolver>();
            // JefeResolver (ApproverResolver): algoritmo de jerarquía SIN USO desde 2026-07-13,
            // reemplazado por SalidaRevisorResolver. Se conserva el código por si se retoma.
            // services.AddScoped<IApproverResolver, ApproverResolver>();
            services.AddScoped<ISolicitudSalidaTokenService, SolicitudSalidaTokenService>();
            services.AddScoped<ISolicitudSalidaService, SolicitudSalidaService>();

            // Gestión de Salidas
            services.AddScoped<IGestionSalidaRepository, GestionSalidaRepository>();
            services.AddScoped<IGestionSalidaService, GestionSalidaService>();
            services.AddScoped<ISalidaVisibilityResolver, SalidaVisibilityResolver>();

            // Lugares (configuración)
            services.AddScoped<IGaLugarRepository, GaLugarRepository>();
            services.AddScoped<IGaLugarService, GaLugarService>();

            // Motivos de salida (configuración)
            services.AddScoped<IGaMotivoSalidaRepository, GaMotivoSalidaRepository>();
            services.AddScoped<IGaMotivoSalidaService, GaMotivoSalidaService>();

            // Trayectos (configuración: par origen-destino con monto)
            services.AddScoped<IGaTrayectoRepository, GaTrayectoRepository>();
            services.AddScoped<IGaTrayectoService, GaTrayectoService>();

            // Revisor de salidas (configuración: override manual del aprobador por trabajador)
            services.AddScoped<IRevisorSalidaRepository, RevisorSalidaRepository>();
            services.AddScoped<IRevisorSalidaService, RevisorSalidaService>();

            // Carpeta de adjuntos (configuración: carpeta SharePoint/OneDrive detectada por link
            // donde se guardan los documentos adjuntos de las solicitudes de salida)
            services.AddScoped<ICarpetaAdjuntosRepository, CarpetaAdjuntosRepository>();
            services.AddScoped<ICarpetaAdjuntosService, CarpetaAdjuntosService>();

            // Revisores de áreas (configuración: n revisores por área estándar, 2do paso
            // al resolver el revisor de una salida, entre workers_revisores y el fallback GTH)
            services.AddScoped<IAreaRevisorRepository, AreaRevisorRepository>();
            services.AddScoped<IAreaRevisorService, AreaRevisorService>();

            // Visibilidad de salidas (configuración: override manual de áreas visibles por trabajador)
            services.AddScoped<IVisibilidadSalidaRepository, VisibilidadSalidaRepository>();
            services.AddScoped<IVisibilidadSalidaService, VisibilidadSalidaService>();

            return services;
        }
    }
}
