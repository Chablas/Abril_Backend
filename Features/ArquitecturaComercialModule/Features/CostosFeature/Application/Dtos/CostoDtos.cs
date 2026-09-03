namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Dtos;

public class ProyectoCostoFiltroDTO
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class CostoFiltrosDTO
{
    public List<ProyectoCostoFiltroDTO> Proyectos { get; set; } = [];
    public List<string> Partidas { get; set; } = [];
}

public class UpsertCostoRegistroDTO
{
    public int ProyectoId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int Semana { get; set; }
    public string Partida { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

public class UpsertCostoProyeccionDTO
{
    public int ProyectoId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Partida { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

public class UpsertCostoMetaDTO
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public decimal Monto { get; set; }
}

public class CostoPartidaFilaDTO
{
    public string Partida { get; set; } = string.Empty;
    public Dictionary<int, decimal> MontosPorSemana { get; set; } = [];
    public decimal TotalMes { get; set; }
}

public class CostoPartidaProyeccionDTO
{
    public string Partida { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

/// <summary>Matriz completa de un proyecto para el mes en curso: costos por partida x semana
/// (con subtotal del mes) y la proyección al mes siguiente por partida.</summary>
public class CostoMatrizDTO
{
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int NumeroSemanas { get; set; }
    public List<CostoPartidaFilaDTO> Partidas { get; set; } = [];
    public decimal SubtotalMes { get; set; }

    public int AnioProyeccion { get; set; }
    public int MesProyeccion { get; set; }
    public List<CostoPartidaProyeccionDTO> Proyecciones { get; set; } = [];
    public decimal SubtotalProyeccion { get; set; }
}

public class CostoDashboardItemDTO
{
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public decimal TotalMes { get; set; }
}

public class CostoDashboardDTO
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public List<CostoDashboardItemDTO> Proyectos { get; set; } = [];
}

public class CostoEvolucionPuntoDTO
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public decimal GastoEjecutadoOProyectado { get; set; }
    public bool EsProyeccion { get; set; }
    public decimal? PresupuestoMeta { get; set; }
}

public class CostoEvolucionDTO
{
    public List<CostoEvolucionPuntoDTO> Puntos { get; set; } = [];
}
