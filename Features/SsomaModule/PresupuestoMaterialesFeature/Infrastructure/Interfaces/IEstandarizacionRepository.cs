using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

public class MatchResult
{
    public int ItemId { get; set; }
    public string NombreItem { get; set; } = null!;
    public int FamiliaId { get; set; }
    public string NombreFamilia { get; set; } = null!;
    public bool PerteneceSsoma { get; set; }
    public decimal Score { get; set; }
    public string Metodo { get; set; } = null!;
    /// <summary>Del alias matcheado (si aplica). Default 1 cuando el match no viene de un alias específico.</summary>
    public decimal FactorConversion { get; set; } = 1;
}

public interface IEstandarizacionRepository
{
    /// <summary>Busca en ss_material_alias por texto_crudo_norm exacto — O(1) con índice único.</summary>
    Task<MatchResult?> BuscarPorAliasExactoAsync(string textoCrudoNorm);
    /// <summary>Busca en ss_material_item por nombre_normalizado exacto.</summary>
    Task<MatchResult?> BuscarPorNombreExactoAsync(string nombreNorm);
    /// <summary>Búsqueda trigram en ss_material_item.nombre_normalizado vía pg_trgm.</summary>
    Task<List<MatchResult>> BuscarPorTrigramAsync(string textoCrudoNorm, decimal umbralMinimo, int topN = 5);
    Task CrearAliasAsync(string textoCrudo, string textoCrudoNorm, int itemId, string origen, decimal confianza);
}
