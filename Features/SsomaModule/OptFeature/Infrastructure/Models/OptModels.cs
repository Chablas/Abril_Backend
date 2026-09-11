using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.OptFeature.Infrastructure.Models;

public class SsomaOpt
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public int? PetId { get; set; }
    public DateTime Fecha { get; set; }
    public string TipoObservacion { get; set; } = string.Empty;
    public bool CuentaConPet { get; set; }
    public string? Area { get; set; }
    public bool SeInformaTrabajador { get; set; }
    public int? ObservadorId { get; set; }
    public string? ObservadorNombre { get; set; }
    public string? ObservadorCargo { get; set; }
    public string? FirmaObservadorUrl { get; set; }
    public bool SeFelicito { get; set; }
    public bool SeRecibieronComentarios { get; set; }
    public bool SeRetroalimento { get; set; }
    [Column("se_obtuvo_compromiso")]
    public bool SeObtuvoCCompromiso { get; set; }
    public string? AccionRequerida { get; set; }
    public string? AccionObservacion { get; set; }

    // El observador NUNCA modifica el PETS directamente (es peligroso: el contenido
    // pasa por revisión de SSOMA antes de publicarse) — solo deja indicado que hace
    // falta y qué. Al guardar, si viene marcado, el PETS queda "pendiente de
    // revisión" (mismo campo que dispara un accidente/incidente, ver SsomaPet).
    public bool RequierePetModificacion { get; set; }
    public string? RequierePetModificacionNota { get; set; }

    public int TotalPasos { get; set; }
    public int TotalSeguros { get; set; }
    public int TotalInseguros { get; set; }
    public decimal? ScorePct { get; set; }
    public string Estado { get; set; } = "Completado";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }

    public Project? Proyecto { get; set; }
    public SsomaPet? Pet { get; set; }
    public Worker? Observador { get; set; }
    public ICollection<SsomaOptTrabajador> Trabajadores { get; set; } = [];
    public ICollection<SsomaOptVerificacion> Verificaciones { get; set; } = [];
    public ICollection<SsomaOptPaso> Pasos { get; set; } = [];
    public ICollection<SsomaOptFotoArea> FotosArea { get; set; } = [];
}

public class SsomaOptTrabajador
{
    public int Id { get; set; }
    public int OptId { get; set; }
    public int TrabajadorId { get; set; }
    public string? TipoTrabajador { get; set; }
    public string? TiempoEnObra { get; set; }
    public string? AniosExperiencia { get; set; }
    public string? FirmaTrabajadorUrl { get; set; }

    public SsomaOpt? Opt { get; set; }
    public Worker? Trabajador { get; set; }
}

public class SsomaPet
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public string? SharepointUrl { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // "Abril" (default, catálogo propio — global, válido en cualquier proyecto) |
    // "Contratista" (el contratista trae su propio documento). Un PETS de
    // contratista SIEMPRE está atado a un proyecto puntual (ProyectoId obligatorio)
    // — si trabaja en otra obra, se usa "Duplicar" para clonarlo y reasignarlo a
    // ese otro proyecto, no se reutiliza el mismo registro entre obras.
    public string Origen { get; set; } = "Abril";
    public int? ContributorId { get; set; }
    public int? ProyectoId { get; set; }

    // "borrador" | "aprobado" — cualquier edición (paso, imagen, catálogo, texto,
    // firma, anexo) lo vuelve a poner en "borrador" automáticamente, aunque ya
    // hubiera una versión aprobada antes. Así el estado nunca miente sobre si lo que
    // se está viendo/editando coincide con la última versión oficial.
    public string EstadoRevision { get; set; } = "borrador";

    // 0 = nunca se aprobó ninguna versión. Referencia al número más alto en
    // SsomaPetVersion para este PetId — no una FK directa porque el histórico de
    // versiones se consulta por (PetId, NumeroVersion), no por Id de fila.
    public int VersionVigente { get; set; }

    // Eje independiente de EstadoRevision: un PETS puede estar "aprobado" y aun así
    // quedar marcado para revisión porque ocurrió un accidente/incidente que lo tenía
    // asociado (ver AccidenteIncidenteRepository.MarcarEnviadoAsync). Se limpia solo
    // al aprobar una versión nueva (AprobarVersionAsync) — esa aprobación ES la
    // revisión que se pedía.
    public bool RevisionPendiente { get; set; }
    public string? RevisionPendienteMotivo { get; set; }
    public DateTime? RevisionPendienteFecha { get; set; }

    public ICollection<SsomaPetPaso> Pasos { get; set; } = [];
}

