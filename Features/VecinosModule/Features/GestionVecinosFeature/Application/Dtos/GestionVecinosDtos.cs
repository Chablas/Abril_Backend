using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Dtos
{
    /// <summary>Opción genérica de catálogo (id + descripción).</summary>
    public class CatalogOptionDto
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = null!;
    }

    public class ProjectOptionDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
    }

    /// <summary>Opciones para filtros y para el formulario de creación.</summary>
    public class VecinoFormOptionsDto
    {
        public List<ProjectOptionDto> Projects { get; set; } = new();
        public List<CatalogOptionDto> Colindancias { get; set; } = new();
        public List<CatalogOptionDto> TiposConstruccion { get; set; } = new();
    }

    /// <summary>Fila de la lista/tarjeta de vecinos (contiene todo lo necesario para el detalle).</summary>
    public class VecinoListItemDto
    {
        public int VecinoId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string? Predio { get; set; }
        public string Direccion { get; set; } = null!;
        public string? InteriorDepartamento { get; set; }
        public string NombrePropietario { get; set; } = null!;
        public string Dni { get; set; } = null!;
        public string? Celular { get; set; }
        public int VecinoColindanciaId { get; set; }
        public string ColindanciaDescripcion { get; set; } = null!;
        public int VecinoTipoConstruccionId { get; set; }
        public string TipoConstruccionDescripcion { get; set; } = null!;
        public DateTime CreatedDateTime { get; set; }
        public int SolicitudesCount { get; set; }
        public int CompromisosCount { get; set; }
        /// <summary>Solicitudes aprobadas (Aceptada) del vecino.</summary>
        public int SolicitudesAprobadas { get; set; }
        /// <summary>Solicitudes evaluables del vecino (Aceptada + Por responder, sin Denegada).</summary>
        public int SolicitudesEvaluables { get; set; }
        /// <summary>Entregables aprobados del vecino.</summary>
        public int EntregablesAprobados { get; set; }
        /// <summary>Entregables evaluables del vecino (Falta + Enviado + Aprobado, sin "No aplica").</summary>
        public int EntregablesEvaluables { get; set; }
        /// <summary>Requisitos subidos del vecino.</summary>
        public int RequisitosSubidos { get; set; }
        /// <summary>Requisitos evaluables del vecino (Subido + No subido, sin "No aplica").</summary>
        public int RequisitosEvaluables { get; set; }
    }

    /// <summary>Respuesta del load inicial: opciones de filtros/formulario + primera página.</summary>
    public class VecinosPageDto
    {
        public VecinoFormOptionsDto Options { get; set; } = new();
        public PagedResult<VecinoListItemDto> Vecinos { get; set; } = new();
    }

    public class VecinoFilterDto
    {
        public int Page { get; set; } = 1;
        public int? ProjectId { get; set; }
        public int? VecinoColindanciaId { get; set; }
        public string? Search { get; set; }
    }

    public class VecinoCreateDto
    {
        public int ProjectId { get; set; }
        public string? Predio { get; set; }
        public string Direccion { get; set; } = null!;
        public string? InteriorDepartamento { get; set; }
        public string NombrePropietario { get; set; } = null!;
        public string Dni { get; set; } = null!;
        public string? Celular { get; set; }
        public int VecinoColindanciaId { get; set; }
        public int VecinoTipoConstruccionId { get; set; }
    }

    // ── Solicitudes ─────────────────────────────────────────────────────────
    public class VecinoSolicitudItemDto
    {
        public int VecinoSolicitudId { get; set; }
        public int VecinoId { get; set; }
        public string Descripcion { get; set; } = null!;
        public bool EsCritica { get; set; }
        public int VecinoSolicitudEstadoId { get; set; }
        public string EstadoDescripcion { get; set; } = null!;
        public DateTime CreatedDateTime { get; set; }
    }

    /// <summary>Respuesta al abrir el detalle: solicitudes + catálogos de solicitud, compromiso y entregables.</summary>
    public class VecinoSolicitudesResponseDto
    {
        public List<VecinoSolicitudItemDto> Solicitudes { get; set; } = new();
        public List<CatalogOptionDto> Estados { get; set; } = new();
        public List<CatalogOptionDto> CompromisoEstados { get; set; } = new();
        public List<CatalogOptionDto> EntregableEstados { get; set; } = new();
    }

    public class VecinoSolicitudCreateDto
    {
        public string Descripcion { get; set; } = null!;
        public bool EsCritica { get; set; }
    }

    public class VecinoSolicitudEstadoUpdateDto
    {
        public int VecinoSolicitudEstadoId { get; set; }
    }

    // ── Compromisos ─────────────────────────────────────────────────────────
    public class VecinoEntregableItemDto
    {
        public int VecinoCompromisoEntregableId { get; set; }
        public int VecinoEntregableTipoId { get; set; }
        public string TipoDescripcion { get; set; } = null!;
        public int Orden { get; set; }
        public int VecinoEntregableEstadoId { get; set; }
        public string EstadoDescripcion { get; set; } = null!;
    }

    public class VecinoCompromisoItemDto
    {
        public int VecinoCompromisoId { get; set; }
        public int VecinoSolicitudId { get; set; }
        public string Descripcion { get; set; } = null!;
        public bool EsCritico { get; set; }
        public int VecinoCompromisoEstadoId { get; set; }
        public string EstadoDescripcion { get; set; } = null!;
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFin { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public List<VecinoEntregableItemDto> Entregables { get; set; } = new();
    }

    public class VecinoCompromisoCreateDto
    {
        public string Descripcion { get; set; } = null!;
        public bool EsCritico { get; set; }
        public int? VecinoCompromisoEstadoId { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFin { get; set; }
    }

    public class VecinoCompromisoEstadoUpdateDto
    {
        public int VecinoCompromisoEstadoId { get; set; }
    }

    public class VecinoEntregableEstadoUpdateDto
    {
        public int VecinoEntregableEstadoId { get; set; }
    }

    // ── Requisitos (Gestión de requisitos) ────────────────────────────────────
    /// <summary>Un requisito del vecino: tipo, estado y archivo (si tiene).</summary>
    public class VecinoRequisitoItemDto
    {
        /// <summary>Id del registro (null si aún no existe fila para ese tipo).</summary>
        public int? VecinoRequisitoId { get; set; }
        public int VecinoRequisitoTipoId { get; set; }
        public string TipoDescripcion { get; set; } = null!;
        public int Orden { get; set; }
        public int VecinoRequisitoEstadoId { get; set; }
        public string EstadoDescripcion { get; set; } = null!;
        public string? ArchivoUrl { get; set; }
        public string? OriginalFileName { get; set; }
    }

    public class VecinoRequisitosResponseDto
    {
        public List<VecinoRequisitoItemDto> Requisitos { get; set; } = new();
        public List<CatalogOptionDto> Estados { get; set; } = new();
    }

    public class VecinoRequisitoEstadoUpdateDto
    {
        public int VecinoRequisitoEstadoId { get; set; }
    }

    public class VecinoRequisitoNoAplicaDto
    {
        public bool NoAplica { get; set; }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────
    /// <summary>Conteo de registros por estado de catálogo (para los donuts).</summary>
    public class VecinosDashboardEstadoDto
    {
        public int EstadoId { get; set; }
        public string Descripcion { get; set; } = null!;
        public int Count { get; set; }
    }

    /// <summary>Bloque del dashboard: usado tanto por proyecto como por el resumen global.</summary>
    public class VecinosDashboardProjectDto
    {
        /// <summary>0 cuando representa el resumen general (agregado global).</summary>
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public int VecinosCount { get; set; }
        public List<VecinosDashboardEstadoDto> Solicitudes { get; set; } = new();
        public List<VecinosDashboardEstadoDto> Compromisos { get; set; } = new();
    }

    /// <summary>Respuesta del dashboard: primero el resumen global, luego cada proyecto.</summary>
    public class VecinosDashboardDto
    {
        public VecinosDashboardProjectDto Resumen { get; set; } = new();
        public List<VecinosDashboardProjectDto> Proyectos { get; set; } = new();
    }
}
