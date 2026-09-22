using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

public interface IResiduoViajeRepository
{
    Task<ResiduoViajePagedDto> ListarAsync(ResiduoViajeListFiltroDto filtro);
    Task<SsResiduoViaje?> ObtenerAsync(long id);
    Task<long> CrearAsync(SsResiduoViaje entidad);
    Task<bool> ActualizarAsync(SsResiduoViaje entidad);
    Task<bool> DesactivarAsync(long id);
    Task<bool> SetArchivoAsync(long id, string archivoUrl);

    /// <summary>Suma mensual (ene-dic) y por tipo de manejo, agrupada por tipo de residuo, para los
    /// viajes de proyectos cuyo contributor_id coincide y cuya fecha cae en el año indicado.</summary>
    Task<List<ResiduoViajeAgregadoMensualDto>> ObtenerAgregadoMensualAsync(int contributorId, int anio);
}

/// <summary>Fila agregada usada por el recálculo de declaración: un tipo de residuo, sus 12 montos
/// mensuales y su desglose por tipo de manejo, ya sumados desde ss_residuo_viaje.</summary>
public class ResiduoViajeAgregadoMensualDto
{
    public int ResiduoTipoId { get; set; }
    public decimal[] Meses { get; set; } = new decimal[12];
    public decimal Almacenado { get; set; }
    public decimal Tratado { get; set; }
    public decimal Acondicionado { get; set; }
    public decimal Valorizado { get; set; }
    public decimal Comercializado { get; set; }
    public decimal DisposicionFinal { get; set; }
}
