namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;

// ── Catálogo ─────────────────────────────────────────────────────────────────

public class PetarItemDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
}

public class PetarTipoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public List<PetarItemDto> Items { get; set; } = [];
}

public class PetarInitDto
{
    public List<PetarTipoDto> Tipos { get; set; } = [];
    /// <summary>Datos del ATS de origen, para no tener que volver a pedirlos (proyecto, lugar sugerido, etc.).</summary>
    public int AtsId { get; set; }
    public string? AtsLugar { get; set; }
    public string? AtsActividad { get; set; }
}

// ── Guardar borrador ─────────────────────────────────────────────────────────

public class PetarItemRespuestaRequestDto
{
    public int ItemId { get; set; }
    /// <summary>"SI" | "NO" | "NA".</summary>
    public string Respuesta { get; set; } = string.Empty;
}

public class PetarGuardarRequestDto
{
    public int AtsId { get; set; }
    public int TipoId { get; set; }
    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public string? HoraInicio { get; set; } // "HH:mm"
    public string? HoraFin { get; set; }
    public List<PetarItemRespuestaRequestDto> Respuestas { get; set; } = [];
    /// <summary>Solo cuando el tipo elegido es "Izaje de cargas" (SSO-FO-043) — campos técnicos de
    /// la grúa que el formato físico exige y el checklist SI/NO no cubre.</summary>
    public PetarIzajeGruaDto? IzajeGrua { get; set; }
}

/// <summary>Campos técnicos propios del izaje con grúa (SSO-FO-043): tipo/modelo/capacidad de la
/// grúa, ángulo de pluma, peso de la carga, etc. Todos opcionales — el checklist SI/NO ya es
/// obligatorio por sí solo y no depende de que se llenen estos datos.</summary>
public class PetarIzajeGruaDto
{
    public string? TipoGrua { get; set; }
    public string? FabricanteOMarca { get; set; }
    public string? ModeloOPlaca { get; set; }
    public string? SerieOTarjetaCirculacion { get; set; }
    public decimal? LongitudPlumaBrazoM { get; set; }
    public decimal? RadioMaximoGiroM { get; set; }
    public string? DireccionGradoGiro { get; set; }
    public decimal? ElevacionM { get; set; }
    public decimal? AnguloPluma { get; set; }
    public decimal? CapacidadCertificadaTon { get; set; }
    public decimal? PesoCargaTotalTon { get; set; }
    public decimal? PorcentajeCapacidad { get; set; }
    public string? TamanoEstrobo { get; set; }
    public string? Observaciones { get; set; }
}

// ── Firmas ───────────────────────────────────────────────────────────────────

public class PetarFirmarRequestDto
{
    public string SelfieBase64 { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
}

public class PetarFirmarVistoRequestDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
}

public class PetarCerrarRequestDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
}

// ── Respuesta ────────────────────────────────────────────────────────────────

public class PetarItemRespuestaResponseDto
{
    public int ItemId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string Respuesta { get; set; } = string.Empty;
}

public class PetarResponseDto
{
    public int Id { get; set; }
    public int AtsId { get; set; }
    public int TipoId { get; set; }
    public string? TipoNombre { get; set; }
    public string? TipoCodigo { get; set; }
    public int WorkerId { get; set; }
    public string? WorkerNombre { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }

    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public DateOnly Fecha { get; set; }
    public string? HoraInicio { get; set; }
    public string? HoraFin { get; set; }

    public DateTime? HoraServidorFirma { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public string? SelfieUrl { get; set; }
    public string? FirmaUrl { get; set; }

    public string? SupervisorNombre { get; set; }
    public string? SupervisorCargo { get; set; }
    public string? SupervisorFirmaUrl { get; set; }
    public DateTime? SupervisorHoraServidor { get; set; }

    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }

    public string Estado { get; set; } = string.Empty;
    public DateTime? CierreHoraServidor { get; set; }
    public string? CierreObservaciones { get; set; }
    public string? CierreFirmaUrl { get; set; }
    public string? PdfHash { get; set; }

    public bool PuedeFirmarSupervisor { get; set; }
    public bool PuedeFirmarSsoma { get; set; }
    public bool PuedeCerrar { get; set; }

    public List<PetarItemRespuestaResponseDto> Respuestas { get; set; } = [];
    public PetarIzajeGruaDto? IzajeGrua { get; set; }
}

public class PetarVerificacionPublicaDto
{
    public bool Encontrado { get; set; }
    public bool Valido { get; set; }
    public string? WorkerNombre { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? TipoNombre { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? Estado { get; set; }
}

public class PetarFiltroDto
{
    public int? ProyectoId { get; set; }
    public int? WorkerId { get; set; }
    public int? AtsId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public int Page { get; set; } = 1;
}

public class PetarListResponseDto
{
    public List<PetarResponseDto> Data { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}
