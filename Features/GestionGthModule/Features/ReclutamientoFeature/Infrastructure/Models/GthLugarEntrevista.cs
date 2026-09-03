namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Catálogo del lugar donde se cita al candidato para la entrevista
    /// (tabla <c>gth_lugar_entrevista</c>). Hoy solo está la oficina principal
    /// ("Calle Mama Ocllo 2647, Lince"), que es el valor por defecto del desplegable; GTH
    /// puede dar de alta salas de venta u obras sin tocar código.
    /// </summary>
    public class GthLugarEntrevista
    {
        public int GthLugarEntrevistaId { get; set; }

        /// <summary>Dirección o nombre del lugar, tal como se muestra en el correo de invitación.</summary>
        public string Nombre { get; set; } = null!;

        /// <summary>
        /// Enlace al mapa del lugar (Google Maps). El correo de invitación lo muestra debajo del
        /// nombre para que el postulante sepa dónde encontrarnos. Es un dato DEL lugar y no una
        /// constante del backend: una sala de venta o una obra tienen su propio mapa. Null en los
        /// lugares que aún no lo tienen cargado — ahí el correo sale solo con el nombre.
        /// </summary>
        public string? MapsUrl { get; set; }

        /// <summary>
        /// Referencia para ubicar el lugar ("A la altura de la cuadra 11 de la avenida 2 de Mayo").
        /// El correo de invitación y la página de confirmación del postulante la muestran bajo la
        /// dirección. Es un dato DEL lugar por lo mismo que <see cref="MapsUrl"/>: una sala de venta
        /// o una obra tienen su propia referencia. Null en los lugares que no la tienen cargada — ahí
        /// no se muestra esa línea.
        /// </summary>
        public string? Referencia { get; set; }

        /// <summary>Orden en el desplegable (el primero es el que se preselecciona).</summary>
        public int Orden { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
