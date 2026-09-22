namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

public class ResiduoDeclaracionDetalleDto
{
    public int Id { get; set; }
    public int ResiduoTipoId { get; set; }
    public string? NombreResiduoTipo { get; set; }
    public decimal CantidadAcumuladaAnterior { get; set; }
    public decimal Ene { get; set; }
    public decimal Feb { get; set; }
    public decimal Mar { get; set; }
    public decimal Abr { get; set; }
    public decimal May { get; set; }
    public decimal Jun { get; set; }
    public decimal Jul { get; set; }
    public decimal Ago { get; set; }
    public decimal Sep { get; set; }
    public decimal Oct { get; set; }
    public decimal Nov { get; set; }
    public decimal Dic { get; set; }
    public decimal Almacenado { get; set; }
    public decimal Tratado { get; set; }
    public decimal Acondicionado { get; set; }
    public decimal Valorizado { get; set; }
    public decimal Comercializado { get; set; }
    public decimal DisposicionFinal { get; set; }
}

public class ResiduoDeclaracionDetalleUpsertDto
{
    public int ResiduoTipoId { get; set; }
    public decimal CantidadAcumuladaAnterior { get; set; }
    public decimal Ene { get; set; }
    public decimal Feb { get; set; }
    public decimal Mar { get; set; }
    public decimal Abr { get; set; }
    public decimal May { get; set; }
    public decimal Jun { get; set; }
    public decimal Jul { get; set; }
    public decimal Ago { get; set; }
    public decimal Sep { get; set; }
    public decimal Oct { get; set; }
    public decimal Nov { get; set; }
    public decimal Dic { get; set; }
    public decimal Almacenado { get; set; }
    public decimal Tratado { get; set; }
    public decimal Acondicionado { get; set; }
    public decimal Valorizado { get; set; }
    public decimal Comercializado { get; set; }
    public decimal DisposicionFinal { get; set; }
}

public class ResiduoDeclaracionEoRsDto
{
    public int Id { get; set; }
    public int EoRsId { get; set; }
    public string? NombreEoRs { get; set; }
    public string Etapa { get; set; } = "";
    public int NumeroServiciosAnio { get; set; }
    public decimal TotalResiduoTon { get; set; }
}

public class ResiduoDeclaracionEoRsUpsertDto
{
    public int EoRsId { get; set; }
    public string Etapa { get; set; } = "";
    public int NumeroServiciosAnio { get; set; }
    public decimal TotalResiduoTon { get; set; }
}

public class ResiduoDeclaracionDto
{
    public int Id { get; set; }
    public int ContributorId { get; set; }
    public string? NombreContributor { get; set; }
    public int PeriodoAnio { get; set; }
    public string Estado { get; set; } = "";
    public DateOnly? FechaPresentacion { get; set; }
    public string? ArchivoConstanciaUrl { get; set; }
    public List<ResiduoDeclaracionDetalleDto> Detalles { get; set; } = [];
    public List<ResiduoDeclaracionEoRsDto> EoRsIntervinientes { get; set; } = [];
}

public class ResiduoDeclaracionUpsertDto
{
    public int ContributorId { get; set; }
    public int PeriodoAnio { get; set; }
}

public class ResiduoDeclaracionMarcarPresentadaDto
{
    public DateOnly FechaPresentacion { get; set; }
    public string? ArchivoConstanciaUrl { get; set; }
}

/// <summary>Parámetros para generar/recalcular automáticamente el detalle de una declaración a
/// partir de ss_residuo_viaje.</summary>
public class ResiduoDeclaracionRecalcularDto
{
    public int ContributorId { get; set; }
    public int PeriodoAnio { get; set; }
    /// <summary>Si es true, recalcula aunque la declaración ya esté PRESENTADA.</summary>
    public bool Forzar { get; set; }
}
