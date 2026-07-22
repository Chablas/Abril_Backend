namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Catálogo de canales donde se publica una vacante (tabla <c>gth_canal_publicacion</c>).
    /// Valores iniciales: Bumeran (API disponible · publicación automática), LinkedIn y
    /// Computrabajo (API no disponible · registro de publicación manual).
    /// </summary>
    public class GthCanalPublicacion
    {
        public int GthCanalPublicacionId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;

        /// <summary>true = el canal tiene API y la publicación es automática; false = registro manual.</summary>
        public bool ApiDisponible { get; set; }

        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
