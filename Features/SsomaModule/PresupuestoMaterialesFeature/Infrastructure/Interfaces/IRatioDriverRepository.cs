using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

/// <summary>Proyectos con área techada cargada — base sobre la que se arman los dos drivers
/// reales (HH y Trabajadores), que ahora vienen de fuentes distintas e independientes.</summary>
public class ProyectoAreaRow
{
    public int ProjectId { get; set; }
    public decimal AreaTechada { get; set; }
    /// <summary>Project.Activo: Finalizado | Activo | Inactivo — si el proyecto sigue Activo,
    /// el HH acumulado a la fecha es parcial (todavía no es el total real de la obra).</summary>
    public string CicloVida { get; set; } = "";
    /// <summary>Valor final tipeado a mano en Datos Base (Project.HhTotalCasa) — gana sobre el
    /// calculado desde Tareo/planilla cuando existe.</summary>
    public decimal? HhTotalCasa { get; set; }
    /// <summary>Project.CantTrabajadoresCasa, texto libre — se parsea al calcular.</summary>
    public string? CantTrabajadoresCasa { get; set; }
}

/// <summary>HH real de un proyecto, agregado desde el Tareo de Control de Acceso (SsTareo +
/// detalle casa/contratista) — no desde Project.HhTotalCasa, que es un campo tipeado a mano.</summary>
public class ProyectoHhRealRow
{
    public int ProjectId { get; set; }
    public decimal HhTotal { get; set; }
    public int DiasRegistrados { get; set; }
}

/// <summary>N Trabajadores real de un proyecto = cantidad de trabajadores DISTINTOS que alguna
/// vez tuvieron una vinculación a ese proyecto (worker_vinculaciones), no un promedio diario —
/// asi lo pidio el responsable: "los totales que alguna vez han pisado la obra".</summary>
public class ProyectoTrabajadoresRealRow
{
    public int ProjectId { get; set; }
    public int TotalTrabajadoresDistintos { get; set; }
}

public class RatioDriverUpsertItem
{
    public string TipoDriver { get; set; } = null!;
    public int ProjectId { get; set; }
    public decimal AreaTechada { get; set; }
    /// <summary>Valor "oficial": el manual si existe, si no el calculado. Es el que entra a
    /// la mediana/ratio.</summary>
    public decimal Cantidad { get; set; }
    public decimal Ratio { get; set; }
    /// <summary>Crudo calculado desde Tareo/planilla (HH) o worker_vinculaciones
    /// (TRABAJADORES) — se guarda siempre, aunque haya manual, para poder comparar.</summary>
    public decimal CantidadCalculado { get; set; }
    /// <summary>Valor final tipeado a mano en Datos Base — null si el proyecto todavía no lo
    /// tiene cargado.</summary>
    public decimal? CantidadManual { get; set; }
    public int DiasRegistrados { get; set; }
    /// <summary>Valor de incluido_manual SOLO para cuando el par (tipo,proyecto) se inserta por
    /// primera vez — true únicamente si el proyecto ya está Finalizado. Si la fila ya existía,
    /// el UPDATE no toca esta columna: la decisión manual previa del responsable se respeta
    /// siempre, incluso si el proyecto pasa de Activo a Finalizado más adelante.</summary>
    public bool IncluidoManualDefault { get; set; }
}

public class RatioDriverOutlierRow
{
    public int Id { get; set; }
    public string TipoDriver { get; set; } = null!;
    public decimal Ratio { get; set; }
}

public class RatioDriverOutlierUpdate
{
    public int Id { get; set; }
    public bool EsOutlier { get; set; }
}

public interface IRatioDriverRepository
{
    Task<List<ProyectoAreaRow>> ObtenerProyectosConAreaAsync();
    Task<List<ProyectoHhRealRow>> ObtenerHhRealPorProyectoAsync(List<int> projectIds);
    Task<List<ProyectoTrabajadoresRealRow>> ObtenerTrabajadoresRealPorProyectoAsync(List<int> projectIds);
    Task UpsertRatiosBulkAsync(List<RatioDriverUpsertItem> items);
    Task<List<RatioDriverOutlierRow>> ObtenerTodosParaOutlierAsync();
    Task ActualizarOutliersBulkAsync(List<RatioDriverOutlierUpdate> updates);
    Task<List<RatioDriverProyectoDto>> ObtenerPorTipoAsync(string tipoDriver);
    Task ActualizarIncluidoManualAsync(string tipoDriver, int projectId, bool incluir);
}