// Una "foto" completa del PETS en el momento de aprobarlo — para que el PDF oficial
// entregado por QR/impreso sea siempre una versión aprobada estable, y nunca cambie
// bajo los pies de alguien en campo mientras alguien más lo sigue editando en borrador.
// El contenido va serializado en SnapshotJson (mismo shape que PetDetalleDto) en vez
// de duplicar todas las tablas de pasos/catálogo con un VersionId — más simple y
// suficiente porque una versión aprobada nunca se edita, solo se lee.
public class SsomaPetVersion
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public int NumeroVersion { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;

    // Motivo obligatorio al aprobar (ej. "Revisión periódica", "Accidente #123",
    // "Corrección de redacción") — queda en el historial visible, es la respuesta a
    // "por qué cambió" que un trabajador en campo debería poder ver.
    public string Motivo { get; set; } = string.Empty;

    public int? AprobadoPorId { get; set; }
    public string AprobadoPorNombre { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaPet? Pet { get; set; }
}

// Catálogo de pasos del PETS: piloto para que OPT (y a futuro otras herramientas)
// jalen automáticamente la estructura del PETS en vez de tipearla a mano cada vez.
// "Orden" es solo la clave de ordenamiento interna — el número que se muestra al
// usuario ("Paso 4") se calcula por posición, nunca se guarda como texto fijo, para
// que insertar un paso en medio no requiera renumerar nada a mano.
public class SsomaPetPaso
{
    public int Id { get; set; }
    public int PetId { get; set; }

    // Sección del documento a la que pertenece este árbol: procedimiento (original) |
    // introduccion | alcance | objetivo | definiciones | responsabilidades |
    // restricciones. Cada sección es un árbol independiente dentro del mismo PetId —
    // "hermanos" se agrupan por (PetId, Seccion, ParentId).
    public string Seccion { get; set; } = "procedimiento";

    // Árbol: null = nivel superior de la sección. "Orden" es la posición entre
    // los HERMANOS del mismo ParentId (no global) — así insertar/reordenar dentro de
    // un subtítulo no toca el orden de otro subtítulo.
    public int? ParentId { get; set; }

    // subtitulo | paso | letra | guion — controla cómo se numera/viñetea al mostrarlo,
    // nunca se guarda el número/letra como texto fijo.
    public string Tipo { get; set; } = "paso";

    public string Descripcion { get; set; } = string.Empty;

    // Se mantiene por compatibilidad con datos ya guardados (una sola imagen por
    // paso) — las imágenes NUEVAS van a SsomaPetPasoImagen (varias por paso). No se
    // vuelve a escribir acá desde el código nuevo.
    public string? ImagenUrl { get; set; }

    // Etiqueta libre para resaltar el paso por tema transversal (hoy solo
    // "medio_ambiente"; queda como texto y no como bool para poder sumar categorías
    // nuevas — ej. "calidad" — sin otra migración). null = sin categoría.
    public string? Categoria { get; set; }

    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public SsomaPet? Pet { get; set; }
    public SsomaPetPaso? Parent { get; set; }
    public ICollection<SsomaPetPaso> Hijos { get; set; } = [];
    public ICollection<SsomaPetPasoImagen> Imagenes { get; set; } = [];
}

// Varias imágenes por paso (un paso de Procedimiento puede traer 2-3 fotos juntas en
// el Word original) — reemplaza la limitación de "una sola imagen" de SsomaPetPaso.
// ImagenUrl.
public class SsomaPetPasoImagen
{
    public int Id { get; set; }
    public int PasoId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaPetPaso? Paso { get; set; }
}

public class SsomaOptCriterioVerificacion
{
    public int Id { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public class SsomaOptVerificacion
{
    public int Id { get; set; }
    public int OptId { get; set; }
    public int CriterioId { get; set; }
    public bool Resultado { get; set; }

    public SsomaOpt? Opt { get; set; }
    public SsomaOptCriterioVerificacion? Criterio { get; set; }
}

public class SsomaOptFotoArea
{
    public int Id { get; set; }
    public int OptId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Orden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaOpt? Opt { get; set; }
}

public class SsomaOptPaso
{
    public int Id { get; set; }
    public int OptId { get; set; }
    public string NumeroDisplay { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Nivel { get; set; } = 1;
    public string? Resultado { get; set; }
    public string? DesviacionObservada { get; set; }
    public int Orden { get; set; }

    public SsomaOpt? Opt { get; set; }
}
