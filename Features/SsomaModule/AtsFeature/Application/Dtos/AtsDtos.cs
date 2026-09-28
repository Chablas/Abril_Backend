namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;

// ── Catálogos ────────────────────────────────────────────────────────────────

public class AtsProyectoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class AtsPasoDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
}

/// <summary>Un paso de las categorías NO universales (Liberación de Seguridad/Producción/Calidad)
/// con los puestos que hoy lo ven, para la pantalla de administración.</summary>
public class AtsPasoPuestoDto
{
    public int PasoId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public List<int> PuestoIds { get; set; } = [];
}

/// <summary>Fila del listado del Coordinador SSOMA para subir la autorización de uso de firma
/// digital e imagen (firmada en físico) de cada trabajador — sin esto, el trabajador no puede
/// crear/editar un ATS (ver AtsService.Crear/Editar).</summary>
public class AtsAutorizacionTrabajadorDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public int? ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? ObraOficinaStaff { get; set; }
    public bool TieneFirmaDigital { get; set; }
    public bool TieneAutorizacion { get; set; }
    public DateTime? SubidoEn { get; set; }
    public string? ArchivoUrl { get; set; }
}

public class AtsAutorizacionFirmaDigitalRequestDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
}

public class AtsCategoriaPasoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<AtsPasoDto> Pasos { get; set; } = [];
}

public class AtsRiesgoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool RequierePetar { get; set; }
}

/// <summary>Ficha resumida de un PETAR ya generado para un ATS — para mostrarlo enlazado en la
/// lista de ATS sin tener que navegar a otra pantalla.</summary>
public class AtsPetarResumenDto
{
    public int Id { get; set; }
    public string? TipoNombre { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class AtsPeligroDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<AtsRiesgoDto> Riesgos { get; set; } = [];
}

public class AtsRiesgoRequierePetarRequestDto
{
    public bool RequierePetar { get; set; }
}

public class AtsEppDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class AtsHerramientaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
}

/// <summary>Quién puede firmar cada visto de un ATS, resuelto desde el proyecto (no hay tabla de
/// roles por proyecto: la responsabilidad la define la ficha de Project, igual que
/// AlertaLoginSsomaService). Se usa tanto para autorizar el POST cuanto para avisar por correo.</summary>
public class AtsResponsablesDto
{
    public string ProyectoNombre { get; set; } = string.Empty;
    public int? ResidenteWorkerId { get; set; }
    public string? ResidenteNombre { get; set; }
    public string? ResidenteEmail { get; set; }
    /// <summary>Coordinador SSOMA del proyecto — puede ser más de un correo (EmailCoordSsoma + CoordAdmin).</summary>
    public List<string> SsomaEmails { get; set; } = [];
}

public class AtsPuestoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class AtsPlantillaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? PuestoId { get; set; }
    public List<int> PeligroIds { get; set; } = [];
    public List<int> EppIds { get; set; } = [];
    public List<int> HerramientaIds { get; set; } = [];
}

/// <summary>Todo lo que la pantalla de "Nuevo ATS" necesita para armarse: catálogos completos,
/// los pasos que le tocan a ESE trabajador según su puesto (ya filtrados, no los 5 bloques
/// completos), la plantilla sugerida por su puesto si existe, y su proyecto actual (vigencia).</summary>
public class AtsInitDto
{
    public List<AtsProyectoDto> Proyectos { get; set; } = [];
    public int? ProyectoActualId { get; set; }
    public int? PuestoId { get; set; }
    public List<AtsCategoriaPasoDto> Pasos { get; set; } = [];
    public List<AtsPeligroDto> Peligros { get; set; } = [];
    public List<AtsEppDto> Epps { get; set; } = [];
    public List<AtsHerramientaDto> Herramientas { get; set; } = [];
    public List<AtsPlantillaDto> Plantillas { get; set; } = [];
    public int? PlantillaSugeridaId { get; set; }
    public bool TieneConsentimiento { get; set; }
}

// ── Guardar borrador ─────────────────────────────────────────────────────────

public class AtsPasoRequestDto
{
    public int PasoId { get; set; }
    public bool Aplica { get; set; }
}

public class AtsRiesgoDetalleRequestDto
{
    public int PeligroId { get; set; }
    public int RiesgoId { get; set; }
    /// <summary>"A" | "M" | "B" (Alto/Medio/Bajo).</summary>
    public string RiesgoBase { get; set; } = string.Empty;
    public string Controles { get; set; } = string.Empty;
    public string RiesgoResidual { get; set; } = string.Empty;
}

public class AtsGuardarRequestDto
{
    public int ProyectoId { get; set; }
    public int? PlantillaId { get; set; }
    public string Actividad { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public List<AtsPasoRequestDto> Pasos { get; set; } = [];
    public List<int> EppIds { get; set; } = [];
    public List<int> HerramientaIds { get; set; } = [];
    public List<AtsRiesgoDetalleRequestDto> Riesgos { get; set; } = [];
}

// ── Firmas adicionales (Autoriza / Visto Bueno SSOMA) ───────────────────────

/// <summary>Firma de Residente/Ing. Producción o de Prevencionista/Coordinador SSOMA sobre un ATS
/// que el ejecutante ya firmó. Sin selfie ni geolocalización: no es evidencia de presencia física
/// en el lugar de la tarea, es una validación documental que puede darse desde oficina.</summary>
public class AtsFirmarVistoRequestDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
}

