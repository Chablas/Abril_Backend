namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>
    /// Selección sobre la que se quiere saber qué correos saldrían. Repite la forma de los DTO de
    /// las acciones (planillas y/o salidas sueltas) para que la pantalla pueda pedir el preview con
    /// la MISMA selección con la que después va a escribir, y no con una aproximación.
    ///
    /// Lo comparten las tres pantallas del revisor: cada una ignora los campos que no usa (Gestión
    /// de Salidas trabaja con salidas sueltas, Reembolsos con planillas).
    /// </summary>
    public class CorreoPreviewRequestDto
    {
        public List<int> RendicionIds { get; set; } = new();
        public List<int> SolicitudIds { get; set; } = new();

        /// <summary>
        /// Cuál de las decisiones de la pantalla se está por tomar, cuando tiene más de una familia
        /// de correos. Ver <see cref="CorreoPreviewAcciones"/>; las pantallas con una sola decisión
        /// lo ignoran.
        /// </summary>
        public string? Accion { get; set; }

        /// <summary>true = la variante que aprueba; false = la que observa o rechaza.</summary>
        public bool Aprobar { get; set; }
    }

    /// <summary>
    /// Valores de <see cref="CorreoPreviewRequestDto.Accion"/>. Son de la petición, no del
    /// catálogo de correos: dicen de qué paso del flujo se pide el preview, y el servidor traduce
    /// eso al código de evento junto con <see cref="CorreoPreviewRequestDto.Aprobar"/>.
    /// </summary>
    public static class CorreoPreviewAcciones
    {
        /// <summary>La decisión previa al Consolidado del S10 (aprobar / observar).</summary>
        public const string PrimeraRevision = "PRIMERA_REVISION";

        /// <summary>La decisión del reembolso (aprobar y firmar / rechazar).</summary>
        public const string Reembolso = "REEMBOLSO";
    }
}
