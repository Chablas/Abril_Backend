using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models;

// ============================================================================
// CATÁLOGOS — editables desde el front (Coordinador SSOMA en adelante), nunca
// hardcodeados. Tomados del formato vigente SSO-FO-018.m "ATS SUPERVISIÓN".
// ============================================================================

/// <summary>Categoría de pasos/actividades del ATS: Trabajos de gabinete, Liberación de
/// Seguridad/Producción/Calidad, Supervisión en campo.</summary>
public class SsAtsCategoriaPaso
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<SsAtsPaso> Pasos { get; set; } = [];
}

/// <summary>
/// Un paso/actividad dentro de una categoría (ej. "Desarrollar la charla de seguridad").
/// A qué puestos les corresponde ver este paso lo dice <see cref="SsAtsPasoPuesto"/> — la
/// mayoría de puestos ve Gabinete + Supervisión en campo, y solo el puesto dueño de cada
/// Liberación (Seguridad/Producción/Calidad) ve ese bloque.
/// </summary>
public class SsAtsPaso
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Ítem de trabajo de alto riesgo (andamios, elevador, anclajes) — el wizard lo
    /// agrupa aparte de las verificaciones básicas de rutina dentro de la misma categoría.</summary>
    public bool RequierePetar { get; set; }

    public SsAtsCategoriaPaso? Categoria { get; set; }
    public ICollection<SsAtsPasoPuesto> Puestos { get; set; } = [];
}

/// <summary>Qué puestos ven un paso determinado al llenar su ATS.</summary>
public class SsAtsPasoPuesto
{
    public int Id { get; set; }
    public int PasoId { get; set; }
    public int PuestoId { get; set; }

    public SsAtsPaso? Paso { get; set; }
    public Puesto? Puesto { get; set; }
}

/// <summary>Peligro identificable (ej. "Trabajo a distinto nivel (altura)").</summary>
public class SsAtsPeligro
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<SsAtsRiesgo> Riesgos { get; set; } = [];
}

/// <summary>Riesgo asociado a un peligro (ej. peligro "Trabajo a distinto nivel" → riesgo
/// "Caída de personas a distinto nivel"). Un peligro puede tener varios riesgos.</summary>
public class SsAtsRiesgo
{
    public int Id { get; set; }
    public int PeligroId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Si un ATS firmado marca este riesgo y no tiene ningún PETAR generado, se avisa en
    /// la lista (badge de advertencia) — no bloquea, ver PetarFeature.</summary>
    public bool RequierePetar { get; set; }

    public SsAtsPeligro? Peligro { get; set; }
}

/// <summary>EPP (ej. "Casco"). <see cref="Categoria"/> distingue "Básico" (va siempre, sin
/// importar la tarea) de "Específico" (depende de la tarea/peligro concreto) — antes era una
/// sola lista plana y no quedaba claro cuál era cuál al llenar el ATS.</summary>
public class SsAtsEpp
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = "Específico";
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Herramienta/equipo (ej. "Esmeril angular"). <see cref="Categoria"/> es solo para
/// agrupar visualmente (Herramientas manuales / Equipos de poder / Varios), no cambia el
/// comportamiento.</summary>
public class SsAtsHerramienta
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
}

// ============================================================================
// PLANTILLA — set de peligros/EPP/herramientas sugeridos, editable por Coordinador
// SSOMA en adelante. Los pasos NO se duplican acá: salen directo de SsAtsPasoPuesto
// según el puesto del trabajador. La plantilla es solo una sugerencia de partida —
// el trabajador puede agregar o quitar peligros al llenar su ATS.
// ============================================================================

public class SsAtsPlantilla
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    /// <summary>Si tiene puesto, se autoselecciona para trabajadores de ese puesto. Null = de uso manual.</summary>
    public int? PuestoId { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Puesto? Puesto { get; set; }
    public ICollection<SsAtsPlantillaPeligro> Peligros { get; set; } = [];
    public ICollection<SsAtsPlantillaEpp> Epps { get; set; } = [];
    public ICollection<SsAtsPlantillaHerramienta> Herramientas { get; set; } = [];
}

