using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models;

// ── Tablas de referencia ─────────────────────────────────────────────────────

public class SsomaFlashTipo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;   // AC, IN, NC, AL
    public string Nombre { get; set; } = string.Empty;   // Accidente, Incidente, No Conformidad, Alerta
    public int Orden { get; set; }
}

public class SsomaFlashEtapaProyecto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class SsomaFlashParteAfectada
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class SsomaEmpresaAbril
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public bool Activa { get; set; } = true;
}

public class SsomaFlashPartida
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

// ── Entidad principal ────────────────────────────────────────────────────────

public class SsomaAccidenteIncidente
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;           // GAR-AC-01
    public int ProyectoId { get; set; }
    public int TipoId { get; set; }
    public DateTime Fecha { get; set; }
    public TimeSpan? Hora { get; set; }
    public string LugarExacto { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = "Borrador";

    // Empresa involucrada (Abril o contratista)
    public int? EmpresaAbrilId { get; set; }
    public int? ContributorId { get; set; }

    public string? JefeInmediatoNombre { get; set; }

    // Etapa y partida
    public int? EtapaProyectoId { get; set; }
    public int? PartidaId { get; set; }

    // Trabajador afectado
    public int? WorkerId { get; set; }
    public string? TrabajadorNombre { get; set; }
    public string? PuestoTrabajo { get; set; }
    public int? Edad { get; set; }
    public int? AniosExperiencia { get; set; }
    public string? CelularTrabajador { get; set; }
    public int? ParteAfectadaId { get; set; }

    // Daños y consecuencias
    public string? DanoProceso { get; set; }
    public int? ConsecuenciaRealPersonal { get; set; }    // 1-6
    public int? ConsecuenciaPotencialPersonal { get; set; }

    // Descripción y acciones
    public string? AccionesInmediatas { get; set; }

    // Elaborado por
    public int? ElaboradoPorId { get; set; }
    public string? ElaboradoPorNombre { get; set; }
    public string? ElaboradoPorCargo { get; set; }
    public string? ElaboradoPorEmail { get; set; }
    public string? ElaboradoPorTelefono { get; set; }

    // Fotos
    public string? UrlFoto1 { get; set; }
    public string? UrlFoto2 { get; set; }

    // Envío
    public bool Enviado { get; set; } = false;
    public DateTime? FechaEnvio { get; set; }
    public string? UrlPdfSharepoint { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navegación
    public Project? Proyecto { get; set; }
    public SsomaFlashTipo? Tipo { get; set; }
    public SsomaEmpresaAbril? EmpresaAbril { get; set; }
    public SsomaFlashEtapaProyecto? EtapaProyecto { get; set; }
    public SsomaFlashPartida? Partida { get; set; }
    public SsomaFlashParteAfectada? ParteAfectada { get; set; }
    public ICollection<SsomaFlashDescanso> Descansos { get; set; } = [];
}

// ── Descansos médicos asociados al flash ─────────────────────────────────────

public class SsomaFlashDescanso
{
    public int Id { get; set; }
    public int AccidenteIncidenteId { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string? Observacion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaAccidenteIncidente? AccidenteIncidente { get; set; }
}

// ── Documentos adjuntos (compatibilidad hacia atrás) ─────────────────────────

public class SsomaAccidenteDocumento
{
    public int Id { get; set; }
    public int AccidenteId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoArchivo { get; set; } = string.Empty;
    public long TamanioBytes { get; set; }
    public string UrlSharepoint { get; set; } = string.Empty;
    public int? UsuarioId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaAccidenteIncidente? Accidente { get; set; }
}
