namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

// ─── Ratios historicos de drivers de proyecto (HH y N Trabajadores) ───────────
// Analogo al motor de ratios de materiales, pero sobre los drivers del proyecto
// (Project.HhTotalCasa / CantTrabajadoresCasa) en vez de consumos de material.

public class RatioDriverProyectoDto
{
    public int ProjectId { get; set; }
    public string ProjectDescription { get; set; } = null!;
    /// <summary>Finalizado | Activo | Inactivo — si sigue Activo, el HH/dotación es parcial
    /// (Tareo acumulado a la fecha, no el total final de la obra).</summary>
    public string CicloVida { get; set; } = null!;
    public int DiasRegistrados { get; set; }
    public decimal AreaTechada { get; set; }
    /// <summary>Valor "oficial" (manual si existe, si no el calculado) — el que entra a la
    /// mediana. Mantenido por compatibilidad; para distinguir las fuentes usar
    /// CantidadCalculado / CantidadManual.</summary>
    public decimal Cantidad { get; set; }
    public decimal Ratio { get; set; }
    /// <summary>Acumulado real calculado desde Tareo/planilla (HH) o worker_vinculaciones
    /// (TRABAJADORES) — "en vivo", puede estar incompleto si el proyecto sigue activo o si la
    /// fuente no está bien cargada para ese proyecto (ver caso SAUCO).</summary>
    public decimal CantidadCalculado { get; set; }
    /// <summary>Valor final tipeado a mano en Datos Base (Project.HhTotalCasa /
    /// CantTrabajadoresCasa) — null si el proyecto todavía no lo tiene cargado.</summary>
    public decimal? CantidadManual { get; set; }
    /// <summary>Solo informativo para HH: HH_REAL | HH_PROYECTADO | HH_CALCULADO_MEDIANA — de
    /// dónde salió el valor manual, si lo hay.</summary>
    public string? HhFuente { get; set; }
    public bool EsOutlier { get; set; }
    /// <summary>Unica autoridad real sobre si este proyecto entra al calculo del ratio
    /// recomendado — el responsable decide por criterio propio, no se filtra
    /// automaticamente por CicloVida ni por dias registrados.</summary>
    public bool IncluidoManual { get; set; } = true;
}

public class RatioDriverComparacionDto
{
    public string TipoDriver { get; set; } = null!;
    public List<RatioDriverProyectoDto> Proyectos { get; set; } = [];
    public decimal MedianaRatio { get; set; }
    public decimal PromedioRatio { get; set; }
    public decimal MinRatio { get; set; }
    public decimal MaxRatio { get; set; }
}

public class CalcularRatiosDriversResultDto
{
    public int RatiosCalculados { get; set; }
    public int ProyectosSinArea { get; set; }
    public int ProyectosSinTareo { get; set; }
}

public class ActualizarIncluidoManualDriverDto
{
    public bool Incluir { get; set; }
}

public class RatioDriverRecomendadoDto
{
    public string TipoDriver { get; set; } = null!;
    public decimal RatioRecomendado { get; set; }
    public int NProyectos { get; set; }
}

public class RatiosDriversRecomendadosDto
{
    public RatioDriverRecomendadoDto? Hh { get; set; }
    public RatioDriverRecomendadoDto? Trabajadores { get; set; }
}