/// <summary>Qué puestos autosugieren esta plantilla (muchos-a-muchos, igual que
/// <see cref="SsAtsPasoPuesto"/>) — una especialidad puede corresponder a varios puestos reales del
/// catálogo (ej. "ATS Albañilería" → ALBAÑIL, OFICIAL ALBAÑIL, PEÓN). Reemplaza a
/// <see cref="SsAtsPlantilla.PuestoId"/> (1-a-1), que queda en desuso.</summary>
public class SsAtsPlantillaPuesto
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }
    public int PuestoId { get; set; }

    public SsAtsPlantilla? Plantilla { get; set; }
    public Puesto? Puesto { get; set; }
}

public class SsAtsPlantillaPeligro
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }
    public int PeligroId { get; set; }

    public SsAtsPlantilla? Plantilla { get; set; }
    public SsAtsPeligro? Peligro { get; set; }
}

public class SsAtsPlantillaEpp
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }
    public int EppId { get; set; }

    public SsAtsPlantilla? Plantilla { get; set; }
    public SsAtsEpp? Epp { get; set; }
}

public class SsAtsPlantillaHerramienta
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }
    public int HerramientaId { get; set; }

    public SsAtsPlantilla? Plantilla { get; set; }
    public SsAtsHerramienta? Herramienta { get; set; }
}

/// <summary>Actividad madre de la tarea dentro de una plantilla (ej. "Asentado de ladrillos"),
/// sacada de la sección "Identifique las actividades y pasos de la tarea" del ATS en papel.
/// Contiene sub-pasos (<see cref="SsAtsPlantillaPaso"/>) y peligros sugeridos
/// (<see cref="SsAtsPlantillaActividadPeligro"/>).</summary>
public class SsAtsPlantillaActividad
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;

    public SsAtsPlantilla? Plantilla { get; set; }
    public ICollection<SsAtsPlantillaPaso> Pasos { get; set; } = [];
    public ICollection<SsAtsPlantillaActividadPeligro> Peligros { get; set; } = [];
}

/// <summary>Sub-paso de una actividad (ej. "Traslado de materiales y herramientas").</summary>
public class SsAtsPlantillaPaso
{
    public int Id { get; set; }
    public int ActividadId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;

    public SsAtsPlantillaActividad? Actividad { get; set; }
}

/// <summary>Qué peligros del catálogo aplican a una actividad concreta — al marcarla en el ATS,
/// solo se sugieren estos en vez de todo el catálogo de la plantilla.</summary>
public class SsAtsPlantillaActividadPeligro
{
    public int Id { get; set; }
    public int ActividadId { get; set; }
    public int PeligroId { get; set; }

    public SsAtsPlantillaActividad? Actividad { get; set; }
    public SsAtsPeligro? Peligro { get; set; }
}

/// <summary>Control sugerido para un riesgo (ej. riesgo "Sobreesfuerzos" → "Pausas activas y
/// rotación de personal"). El trabajador los marca en vez de escribir el control a mano.</summary>
public class SsAtsRiesgoControl
{
    public int Id { get; set; }
    public int RiesgoId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Eliminacion, Sustitucion, Ingenieria, Administrativo o Epp — jerarquía de controles.</summary>
    public string Tipo { get; set; } = "Administrativo";

    public SsAtsRiesgo? Riesgo { get; set; }
}

// ============================================================================
// INSTANCIA — el ATS que efectivamente llena y firma cada trabajador. Legalmente
// es UN ATS por lugar/tarea de riesgo distinta (no un recorrido genérico de toda
// la obra): si el mismo trabajador hace una tarea puntual de mayor riesgo en otro
// lugar el mismo día, eso es OTRO ATS. Una vez Estado = "Firmado" es inmutable
// (misma regla que antes: una corrección crea un ATS nuevo con AtsAnteriorId).
// ============================================================================

