using Abril_Backend.Features.ConvivirModule.Shared.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Dtos
{
    /// <summary>La campana de la app (RF-05): los avisos de todas sus propiedades, en una llamada.</summary>
    public class ConvivirNotificacionesDto
    {
        /// <summary>Los más recientes primero, hasta un tope (los viejos no se muestran).</summary>
        public List<ConvivirNotificacionDto> Notificaciones { get; set; } = new();
        /// <summary>Todos los no leídos, aunque alguno quede fuera de la lista.</summary>
        public int NoLeidas { get; set; }
        /// <summary>Tiene más de una propiedad: cada aviso muestra de cuál es.</summary>
        public bool VariasPropiedades { get; set; }
    }

    public class ConvivirNotificacionDto
    {
        public int NotificacionId { get; set; }
        /// <summary>Ver <see cref="ConvivirNotificacionTipo"/>: con él la app elige el ícono y a dónde lleva.</summary>
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;
        /// <summary>Cuando apareció el contenido, en hora de Perú y con su desfase (-05:00).</summary>
        public DateTimeOffset Fecha { get; set; }
        public bool Leida { get; set; }
        /// <summary>La propiedad que se elige al abrirlo. En un hito, la primera suya en ese proyecto.</summary>
        public int PropietarioId { get; set; }
        public string Proyecto { get; set; } = string.Empty;
        /// <summary>Solo en los documentos: el hito es de todo el proyecto.</summary>
        public string? Torre { get; set; }
        /// <summary>Solo en los documentos.</summary>
        public string? Departamento { get; set; }
    }

    /// <summary>Fila cruda de la lista; el servicio arma el título y el detalle.</summary>
    public class ConvivirNotificacionFila
    {
        public int NotificacionId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        /// <summary>Hora de Perú.</summary>
        public DateTime Fecha { get; set; }
        public bool Leida { get; set; }
        public int PropietarioId { get; set; }
        public string Proyecto { get; set; } = string.Empty;
        public string? Torre { get; set; }
        public string? Departamento { get; set; }
        /// <summary>HITO: la descripción del hito. DOCUMENTO: el nombre del documento.</summary>
        public string Contenido { get; set; } = string.Empty;
        /// <summary>Solo DOCUMENTO: su tipo («Minuta», «Contrato»...).</summary>
        public string? ContenidoTipo { get; set; }
    }

    /// <summary>Lo que devuelve la base para la campana, en un solo viaje.</summary>
    public class ConvivirNotificacionesFilas
    {
        public List<ConvivirNotificacionFila> Notificaciones { get; set; } = new();
        public int NoLeidas { get; set; }
        public bool VariasPropiedades { get; set; }
    }
}
