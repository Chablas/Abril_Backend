namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

/// <summary>Fila de dotación de personal SSOMA (Prevencionista/Monitor/Vigía) asignada a un hito
/// crítico real del cronograma del proyecto.</summary>
public class PersonalHitoDto
{
    public int Id { get; set; }
    public int HitoId { get; set; }
    public string HitoDescripcion { get; set; } = "";
    public DateOnly? HitoFecha { get; set; }
    public bool EsHitoCritico { get; set; }
    /// <summary>Hito/etapa en la que este rol se retira — null significa "Semanas" manual (comportamiento
    /// anterior). Con hito de salida, Semanas se recalcula server-side a partir de las fechas reales.</summary>
    public int? HitoSalidaId { get; set; }
    public string? HitoSalidaDescripcion { get; set; }
    public DateOnly? HitoSalidaFecha { get; set; }
    public string Rol { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal Semanas { get; set; }
    public decimal CostoMensual { get; set; }
    public decimal Total { get; set; }
}

/// <summary>Hito crítico disponible para asignarle personal (aunque todavía no tenga fila cargada).</summary>
public class HitoCriticoDisponibleDto
{
    public int HitoId { get; set; }
    public string HitoDescripcion { get; set; } = "";
    public DateOnly? HitoFecha { get; set; }
}

public class PersonalHitoItemInputDto
{
    public int HitoId { get; set; }
    /// <summary>Etapa de salida elegida (opcional) — si viene, el backend recalcula Semanas a partir
    /// de las fechas reales del cronograma y descarta el valor de Semanas enviado.</summary>
    public int? HitoSalidaId { get; set; }
    public string Rol { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal Semanas { get; set; }
    public decimal CostoMensual { get; set; }
}

public class PersonalHitoGuardarDto
{
    public List<PersonalHitoItemInputDto> Items { get; set; } = [];
}

/// <summary>Tarifa mensual sugerida por categoría (Oficial/Peón), estimada desde lo cargado en
/// otros proyectos recientemente — un punto de partida editable, no un valor fijo.</summary>
public class PersonalTarifasSugeridasDto
{
    public decimal Oficial { get; set; }
    public decimal Peon { get; set; }
}