public class SsAts
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public int ProyectoId { get; set; }
    /// <summary>Puesto del trabajador al momento de llenar el ATS — de acá salen los pasos que le tocan (SsAtsPasoPuesto). Snapshot: si luego cambia de puesto, este ATS no cambia.</summary>
    public int? PuestoId { get; set; }
    /// <summary>Plantilla de la que partió (si eligió una). Solo referencia informativa, no afecta el contenido ya guardado.</summary>
    public int? PlantillaId { get; set; }

    public string Actividad { get; set; } = string.Empty;
    /// <summary>Torre/bloque elegida (nombre de project_torre, ej. "A") — null cuando el lugar
    /// es exterior/fachada/vecinos (sin torre).</summary>
    public string? TorreNombre { get; set; }
    /// <summary>Uno o varios pisos/niveles de esa torre, separados por coma (ej. "Piso 3, Piso 4")
    /// — un ATS de supervisión puede cubrir varios niveles a la vez. Siempre se pide (nunca
    /// "torre completa" a secas) salvo cuando el lugar es exterior.</summary>
    public string? Pisos { get; set; }
    /// <summary>Lugar específico de ESTA tarea (no una lista de toda la obra — ver regla de un ATS por lugar/tarea).</summary>
    public string? Lugar { get; set; }

    public DateOnly Fecha { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public DateTime? HoraDispositivo { get; set; }

    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }

    public string? SelfieUrl { get; set; }
    public string? SelfieHash { get; set; }

    public string? FirmaUrl { get; set; }
    public string? FirmaHash { get; set; }

    public string? IpOrigen { get; set; }
    public string? UserAgent { get; set; }

    public string Estado { get; set; } = "Borrador";
    public int? AtsAnteriorId { get; set; }

    public string? PdfUrl { get; set; }
    public string? PdfHash { get; set; }

    /// <summary>
    /// "Autoriza" = Residente / Ingeniero de Producción del proyecto: firma que la actividad
    /// está planificada y el frente en condiciones. NO reemplaza la firma del ejecutante
    /// (Worker/FirmaUrl), es una firma adicional que se agrega DESPUÉS de que el ejecutante ya
    /// firmó (Estado == "Firmado"). Requisito del Art. 76 del Reglamento de la Ley 29783.
    /// </summary>
    public int? AutorizaWorkerId { get; set; }
    public string? AutorizaNombre { get; set; }
    public string? AutorizaCargo { get; set; }
    public string? AutorizaFirmaUrl { get; set; }
    public string? AutorizaFirmaHash { get; set; }
    public DateTime? AutorizaHoraServidor { get; set; }

    /// <summary>
    /// "Visto Bueno SSOMA" = Prevencionista / Coordinador SSOMA: NO es quien autoriza el
    /// trabajo, es la verificación técnica de que el análisis de riesgo (peligro→riesgo→
    /// controles→residual) está bien hecho. Obligatorio para trabajos de alto riesgo — no se
    /// quita, es lo que SUNAFIL pide ver ante un accidente.
    /// </summary>
    public int? SsomaWorkerId { get; set; }
    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public string? SsomaFirmaHash { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Presente cuando este ATS nació de "unirse" a un ATS grupal (QR) en vez del wizard
    /// individual completo — su contenido (Pasos/Epps/Herramientas/RiesgosDetalle de abajo) es una
    /// COPIA tomada del grupo al momento de adherirse, no una referencia viva: si el grupo se
    /// edita después, este ATS ya firmado no cambia (misma regla de inmutabilidad que siempre).</summary>
    public int? AtsGrupoId { get; set; }

    public Worker? Worker { get; set; }
    public Worker? AutorizaWorker { get; set; }
    public Worker? SsomaWorker { get; set; }
    public Project? Proyecto { get; set; }
    public Puesto? Puesto { get; set; }
    public SsAtsPlantilla? Plantilla { get; set; }
    public SsAts? AtsAnterior { get; set; }
    public SsAtsGrupo? AtsGrupo { get; set; }

    public ICollection<SsAtsPasoSeleccionado> Pasos { get; set; } = [];
    public ICollection<SsAtsEppSeleccionado> Epps { get; set; } = [];
    public ICollection<SsAtsHerramientaSeleccionada> Herramientas { get; set; } = [];
    public ICollection<SsAtsRiesgoDetalle> RiesgosDetalle { get; set; } = [];
}

/// <summary>
/// ATS GRUPAL: el contenido (actividad, lugar, pasos, EPP, herramientas, riesgos+valoración) se
/// llena UNA sola vez para toda la cuadrilla — cualquier trabajador puede crearlo (decisión de
/// Samuel 2026-09-30: en la práctica cualquiera está en capacidad de hacerlo). Cada integrante de
/// la cuadrilla no llena nada de esto: solo escanea el QR (<see cref="QrToken"/>) y hace su propia
/// adhesión liviana (DNI corto + selfie + geolocalización + firma), que crea SU PROPIA fila en
/// <see cref="SsAts"/> (con <see cref="SsAts.AtsGrupoId"/> apuntando acá) copiando este contenido —
/// así el resto del sistema (PDF, permisos, listado, PETAR individual) no se entera de la
/// diferencia, sigue viendo un SsAts normal por persona.
/// </summary>
public class SsAtsGrupo
{
    public int Id { get; set; }
    public int CreadoPorWorkerId { get; set; }
    public int ProyectoId { get; set; }
    public int? PlantillaId { get; set; }

