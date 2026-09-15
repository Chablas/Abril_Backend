namespace Abril_Backend.Application.DTOs.ArquitecturaComercial;

public class TareasPorArquitectoDTO
{
    public int     UserId      { get; set; }
    public string  Nombre      { get; set; } = "";
    public int     Hitos       { get; set; }
    public int     Entregables { get; set; }
    public int     Consultas   { get; set; }
    public int     Total       { get; set; }
    /// <summary>Carga ponderada por tipo de partida (Hito/Entregable pesan más que una Consulta) —
    /// esto, y no <see cref="Total"/>, es lo que clasifica Sobrecargado/Normal/Disponible.</summary>
    public decimal TotalPonderado { get; set; }
    public decimal AvancePct   { get; set; }
}

/// <summary>Entregables e Hitos con vencimiento en los próximos 14 días, agrupados por proyecto —
/// reemplaza a la antigua "Curva de Avance": un gerente no planifica sobre un % acumulado
/// abstracto, planifica sobre "qué vence, en qué proyecto, en las próximas 1-2 semanas".</summary>
public class ProximoPorProyectoDTO
{
    public int    ProyectoId     { get; set; }
    public string ProyectoNombre { get; set; } = "";
    public int    Entregables    { get; set; }
    public int    Hitos          { get; set; }
}

/// <summary>Tasa de cierre semanal (últimas 8 semanas) SOLO de Consultas — una Consulta no es
/// lo mismo que un Hito/Entregable, así que se mide aparte en vez de mezclarse en un SPI
/// general. Null en una semana = no había consultas venciendo esa semana (no es 0%).</summary>
public class EficienciaConsultaSemanalDTO
{
    public string  Semana      { get; set; } = "";
    public double? TasaCierre  { get; set; }
    public double? SpiPromedio { get; set; }
}

public class CategoriaItemDTO
{
    public int    Id     { get; set; }
    public string Nombre { get; set; } = "";
}

/// <summary>Fila de la mini-línea de tiempo de Hitos/Entregables del dashboard (todos los
/// proyectos, próximos 3 meses) — reemplaza a la tarjeta plana de "Hitos Críticos" por una
/// vista tipo Gantt que sí muestra CUÁNDO cae cada uno, no solo cuántos días faltan.</summary>
public class GanttMiniItemDTO
{
    public int     Id               { get; set; }
    public string  Nombre           { get; set; } = "";
    public string  Proyecto         { get; set; } = "";
    public string? InicioProgramado { get; set; }
    public string? FinProgramado    { get; set; }
    public string? InicioEfectivo   { get; set; }
    public string? FinEfectivo      { get; set; }
    public string  Estado           { get; set; } = "";
}
