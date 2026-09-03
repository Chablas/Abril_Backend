using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Services;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Repositories;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Services;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.AlmacenModule;

/// <summary>Módulo de Logística/Almacén — control de stock, ingresos/salidas por proyecto,
/// y carga de órdenes de compra/contratos. Independiente de Costos y de Arquitectura
/// Comercial: no comparte tablas ni referencias FK con esos módulos.</summary>
public static class AlmacenModule
{
    public static IServiceCollection AddAlmacenModule(this IServiceCollection services)
    {
        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<IMaterialService, MaterialService>();

        services.AddScoped<IOrdenCompraRepository, OrdenCompraRepository>();
        services.AddScoped<IOrdenCompraService, OrdenCompraService>();

        return services;
    }
}
