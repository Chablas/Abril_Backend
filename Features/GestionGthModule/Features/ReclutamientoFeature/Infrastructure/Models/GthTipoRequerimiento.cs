namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Catálogo del tipo de requerimiento de una vacante (tabla <c>gth_tipo_requerimiento</c>).
    /// Valores iniciales: <c>Nuevo</c> y <c>Reemplazo</c>. Normalizado a tabla en vez de texto
    /// plano en el requerimiento (regla de normalización del proyecto).
    /// </summary>
    public class GthTipoRequerimiento
    {
        public int GthTipoRequerimientoId { get; set; }
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