    public string Actividad { get; set; } = string.Empty;
    public string? TorreNombre { get; set; }
    public string? Pisos { get; set; }
    public string? Lugar { get; set; }

    public DateOnly Fecha { get; set; }

    /// <summary>Token único embebido en el QR/link de adhesión — no requiere login, es el único
    /// "candado" de acceso a la página de firma liviana. Acotado a este grupo y expira solo.</summary>
    public Guid QrToken { get; set; } = Guid.NewGuid();
    public DateTime QrExpiraEn { get; set; }

    /// <summary>"Activo" acepta nuevas adhesiones; "Cerrado" (el autor lo cierra manualmente, o
    /// expiró el QR) deja de aceptarlas — los ATS ya adheridos no se ven afectados.</summary>
    public string Estado { get; set; } = "Activo";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Worker? CreadoPorWorker { get; set; }
    public Project? Proyecto { get; set; }
    public SsAtsPlantilla? Plantilla { get; set; }

    public ICollection<SsAtsGrupoPasoSeleccionado> Pasos { get; set; } = [];
    public ICollection<SsAtsGrupoEppSeleccionado> Epps { get; set; } = [];
    public ICollection<SsAtsGrupoHerramientaSeleccionada> Herramientas { get; set; } = [];
    public ICollection<SsAtsGrupoRiesgoDetalle> RiesgosDetalle { get; set; } = [];
    public ICollection<SsAts> Adhesiones { get; set; } = [];
}

/// <summary>Mismo shape que <see cref="SsAtsPasoSeleccionado"/>, a nivel de grupo.</summary>
public class SsAtsGrupoPasoSeleccionado
{
    public int Id { get; set; }
    public int AtsGrupoId { get; set; }
    public int? PasoId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public bool Aplica { get; set; }
    public short Orden { get; set; }

    public SsAtsGrupo? AtsGrupo { get; set; }
}

/// <summary>Mismo shape que <see cref="SsAtsEppSeleccionado"/>, a nivel de grupo.</summary>
public class SsAtsGrupoEppSeleccionado
{
    public int Id { get; set; }
    public int AtsGrupoId { get; set; }
    public int EppId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public SsAtsGrupo? AtsGrupo { get; set; }
}

/// <summary>Mismo shape que <see cref="SsAtsHerramientaSeleccionada"/>, a nivel de grupo.</summary>
public class SsAtsGrupoHerramientaSeleccionada
{
    public int Id { get; set; }
    public int AtsGrupoId { get; set; }
    public int? HerramientaId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public SsAtsGrupo? AtsGrupo { get; set; }
}

/// <summary>Mismo shape que <see cref="SsAtsRiesgoDetalle"/>, a nivel de grupo.</summary>
public class SsAtsGrupoRiesgoDetalle
{
    public int Id { get; set; }
    public int AtsGrupoId { get; set; }
    public int PeligroId { get; set; }
    public int RiesgoId { get; set; }
    public string PeligroNombre { get; set; } = string.Empty;
    public string RiesgoNombre { get; set; } = string.Empty;
    public string RiesgoBase { get; set; } = string.Empty;
    public string Controles { get; set; } = string.Empty;
    public string RiesgoResidual { get; set; } = string.Empty;
    public short Orden { get; set; }

    public SsAtsGrupo? AtsGrupo { get; set; }
}

/// <summary>
/// Snapshot de qué pasos marcó Sí/No ese día. Se copian el texto y la categoría al momento de
/// crear el ATS (no se referencia solo el catálogo) para que un cambio posterior al catálogo
/// nunca reescriba, ni siquiera visualmente, un ATS ya firmado.
/// </summary>
public class SsAtsPasoSeleccionado
{
    public int Id { get; set; }
    public int AtsId { get; set; }
    /// <summary>Null cuando es un paso "de una sola vez" que el trabajador escribió a mano para
    /// este ATS puntual — no viene del catálogo ni lo modifica.</summary>
    public int? PasoId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public bool Aplica { get; set; }
    public short Orden { get; set; }

