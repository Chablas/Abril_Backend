namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos
{
    public class GestionSalidaListItemDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public DateOnly FechaSalida { get; set; }
        /// <summary>Hora de salida del primer trayecto.</summary>
        public TimeOnly HoraSalida { get; set; }
        /// <summary>Hora de retorno del último trayecto.</summary>
        public TimeOnly? HoraRetorno { get; set; }
        /// <summary>Motivo del primer trayecto.</summary>
        public string Motivo { get; set; } = string.Empty;
        /// <summary>Origen del primer trayecto.</summary>
        public string? LugarOrigen { get; set; }
        /// <summary>Destino del último trayecto.</summary>
        public string? LugarDestino { get; set; }
        public int TrayectosCount { get; set; }
        public string EstadoAprobacion { get; set; } = string.Empty;
        public string EstadoRendicion { get; set; } = "No rendido";
        public DateTimeOffset CreatedAt { get; set; }
        /// <summary>True si todos los trayectos tienen al menos una captura — habilita la rendición.</summary>
        public bool PuedeRendirse { get; set; }
        /// <summary>Hora real registrada por recepción. Dato extra, opcional.</summary>
        public TimeOnly? HoraSalidaReal { get; set; }
    }

    public class RegistrarHoraSalidaRealDto
    {
        /// <summary>"HH:mm" o null para limpiar.</summary>
        public TimeOnly? HoraSalidaReal { get; set; }
    }

    public class GestionSalidaFiltersDto
    {
        public int? WorkerId { get; set; }
        public int? LugarProyectoId { get; set; }
        public string? EstadoRendicion { get; set; }
        /// <summary>"Pendiente" | "Aprobado" | "Rechazado" | null para todos.</summary>
        public string? EstadoAprobacion { get; set; }

        /// <summary>UserId del usuario logueado (de claims). Necesario para el scoping de visibilidad.</summary>
        public int? CurrentUserId { get; set; }

        /// <summary>
        /// Visibilidad ya resuelta por el servicio (SalidaVisibilityResolver). Si true, el usuario
        /// ve TODAS las solicitudes sin restricción por área.
        /// </summary>
        public bool SeesAll { get; set; }

        /// <summary>
        /// Nodos area_scope cuyos trabajadores puede ver el usuario. El usuario también ve
        /// siempre las solicitudes donde él es el aprobador resuelto (aprobador_worker_id).
        /// </summary>
        public List<int>? VisibleAreaScopeIds { get; set; }

        /// <summary>
        /// Filtro de área elegido por el usuario en la UI (desplegable en cascada): nodo
        /// seleccionado + sus descendientes, resueltos en el frontend. Null/vacío = sin filtro.
        /// Es independiente de <see cref="VisibleAreaScopeIds"/> (visibilidad obligatoria).
        /// </summary>
        public List<int>? FilterAreaScopeIds { get; set; }

        /// <summary>Página solicitada (1-based). Solo aplica a la vista paginada de la tabla.</summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Columna por la que ordenar la tabla. Null/desconocida = orden original
        /// (pendientes primero, luego más recientes). Valores: trabajador, fechaSalida,
        /// horaSalida, horaRetorno, motivo, lugarOrigen, lugarDestino, estadoAprobacion,
        /// estadoRendicion, createdAt.
        /// </summary>
        public string? SortBy { get; set; }

        /// <summary>Dirección del orden: "asc" o "desc" (por defecto "asc").</summary>
        public string? SortDir { get; set; }
    }

    public class MarcarRendidasBulkDto
    {
        public List<int> Ids { get; set; } = new();
    }

    public class GestionSalidaFilterDataDto
    {
        public List<TrabajadorOptionDto> Trabajadores { get; set; } = new();
        public List<LugarProyectoOptionDto> LugaresProyecto { get; set; } = new();
        /// <summary>Árbol area_scope (lista plana) para el filtro de área en cascada.</summary>
        public List<AreaNodeDto> AreaTree { get; set; } = new();
    }

    /// <summary>Nodo del árbol area_scope (lista plana; el frontend arma la jerarquía). </summary>
    public class AreaNodeDto
    {
        public int AreaScopeId { get; set; }
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public int? AreaScopeParentId { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class TrabajadorOptionDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
    }

    public class LugarProyectoOptionDto
    {
        public int GaLugarId { get; set; }
        public string NombreDisplay { get; set; } = string.Empty;
    }

    public class AprobarRechazarDto { }

    public class GestionSalidaCapturaDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
    }

    public class GestionSalidaTrayectoDto
    {
        public int Id { get; set; }
        public int Orden { get; set; }
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        /// <summary>webUrl del documento adjunto del trayecto (motivos con requiere_adjunto). Null si no tiene.</summary>
        public string? AdjuntoUrl { get; set; }
        public string? AdjuntoFilename { get; set; }
        public List<GestionSalidaCapturaDto> Capturas { get; set; } = new();
        /// <summary>Monto del catálogo ga_trayecto si aplica (worker TI + match origen/destino).</summary>
        public decimal? MontoCatalogo { get; set; }
        /// <summary>Monto efectivo: sum(capturas) si hay; sino MontoCatalogo si aplica; sino 0.</summary>
        public decimal MontoTotal { get; set; }
    }

    public class GestionSalidaDetalleDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public DateOnly FechaSalida { get; set; }
        public string EstadoAprobacion { get; set; } = string.Empty;
        public string EstadoRendicion { get; set; } = "No rendido";
        public DateTimeOffset CreatedAt { get; set; }
        public string? MotivoRechazo { get; set; }
        public GestionSalidaRendicionDto? Rendicion { get; set; }
        public List<GestionSalidaTrayectoDto> Trayectos { get; set; } = new();
    }

    public class GestionSalidaRendicionDto
    {
        public int Id { get; set; }
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        public DateTimeOffset RendidoAt { get; set; }
    }

    /// <summary>Una fila del PDF de planilla — un registro = UN TRAYECTO (no una solicitud).</summary>
    public class RendicionItemDto
    {
        /// <summary>Trayecto ID.</summary>
        public int Id { get; set; }
        /// <summary>Solicitud a la que pertenece este trayecto — para agrupar el TOTAL al final.</summary>
        public int SolicitudId { get; set; }
        public int WorkerId { get; set; }
        public string TrabajadorNombre { get; set; } = string.Empty;
        public string? TrabajadorDni { get; set; }
        /// <summary>person.document_identity_type_id (1 = DNI, 2 = CE). Define la etiqueta del documento en el PDF.</summary>
        public int? TrabajadorDocumentTypeId { get; set; }
        public string? Area { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        /// <summary>Razón social de la empresa a la que está afiliado el trabajador.</summary>
        public string? RazonSocial { get; set; }
        public string? Ruc { get; set; }
        /// <summary>Suma de los montos de las capturas de este trayecto (columna IMPORTE).</summary>
        public decimal Importe { get; set; }
        /// <summary>True si el importe proviene del catálogo ga_trayecto (incluso si vale 0).</summary>
        public bool EsCatalogo { get; set; }
    }
}
