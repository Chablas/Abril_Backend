using Abril_Backend.Features.LearningModule.Application.Interfaces;
using Abril_Backend.Features.LearningModule.Application.Services;
using Abril_Backend.Features.LearningModule.Infrastructure.Interfaces;
using Abril_Backend.Features.LearningModule.Infrastructure.Repositories;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Services;

namespace Abril_Backend.Features.LearningModule
{
    /// <summary>
    /// Centro de aprendizaje y guías (videos-guía por área/módulo). Registra el servicio y
    /// el repositorio de la feature.
    /// </summary>
    public static class LearningModule
    {
        public static IServiceCollection AddLearningModule(this IServiceCollection services)
        {
            services.AddScoped<ILearningRepository, LearningRepository>();
            services.AddScoped<ILearningService, LearningService>();

            // Videos subidos como archivo a SharePoint (el registro de IGraphSharePointService es
            // idempotente: también lo hacen otros módulos).
            services.AddScoped<IGraphSharePointService, GraphSharePointService>();
            services.AddScoped<ILearningVideoStorage, LearningVideoStorage>();
            return services;
        }
    }
}