    public SsAts? Ats { get; set; }
}

public class SsAtsEppSeleccionado
{
    public int Id { get; set; }
    public int AtsId { get; set; }
    public int EppId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public SsAts? Ats { get; set; }
}

public class SsAtsHerramientaSeleccionada
{
    public int Id { get; set; }
    public int AtsId { get; set; }
    /// <summary>Null cuando es una herramienta "otros" escrita a mano para este ATS puntual — no
    /// viene del catálogo ni lo modifica.</summary>
    public int? HerramientaId { get; set; }
    public string Nombre { get; set; } = string.Empty;

    public SsAts? Ats { get; set; }
}

/// <summary>
/// El corazón del IPERC: por cada peligro marcado como aplicable, el riesgo asociado
/// específico, el nivel de riesgo BASE (antes de controles), los controles definidos, y el
/// riesgo RESIDUAL (después de aplicar esos controles). "A"/"M"/"B" = Alto/Medio/Bajo.
/// </summary>
public class SsAtsRiesgoDetalle
{
    public int Id { get; set; }
    public int AtsId { get; set; }
    public int PeligroId { get; set; }
    public int RiesgoId { get; set; }
    public string PeligroNombre { get; set; } = string.Empty;
    public string RiesgoNombre { get; set; } = string.Empty;
    public string RiesgoBase { get; set; } = string.Empty;
    public string Controles { get; set; } = string.Empty;
    public string RiesgoResidual { get; set; } = string.Empty;
    public short Orden { get; set; }

    public SsAts? Ats { get; set; }
}

/// <summary>Log de auditoría INSERT-ONLY, hash encadenado (ver AtsRepository.ComputeHash) — igual que antes.</summary>
public class SsAtsAuditLog
{
    public long Id { get; set; }
    public int AtsId { get; set; }
    public string Evento { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? IpOrigen { get; set; }
    public string? Detalle { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? HashAnterior { get; set; }
    public string Hash { get; set; } = string.Empty;

    public SsAts? Ats { get; set; }
}

/// <summary>Consentimiento de uso de imagen/geolocalización, una vez por trabajador — igual que antes.</summary>
public class SsAtsConsentimiento
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public DateTime AceptadoEn { get; set; } = DateTime.UtcNow;
    public string? IpOrigen { get; set; }
    public string VersionTexto { get; set; } = "v1";

    public Worker? Worker { get; set; }
}

/// <summary>Autorización de uso de firma digital e imagen. Flujo: el Coordinador SSOMA primero
/// captura en pantalla la firma DIGITAL del trabajador (<see cref="FirmaDigitalUrl"/>) — recién con
/// eso se puede descargar el PDF (que ya trae esa firma impresa junto a un espacio para la firma
/// física). El trabajador firma en físico al lado, y el Coordinador escanea y sube ese documento
/// (<see cref="ArchivoUrl"/>) como evidencia final. Sin <see cref="ArchivoUrl"/> el trabajador NO
/// puede crear ni editar un ATS (ver AtsService.Crear/Editar) — la firma digital sola no habilita,
/// es solo el paso previo obligatorio. Mismo patrón que AcTareoAutorizacion (SSO-FO-150 de
/// Arquitectura Comercial), pero es un concepto propio de SSOMA/ATS, no reutiliza esa tabla.</summary>
public class SsAtsAutorizacionPermiso
{
    public int Id { get; set; }
    public int WorkerId { get; set; }

    /// <summary>Escaneado del documento firmado en físico (con la firma digital ya impresa). Null
    /// mientras solo existe la firma digital — ver <see cref="FirmaDigitalUrl"/>.</summary>
    public string? ArchivoUrl { get; set; }
    public int? SubidoPorUserId { get; set; }
    public DateTime? SubidoEn { get; set; }

    /// <summary>Firma capturada en pantalla por el Coordinador SSOMA antes de generar el PDF —
    /// requisito previo a poder descargar la plantilla y subir el escaneado.</summary>
    public string? FirmaDigitalUrl { get; set; }
    public string? FirmaDigitalHash { get; set; }
    public DateTime? FirmadoDigitalEn { get; set; }
    public int? FirmadoDigitalPorUserId { get; set; }

    public Worker? Worker { get; set; }
}
