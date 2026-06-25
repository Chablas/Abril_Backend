using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Application.Interfaces;

namespace Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure;

/// <summary>
/// Pre-calienta el caché de indicadores SSOMA al arrancar el backend.
/// Así el primer usuario recibe respuesta instantánea.
/// </summary>
public class SsomaIndicadoresCacheWarmup : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SsomaIndicadoresCacheWarmup> _log;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public SsomaIndicadoresCacheWarmup(
        IServiceProvider services,
        IMemoryCache cache,
        ILogger<SsomaIndicadoresCacheWarmup> log)
    {
        _services = services;
        _cache = cache;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Espera 5 seg para que el backend termine de arrancar
        await Task.Delay(TimeSpan.FromSeconds(5), ct);

        var now = DateTime.UtcNow;
        int mes = now.Month, anio = now.Year;

        _log.LogInformation("[SSOMA Cache] Pre-calentando indicadores {Mes}/{Anio}...", mes, anio);

        try
        {
            using var scope = _services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IIndicadoresProactivosService>();

            // Calcula en paralelo seguimiento + puntaje todos
            var t1 = WarmAsync($"ind_seguimiento_{mes}_{anio}",
                () => svc.GetSeguimientoTodosProyectosAsync(mes, anio));
            var t2 = WarmAsync($"ind_puntaje_todos_{mes}_{anio}",
                () => svc.GetPuntajeTodosProyectosAsync(mes, anio));

            await Task.WhenAll(t1, t2);
            _log.LogInformation("[SSOMA Cache] Pre-calentamiento completo.");
        }
        catch (Exception ex)
        {
            _log.LogWarning("[SSOMA Cache] Error en pre-calentamiento: {Msg}", ex.Message);
        }
    }

    private async Task WarmAsync<T>(string key, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out _)) return;
        var result = await factory();
        _cache.Set(key, result, CacheTtl);
    }
}
