namespace Abril_Backend.Features.Ssoma.Penalidad.Dtos;

public class PenalidadListQuery
{
    public int? ProyectoId { get; set; }
    public int? EmpresaId { get; set; }
    public string? Estado { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Solo lo setea el controller: empresa del contratista logueado.</summary>
    public int? EmpresaIdContratista { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}

public class PenalidadEstadoHistorialDto
{
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = "";
    public DateTime CambioDateTime { get; set; }
    public string? CambioUsuarioNombre { get; set; }
}

public class PenalidadListItemDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string OrigenTipo { get; set; } = "";
    public int? OrigenId { get; set; }
    public string? OrigenCodigo { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? EmpresaNombre { get; set; }
    public string? InfraccionNombre { get; set; }
    public string Severidad { get; set; } = "";
    public decimal MontoCalculado { get; set; }
    public decimal? MontoFinal { get; set; }
    public string Estado { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? PlazoDescargoVenceEn { get; set; }
    public DateTime? ResueltaEn { get; set; }
    public string? ResolucionTipo { get; set; }
}

public class PenalidadDetalleDto : PenalidadListItemDto
{
    public int EmpresaId { get; set; }
    public int ProyectoId { get; set; }
    public int InfraccionId { get; set; }
    public string? DescripcionOcurrido { get; set; }
    public decimal UitReferencia { get; set; }
    public string? MotivoAjusteMonto { get; set; }

    public string? MotivoRechazoResidente { get; set; }
    public string? MotivoRechazoGerencia { get; set; }

    public string? DescargoTexto { get; set; }
    public string? DocumentoUrl { get; set; }
    public DateTime? DescargoFecha { get; set; }
    public bool DescargoPorIncomparecencia { get; set; }

    /// <summary>
    /// Argumento de SSOMA sobre el descargo — solo se llena en la respuesta cuando la
    /// penalidad ya llegó a Aplicada/Anulada (antes de eso, el backend lo omite: no se hace
    /// visible al contratista hasta que Gerencia decide).
    /// </summary>
    public string? ArgumentoSsoma { get; set; }
    public string? RecomendacionSsoma { get; set; }

    public string? ResolucionTexto { get; set; }
    public string? MotivoObjecionGerencia { get; set; }
    public string? PdfNotificacionUrl { get; set; }
    public string? PdfResolucionUrl { get; set; }

    public bool ApelacionUsada { get; set; }
    public DateTime? PlazoApelacionVenceEn { get; set; }
    public string? ApelacionTexto { get; set; }
    public string? ApelacionDocumentoUrl { get; set; }
    public DateTime? ApelacionFecha { get; set; }

    public List<PenalidadEstadoHistorialDto> Historial { get; set; } = new();
}

/// <summary>
/// RAC o Amonestación aún sin penalidad vinculada — candidato para el selector "Origen" del
/// formulario de Nueva Penalidad, en vez de que el usuario teclee el id a mano.
/// </summary>
public class OrigenCandidatoDto
{
    /// <summary>RAC | AMONESTACION</summary>
    public string OrigenTipo { get; set; } = "";
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int EmpresaId { get; set; }
    public int ProyectoId { get; set; }
    public string? EmpresaNombre { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? Severidad { get; set; }
    public int? InfraccionSugeridaId { get; set; }
    public DateTime Fecha { get; set; }
}

public class PenalidadRegistrarRequest
{
    /// <summary>RAC | AMONESTACION | DIRECTO</summary>
    public string OrigenTipo { get; set; } = "DIRECTO";
    public int? OrigenId { get; set; }

    public int EmpresaId { get; set; }
    public int ProyectoId { get; set; }
    public int InfraccionId { get; set; }
    public string Severidad { get; set; } = "";
    public string? DescripcionOcurrido { get; set; }
}

public class PenalidadRechazarRequest
{
    public string Motivo { get; set; } = "";
}

public class PenalidadDescargaRequest
{
    public string DescargoTexto { get; set; } = "";
    public string DocumentoUrl { get; set; } = "";
}

public class PenalidadEvaluarDescargoRequest
{
    /// <summary>Aprobar | Rechazar</summary>
    public string Recomendacion { get; set; } = "";
    public string Argumento { get; set; } = "";
}

public class PenalidadDecidirGerenciaRequest
{
    /// <summary>Aplicada | Anulada</summary>
    public string ResolucionTipo { get; set; } = "";
    public string? ResolucionTexto { get; set; }
    public decimal? MontoFinal { get; set; }
    public string? MotivoAjusteMonto { get; set; }
    public string? MotivoObjecionGerencia { get; set; }
}

public class PenalidadApelarRequest
{
    public string Texto { get; set; } = "";
    public string DocumentoUrl { get; set; } = "";
}

public class PenalidadDecidirApelacionRequest
{
    /// <summary>Aplicada | Anulada</summary>
    public string ResolucionTipo { get; set; } = "";
    public string? ResolucionTexto { get; set; }
    public string? MotivoObjecionGerencia { get; set; }
}

public class PenalidadCreadaDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
}

// ── Gestión previa (correos/cartas/reuniones antes de penalizar) ────────────

public class GestionPreviaDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int? ProyectoId { get; set; }
    public string Tipo { get; set; } = "";
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = "";
    public string? AdjuntoUrl { get; set; }
    public string? RegistradoPorNombre { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GestionPreviaRegistrarRequest
{
    public int EmpresaId { get; set; }
    public int? ProyectoId { get; set; }
    /// <summary>Correo | CartaPreocupacion | Reunion | Llamada | Otro</summary>
    public string Tipo { get; set; } = "";
    public DateTime Fecha { get; set; }
    public string Descripcion { get; set; } = "";
    public string? AdjuntoUrl { get; set; }
}

public class ContextoEmpresaDto
{
    public int PenalidadesAplicadasUltimos12Meses { get; set; }
    public int PenalidadesTotalHistorico { get; set; }
    public List<GestionPreviaDto> GestionPrevia { get; set; } = new();
}

// ── Catálogos (antes sin CRUD admin) ────────────────────────────────────────

public class InfraccionAdminDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public decimal? FactorUit { get; set; }
    public decimal? MontoFijo { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}

public class InfraccionUpsertRequest
{
    public string Nombre { get; set; } = "";
    public decimal? FactorUit { get; set; }
    public decimal? MontoFijo { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

public class UitAnioAdminDto
{
    public int Id { get; set; }
    public int Anio { get; set; }
    public decimal Valor { get; set; }
    public bool Activo { get; set; }
}

public class UitAnioUpsertRequest
{
    public int Anio { get; set; }
    public decimal Valor { get; set; }
    public bool Activo { get; set; } = true;
}
