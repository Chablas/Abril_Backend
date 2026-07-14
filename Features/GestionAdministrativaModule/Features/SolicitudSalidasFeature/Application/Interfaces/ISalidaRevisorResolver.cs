namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces
{
    /// <summary>
    /// Resuelve a quién se le envía una solicitud de salida para su aprobación:
    ///   1) El primer revisor vivo (state) y activo (active) del solicitante en
    ///      <c>workers_revisores</c>, por orden_prioridad ascendente, cuyo worker
    ///      tenga correo corporativo @abril.pe.
    ///   2) Los revisores del área del solicitante en <c>area_revisores</c>: se parte
    ///      de su nodo workers.area_scope_id y se sube por el árbol hasta el primer
    ///      nodo con un revisor vivo + activo con correo válido (por prioridad).
    ///   3) Fallback: el área de GTH — nodo <c>area_scope</c> del área
    ///      "Gestión del Talento Humano" con <c>email</c> configurado.
    /// Reemplaza al algoritmo de jerarquía de áreas (ApproverResolver / JefeResolver),
    /// que queda sin uso pero se conserva en el código.
    /// </summary>
    public interface ISalidaRevisorResolver
    {
        Task<SalidaRevisorResolution?> ResolveAsync(int solicitanteWorkerId);
    }

    /// <summary>
    /// Destino resuelto del correo de aprobación: un trabajador (WorkerId) o un área
    /// (AreaScopeId, fallback GTH) — exactamente uno de los dos — y el correo a usar.
    /// </summary>
    public record SalidaRevisorResolution(int? WorkerId, int? AreaScopeId, string Email);
}
