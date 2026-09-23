using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    // El detalle de una solicitud (SolicitudSalidaDetalleDto y sus trayectos/capturas) vive en
    // Shared/Dtos/SalidaDetalleDtos.cs: lo muestran también las pantallas que revisan la salida.

    public class TrayectoCreateDto
    {
        /// <summary>Null cuando el motivo elegido tiene pide_horas_lugares = false.</summary>
        public TimeOnly? HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }

        /// <summary>Id de ga_motivo_salida. Nulo cuando el usuario elige "Otro motivo".</summary>
        public int? MotivoId { get; set; }
        /// <summary>Texto libre cuando MotivoId es nulo.</summary>
        public string? MotivoLibre { get; set; }
        /// <summary>Detalle obligatorio cuando el motivo elegido tiene requiere_motivo_adicional.</summary>
        public string? MotivoAdicional { get; set; }

        /// <summary>Id de ga_lugar. Nulo cuando el usuario elige "Otro lugar".</summary>
        public int? LugarOrigenId { get; set; }
        public string? LugarOrigenLibre { get; set; }

        /// <summary>Id de ga_lugar. Nulo cuando el usuario elige "Otro lugar".</summary>
        public int? LugarDestinoId { get; set; }
        public string? LugarDestinoLibre { get; set; }
    }

    public class SolicitudSalidaCreateDto
    {
        public DateOnly FechaSalida { get; set; }
        public List<TrayectoCreateDto> Trayectos { get; set; } = new();
    }

    /// <summary>
    /// Resultado de la subida a SharePoint del documento adjunto de un trayecto
    /// (interno del backend: lo arma el service tras subir el archivo, no viene del cliente).
    /// </summary>
    public class TrayectoAdjuntoSubidoDto
    {
        public string Url { get; set; } = string.Empty;
        public string? ItemId { get; set; }
        public string DriveId { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
    }

    public class SolicitudSalidaFiltersDto
    {
        public int? LugarProyectoId { get; set; }
        /// <summary>"Pendiente" | "Aprobado" | "Rechazado" | null para todos.</summary>
        public string? EstadoAprobacion { get; set; }
        /// <summary>"Rendido" | "No rendido" | null para todos.</summary>
        public string? EstadoRendicion { get; set; }

        /// <summary>
        /// "Pendiente" | "Aprobado" | "Rechazado" | "Firmado" | "Pagado" | null para todos. No lo
        /// usa la tabla (el trabajador no filtra por esto): lo usa el conteo de "Observadas" de las
        /// tarjetas, para no traerse todo el histórico rendido solo para contar los rechazados.
        /// </summary>
        public string? EstadoReembolso { get; set; }

        /// <summary>Límite inferior (inclusive) de fecha_salida. Lo usa la rendición del mes anterior.</summary>
        public DateOnly? FechaSalidaDesde { get; set; }
        /// <summary>Límite superior (inclusive) de fecha_salida. Lo usa la rendición del mes anterior.</summary>
        public DateOnly? FechaSalidaHasta { get; set; }

        /// <summary>
        /// Periodo elegido en el desplegable "Mes a rendir". Cuando viene, la lista se acota a ese
        /// mes y deja SOLO las solicitudes aptas para rendir: es un filtro de la tabla.
        /// </summary>
        public int? RendicionAnio { get; set; }
        public int? RendicionMes { get; set; }

        /// <summary>
        /// True = devolver únicamente las solicitudes aptas para rendir. Lo prende el filtro de mes;
        /// se aplica en memoria porque la aptitud se calcula en memoria.
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
        /// <summary>Cuántas solicitudes propias aptas para rendir tiene ese mes.</summary>
        public int Cantidad { get; set; }
        /// <summary>
        /// Último día para rendir ese mes (N.º día hábil del mes siguiente, N configurable). Solo se ofrecen meses
        /// cuyo plazo sigue abierto, así que esta fecha siempre es de hoy en adelante.
        /// </summary>
        public DateOnly FechaLimite { get; set; }
    }

    /// <summary>
    /// Números de las tarjetas del encabezado. Se cuentan sobre EL MISMO conjunto que muestra la
    /// tabla (los filtros ya aplicados), así que acompañan a la búsqueda en vez de quedarse en un
    /// total fijo. Por eso viajan en la respuesta del listado y no en <c>filter-data</c>.
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
        /// Cuenta las tres bandejas sobre las solicitudes recibidas. Cada tarjeta conserva su
        /// definición (es lo que dice su etiqueta); lo que cambia con los filtros es el universo
        /// sobre el que se cuenta. Se calcula en memoria sobre la lista que el repositorio ya
        /// devolvió: no cuesta una consulta más.
        /// </summary>
        public static ResumenRendicionDto De(IEnumerable<SolicitudSalidaListItemDto> solicitudes)
        {
            var lista = solicitudes as ICollection<SolicitudSalidaListItemDto> ?? solicitudes.ToList();
            return new ResumenRendicionDto
            {
                AptasParaRendir     = lista.Count(x => x.AptaParaRendir),
                // AptaParaRendir ya exige aprobada + no rendida; acá se piden explícitas porque
                // esta tarjeta cuenta justo a las que NO llegan a aptas por falta de captura.
                CapturasIncompletas = lista.Count(x => !x.PuedeRendirse
                                                    && x.EstadoAprobacion == EstadosSalida.Aprobacion.NombreAprobado
                                                    && x.EstadoRendicion  == EstadosSalida.Rendicion.NombreNoRendido),
                Observadas          = lista.Count(x => x.EstadoReembolso == EstadosSalida.Reembolso.NombreObservado),
            };
        }
    }

    public class LugarProyectoOptionDto
    {
        public int Id { get; set; }
        public string NombreDisplay { get; set; } = string.Empty;
    }

    public class SolicitudSalidaFilterDataDto
    {
        public List<LugarProyectoOptionDto> LugaresProyecto { get; set; } = new();

        /// <summary>Meses ofrecidos por el desplegable "Mes a rendir" (los que tienen algo apto).</summary>
        public List<MesRendicionDto> MesesRendicion { get; set; } = new();

        /// <summary>
        /// A quién le llegan los correos de «Rendir», que envía la planilla a primera revisión: el
        /// aviso a la jefatura y el acuse al trabajador (Mis Rendiciones → Configuración → Correos).
        /// Van con los datos de arranque porque son los mismos para toda la pantalla —está acotada
        /// a un solo trabajador— y la confirmación no tiene que pedirlos. Lista vacía = hoy no sale
        /// ninguno.
        /// </summary>
        public List<CorreoAvisoPreviewDto> CorreosRendir { get; set; } = new();
    }

    /// <summary>
    /// Respuesta del listado: las filas y los números de las tarjetas, contados sobre ese mismo
    /// conjunto filtrado. Van juntos para que un cambio de filtro se resuelva en una sola petición
    /// y la tabla y las tarjetas no puedan discrepar.
    /// </summary>
    public class SolicitudSalidaListResultDto
    {
        public List<SolicitudSalidaListItemDto> Data { get; set; } = new();
        public ResumenRendicionDto Resumen { get; set; } = new();
    }
}