// ── Firmar ───────────────────────────────────────────────────────────────────

public class AtsFirmarRequestDto
{
    public string SelfieBase64 { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public bool AceptaConsentimiento { get; set; }
}

// ── Respuesta ────────────────────────────────────────────────────────────────

public class AtsPasoResponseDto
{
    public int PasoId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public bool Aplica { get; set; }
}

public class AtsRiesgoDetalleResponseDto
{
    public int PeligroId { get; set; }
    public int RiesgoId { get; set; }
    public string PeligroNombre { get; set; } = string.Empty;
    public string RiesgoNombre { get; set; } = string.Empty;
    public string RiesgoBase { get; set; } = string.Empty;
    public string Controles { get; set; } = string.Empty;
    public string RiesgoResidual { get; set; } = string.Empty;
}

public class AtsResponseDto
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public string? WorkerNombre { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public int? PuestoId { get; set; }
    public string? PuestoNombre { get; set; }
    public int? PlantillaId { get; set; }

    public string Actividad { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public DateOnly Fecha { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public string? SelfieUrl { get; set; }
    public string? FirmaUrl { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int? AtsAnteriorId { get; set; }
    public string? PdfHash { get; set; }

    public string? AutorizaNombre { get; set; }
    public string? AutorizaCargo { get; set; }
    public string? AutorizaFirmaUrl { get; set; }
    public DateTime? AutorizaHoraServidor { get; set; }

    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }

    /// <summary>Se completan solo cuando GetPorId/Listar los pide para el usuario logueado
    /// (ver AtsService) — el frontend los usa para mostrar u ocultar los botones de firma.</summary>
    public bool PuedeAutorizar { get; set; }
    public bool PuedeVistoBuenoSsoma { get; set; }

    /// <summary>true si algún riesgo marcado en este ATS exige PETAR (ver SsAtsRiesgo.RequierePetar).</summary>
    public bool RequierePetar { get; set; }
    public List<AtsPetarResumenDto> Petares { get; set; } = [];

    public List<AtsPasoResponseDto> Pasos { get; set; } = [];
    public List<string> Epps { get; set; } = [];
    public List<string> Herramientas { get; set; } = [];
    public List<AtsRiesgoDetalleResponseDto> Riesgos { get; set; } = [];
}

/// <summary>Lo mínimo que se muestra públicamente al escanear el QR del PDF — sin datos que
/// identifiquen de más al trabajador (nombre completo sí, pero nada de contacto/DNI).</summary>
public class AtsVerificacionPublicaDto
{
    public bool Valido { get; set; }
    public bool Encontrado { get; set; }
    public string? WorkerNombre { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? Actividad { get; set; }
    public DateOnly? Fecha { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public string? Estado { get; set; }
}

public class AtsFiltroDto
{
    public int? ProyectoId { get; set; }
    public int? WorkerId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public int Page { get; set; } = 1;
}

public class AtsListResponseDto
{
    public List<AtsResponseDto> Data { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}

// ── Administración de plantillas ────────────────────────────────────────────

/// <summary>Fila del mapeo "Plantillas por puesto" — misma idea que AtsPasoPuestoDto pero para
/// autosugerir la plantilla de ATS según el puesto del trabajador.</summary>
public class AtsPlantillaPuestoDto
{
    public int PlantillaId { get; set; }
    public string PlantillaNombre { get; set; } = string.Empty;
    public List<int> PuestoIds { get; set; } = [];
}

public class AtsPlantillaGuardarRequestDto
{
    public string Nombre { get; set; } = string.Empty;
    public int? PuestoId { get; set; }
    public List<int> PeligroIds { get; set; } = [];
    public List<int> EppIds { get; set; } = [];
    public List<int> HerramientaIds { get; set; } = [];
}

// ── Actividades/pasos por plantilla ─────────────────────────────────────────

public class AtsPlantillaPasoDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
}

public class AtsPlantillaActividadDto
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
    public List<AtsPlantillaPasoDto> Pasos { get; set; } = [];
    public List<int> PeligroIds { get; set; } = [];
}

public class AtsPlantillaActividadGuardarRequestDto
{
    public string Texto { get; set; } = string.Empty;
}

public class AtsPlantillaPasoGuardarRequestDto
{
    public string Texto { get; set; } = string.Empty;
}

public class AtsPlantillaActividadPeligrosRequestDto
{
    public List<int> PeligroIds { get; set; } = [];
}

// ── Controles sugeridos por riesgo ──────────────────────────────────────────

public class AtsRiesgoControlDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
}

public class AtsRiesgoConControlesDto
{
    public int RiesgoId { get; set; }
    public string RiesgoNombre { get; set; } = string.Empty;
    public int PeligroId { get; set; }
    public string PeligroNombre { get; set; } = string.Empty;
    public List<AtsRiesgoControlDto> Controles { get; set; } = [];
}

public class AtsRiesgoControlGuardarRequestDto
{
    public string Texto { get; set; } = string.Empty;
}
