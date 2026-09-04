using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos
{
    public class GestionSalidaListItemDto
    {
        public int Id { get; set; }
        /// <summary>Código SOL-AAAA-NNNN. Null solo en solicitudes anteriores a la columna.</summary>
        public string? Codigo { get; set; }
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        /// <summary>
        /// Área del trabajador: el nombre del nodo al que apunta <c>puesto.area_destino_scope_id</c>, es
        /// decir el área más baja a la que pertenece (una persona puede colgar de Gerencia de
        /// Proyectos › Unidad de Proyectos › Unidad de Proyectos y aquí va solo la última). El
        /// detalle sí devuelve la ruta completa en <see cref="GestionSalidaDetalleDto.AreaRuta"/>.
        /// </summary>
        public string? Area { get; set; }
        /// <summary>
        /// Nombre completo del jefe/revisor del solicitante — el mismo que recibe el correo de
        /// aprobación al crear la solicitud (<c>IJefeRevisorResolver</c>). Cuando la resolución
        /// cae al fallback de GTH no hay persona: ahí va el nombre del área.
        /// </summary>
        public string? RevisorNombre { get; set; }
        public DateOnly FechaSalida { get; set; }
        /// <summary>Hora de salida del primer trayecto. Null si el motivo no pide horario.</summary>
        public TimeOnly? HoraSalida { get; set; }
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

        /// <summary>
        /// True si al menos un trayecto lleva un motivo marcado como reembolsable en
        /// Configuración → Motivos (<c>ga_motivo_salida.es_reembolsable</c>). Sin eso la salida no
        /// genera gasto de movilidad y no hay nada que rendir. Un trayecto con motivo libre (sin
        /// catálogo) no concede: el flag arranca en false a propósito.
        /// </summary>
        public bool EsReembolsable { get; set; }

        /// <summary>
        /// Último día para rendir esta salida: el 7.º día hábil del mes siguiente al de su
        /// <c>fecha_salida</c> (sin sábados, domingos ni los feriados de Configuración → Feriados).
        /// </summary>
        public DateOnly PlazoRendicionHasta { get; set; }

        /// <summary>
        /// True si el plazo ya pasó. La salida deja de poder rendirse —no se selecciona, no entra
        /// en el desplegable de mes y el backend la rechaza— pero su detalle se sigue viendo.
        /// </summary>
        public bool PlazoVencido { get; set; }

        /// <summary>
        /// True si la salida está lista para rendirse: aprobada, no rendida, con todos sus
        /// trayectos cubiertos (<see cref="PuedeRendirse"/>), con motivo reembolsable
        /// (<see cref="EsReembolsable"/>) y dentro del plazo (<see cref="PlazoVencido"/>). Es la
        /// condición que usan el desplegable "Mes a rendir", la selección de filas y el conteo de
        /// las tarjetas — se calcula acá y no en la pantalla para que las tres no puedan divergir.
        /// </summary>
        public bool AptaParaRendir { get; set; }
        /// <summary>Hora real de salida registrada por recepción. Dato extra, opcional.</summary>
        public TimeOnly? HoraSalidaReal { get; set; }
        /// <summary>Hora real de retorno registrada por recepción. Dato extra, opcional.</summary>
        public TimeOnly? HoraRetornoReal { get; set; }
        /// <summary>
        /// True cuando TODOS los trayectos tienen motivos con es_hora_estimada: las horas declaradas
        /// son estimadas y recepción no registra hora real de salida/retorno. Si al menos un trayecto
        /// tiene motivo de hora exacta (o motivo libre), se sigue registrando.
        /// </summary>
        public bool EsHoraEstimada { get; set; }
        /// <summary>
        /// True si el usuario logueado puede aprobar/rechazar ESTA salida. Es false cuando la salida
        /// es propia (worker del propio usuario) y el usuario no es Gerente — nadie aprueba sus
        /// propias salidas salvo los gerentes. Solo afecta Aprobar/Rechazar, no la rendición.
        /// </summary>
        public bool PuedeDecidir { get; set; } = true;

        /// <summary>
        /// True si la salida es del propio usuario logueado (su worker). Habilita el botón
        /// "Cancelar": un trabajador solo puede cancelar SUS propias solicitudes Pendientes.
        /// </summary>
        public bool EsPropia { get; set; }

        // -- Reembolso (solo informativo) --------------------------------
        // El archivo del Consolidado del S10 y la planilla firmada no viajan en esta lista: se
        // abren desde Gestion de Rendiciones, que es donde se usan. Aca solo se pinta el estado.
        /// <summary>"Pendiente" | "Aprobado" | "Rechazado" | "Firmado" | "Pagado".</summary>
        public string EstadoReembolso { get; set; } = "Pendiente";

        /// <summary>
        /// True cuando el reembolso ya se puede decidir: la salida esta Rendida Y tiene adjunto el
        /// Consolidado del S10. Sin eso no hay que revisar.
        /// </summary>
        public bool ReembolsoRevisable { get; set; }

        /// <summary>Observacion del ultimo rechazo del reembolso. Es lo que el trabajador subsana.</summary>
        public string? ObservacionReembolso { get; set; }

        /// <summary>Nombre de quien aprobo/rechazo el reembolso. Null si nadie lo decidio aun.</summary>
        public string? ReembolsoDecididoPor { get; set; }
        public DateTimeOffset? ReembolsoDecididoAt { get; set; }
    }

    public class RegistrarHoraSalidaRealDto
    {
        /// <summary>"HH:mm" o null para limpiar.</summary>
        public TimeOnly? HoraSalidaReal { get; set; }
    }

    public class RegistrarHoraRetornoRealDto
    {
        /// <summary>"HH:mm" o null para limpiar.</summary>
        public TimeOnly? HoraRetornoReal { get; set; }
    }

    public class GestionSalidaFiltersDto
    {
        public int? WorkerId { get; set; }
        public int? LugarProyectoId { get; set; }
        public string? EstadoRendicion { get; set; }
        /// <summary>"Pendiente" | "Aprobado" | "Rechazado" | null para todos.</summary>
        public string? EstadoAprobacion { get; set; }

        /// <summary>
        /// "Pendiente" | "Aprobado" | "Rechazado" | "Firmado" | "Pagado" | null para todos. Es un
        /// filtro informativo: el reembolso ya no se decide en esta pantalla (vive en Gestion de
        /// Rendiciones), pero su estado se sigue mostrando en la columna.
        /// </summary>
        public string? EstadoReembolso { get; set; }

        /// <summary>
        /// True = solo las solicitudes cuya <c>fecha_salida</c> es la de HOY. El día se calcula en
        /// hora de Perú (UTC-5) y no en la del servidor, que corre en UTC. Es el filtro que la
        /// pantalla de Gestión de Salidas manda encendido por defecto.
        /// </summary>
        public bool SoloHoy { get; set; }

        /// <summary>UserId del usuario logueado (de claims). Necesario para el scoping de visibilidad.</summary>
        public int? CurrentUserId { get; set; }

        /// <summary>
        /// Visibilidad ya resuelta por el servicio (SalidaVisibilityResolver). Si true, el usuario
        /// ve TODAS las solicitudes sin restricción por área.
        /// </summary>
        public bool SeesAll { get; set; }

        /// <summary>
        /// True cuando el usuario autenticado tiene el rol USUARIO DE RECEPCIÓN (lo setea el
        /// controller desde los claims). El rol se sobrepone al alcance por área: fuerza
        /// <see cref="SeesAll"/> sin correr el resolver de visibilidad.
        /// </summary>
        public bool SeesAllOverride { get; set; }

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

        /// <summary>Límite inferior (inclusive) de fecha_salida. Lo usa la rendición del mes anterior.</summary>
        public DateOnly? FechaSalidaDesde { get; set; }
        /// <summary>Límite superior (inclusive) de fecha_salida. Lo usa la rendición del mes anterior.</summary>
        public DateOnly? FechaSalidaHasta { get; set; }

        /// <summary>
        /// Periodo elegido en el desplegable "Mes a rendir". Cuando viene, el repositorio acota
        /// <c>fecha_salida</c> a ese mes y deja SOLO las solicitudes aptas para rendir — es un
        /// filtro de la tabla, no solo el alcance de la acción de rendir.
        ///
        /// Es excluyente con <see cref="SoloHoy"/>: el frontend apaga uno al prender el otro y acá
        /// el mes gana si por lo que sea llegan los dos.
        /// </summary>
        public int? RendicionAnio { get; set; }
        public int? RendicionMes { get; set; }

        /// <summary>
        /// True = devolver únicamente las solicitudes aptas para rendir (aprobadas, no rendidas,
        /// con trayectos cubiertos y motivo reembolsable). Lo prende el propio filtro de mes; se
        /// aplica en memoria porque la aptitud se calcula en memoria.
        /// </summary>
        public bool SoloAptas { get; set; }
    }

    /// <summary>Un mes ofrecido por el desplegable "Mes a rendir".</summary>
    public class MesRendicionDto
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        /// <summary>"Agosto 2026" — ya capitalizado, la pantalla lo imprime tal cual.</summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>Cuántas solicitudes aptas para rendir tiene ese mes dentro del alcance del usuario.</summary>
        public int Cantidad { get; set; }
        /// <summary>
        /// Último día para rendir ese mes (7.º día hábil del mes siguiente). Solo se ofrecen meses
        /// cuyo plazo sigue abierto, así que esta fecha siempre es de hoy en adelante.
        /// </summary>
        public DateOnly FechaLimite { get; set; }
    }

    /// <summary>
    /// Los números de las tarjetas del encabezado. Se cuentan sobre EL MISMO conjunto filtrado que
    /// alimenta la tabla —todas las páginas, no solo la visible—, así que acompañan a la búsqueda
    /// en vez de quedarse en un total fijo. Por eso viajan en la respuesta del listado y no en
    /// <c>filter-data</c>.
    /// </summary>
    public class ResumenRendicionDto
    {
        /// <summary>Aprobadas, no rendidas, con trayectos cubiertos y motivo reembolsable.</summary>
        public int AptasParaRendir { get; set; }
        /// <summary>Aprobadas y no rendidas a las que les falta captura en algún trayecto.</summary>
        public int CapturasIncompletas { get; set; }
        /// <summary>Reembolsos rechazados: esperan que el trabajador subsane.</summary>
        public int Observadas { get; set; }

        /// <summary>
        /// Cuenta las tres bandejas sobre las salidas recibidas. Cada tarjeta conserva su
        /// definición (es lo que dice su etiqueta); lo que cambia con los filtros es el universo
        /// sobre el que se cuenta. Se calcula en memoria sobre la lista completa que el
        /// repositorio ya tenía en la mano para paginar: no cuesta una consulta más.
        /// </summary>
        public static ResumenRendicionDto De(IEnumerable<GestionSalidaListItemDto> salidas)
        {
            var lista = salidas as ICollection<GestionSalidaListItemDto> ?? salidas.ToList();
            return new ResumenRendicionDto
            {
                AptasParaRendir     = lista.Count(x => x.AptaParaRendir),
                // AptaParaRendir ya exige aprobada + no rendida; acá se piden explícitas porque
                // esta tarjeta cuenta justo a las que NO llegan a aptas por falta de captura.
                CapturasIncompletas = lista.Count(x => !x.PuedeRendirse
                                                    && x.EstadoAprobacion == EstadosSalida.Aprobacion.NombreAprobado
                                                    && x.EstadoRendicion  == EstadosSalida.Rendicion.NombreNoRendido),
                Observadas          = lista.Count(x => x.EstadoReembolso == EstadosSalida.Reembolso.NombreRechazado),
            };
        }
    }

    /// <summary>
    /// Respuesta del listado paginado más los números de las tarjetas, contados sobre todo el
    /// conjunto filtrado (no sobre la página). Van juntos para que un cambio de filtro se resuelva
    /// en una sola petición y la tabla y las tarjetas no puedan discrepar.
    /// </summary>
    public class GestionSalidaPagedDto : PagedResult<GestionSalidaListItemDto>
    {
        public ResumenRendicionDto Resumen { get; set; } = new();
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

        /// <summary>Meses ofrecidos por el desplegable "Mes a rendir" (los que tienen algo apto).</summary>
        public List<MesRendicionDto> MesesRendicion { get; set; } = new();
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

    /// <summary>Un documento adjunto (prueba) de un trayecto, para mostrar en el detalle.</summary>
    public class GestionSalidaAdjuntoDto
    {
        public string Url { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
    }

    public class GestionSalidaTrayectoDto
    {
        public int Id { get; set; }
        public int Orden { get; set; }
        /// <summary>Null en trayectos de motivos que no piden horario.</summary>
        public TimeOnly? HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public string Motivo { get; set; } = string.Empty;
        /// <summary>Detalle que escribió el trabajador cuando el motivo lo exige. Null si no aplica.</summary>
        public string? MotivoAdicional { get; set; }
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        /// <summary>Documentos adjuntos del trayecto (motivos con requiere_adjunto). Vacío si no tiene.</summary>
        public List<GestionSalidaAdjuntoDto> Adjuntos { get; set; } = new();
        public List<GestionSalidaCapturaDto> Capturas { get; set; } = new();
        /// <summary>Monto del catálogo ga_trayecto si aplica (worker TI + match origen/destino).</summary>
        public decimal? MontoCatalogo { get; set; }
        /// <summary>Monto efectivo: sum(capturas) si hay; sino MontoCatalogo si aplica; sino 0.</summary>
        public decimal MontoTotal { get; set; }
    }

    public class GestionSalidaDetalleDto
    {
        public int Id { get; set; }
        /// <summary>Código SOL-AAAA-NNNN. Null solo en solicitudes anteriores a la columna.</summary>
        public string? Codigo { get; set; }
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        /// <summary>Área más baja del trabajador (el nodo de <c>puesto.area_destino_scope_id</c>).</summary>
        public string? Area { get; set; }
        /// <summary>
        /// Ruta completa del área en el árbol <c>area_scope</c>, de la raíz al nodo del trabajador
        /// (ej. ["Gerencia de Proyectos", "Unidad de Proyectos", "Unidad de Proyectos"]). Vacía si
        /// el trabajador no tiene área asignada.
        /// </summary>
        public List<string> AreaRuta { get; set; } = new();
        /// <summary>Nombre completo del jefe/revisor del solicitante (o el área en el fallback GTH).</summary>
        public string? RevisorNombre { get; set; }
        /// <summary>Correo al que se le notificó la solicitud: el email corporativo del revisor.</summary>
        public string? RevisorEmail { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string EstadoAprobacion { get; set; } = string.Empty;
        public string EstadoRendicion { get; set; } = "No rendido";
        public DateTimeOffset CreatedAt { get; set; }
        public string? MotivoRechazo { get; set; }

        // -- Reembolso ---------------------------------------------------
        /// <summary>"Pendiente" | "Aprobado" | "Rechazado" | "Firmado" | "Pagado".</summary>
        public string EstadoReembolso { get; set; } = "Pendiente";
        /// <summary>Observacion del ultimo rechazo del reembolso.</summary>
        public string? ObservacionReembolso { get; set; }
        /// <summary>Nombre de quien aprobo/rechazo el reembolso.</summary>
        public string? ReembolsoDecididoPor { get; set; }
        public DateTimeOffset? ReembolsoDecididoAt { get; set; }
        /// <summary>Nombre de quien firmo la planilla de esta salida.</summary>
        public string? FirmadoPor { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
        /// <summary>Nombre del tesorero que marco el reembolso como pagado.</summary>
        public string? PagadoPor { get; set; }
        public DateTimeOffset? PagadoAt { get; set; }

        public GestionSalidaRendicionDto? Rendicion { get; set; }
        /// <summary>PDF Consolidado del S10 vigente (propio de la salida o heredado de su planilla). Null si no hay.</summary>
        public Abril_Backend.Features.GestionAdministrativa.Shared.Dtos.ConsolidadoS10Dto? ConsolidadoS10 { get; set; }
        public List<GestionSalidaTrayectoDto> Trayectos { get; set; } = new();
    }

    public class GestionSalidaRendicionDto
    {
        public int Id { get; set; }
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        public DateTimeOffset RendidoAt { get; set; }
        /// <summary>webUrl de la copia FIRMADA por la jefatura. Null mientras nadie la firme.</summary>
        public string? PdfFirmadoUrl { get; set; }
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
        /// <summary>Detalle del motivo (motivos con requiere_motivo_adicional). Se imprime pegado
        /// al motivo en la columna MOTIVO de la planilla. Null si no aplica.</summary>
        public string? MotivoAdicional { get; set; }
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
