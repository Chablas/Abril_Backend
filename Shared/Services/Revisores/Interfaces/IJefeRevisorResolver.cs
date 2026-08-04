namespace Abril_Backend.Shared.Services.Revisores.Interfaces
{
    /// <summary>
    /// Resuelve el jefe/revisor de un trabajador a partir de la configuración global
    /// ("Revisores de Trabajadores" y "Revisores de Áreas" en /configuracion):
    ///   1) El primer revisor vivo (state) y activo (active) del trabajador en
    ///      <c>workers_revisores</c>, por orden_prioridad ascendente, cuyo worker
    ///      tenga correo corporativo @abril.pe.
    ///   2) Los revisores del área del trabajador en <c>area_revisores</c>: se parte
    ///      de su nodo workers.area_scope_id y se sube por el árbol hasta el primer
    ///      nodo con un revisor vivo + activo con correo válido (por prioridad).
    ///   3) Fallback: el área de GTH — nodo <c>area_scope</c> del área
    ///      "Gestión del Talento Humano" con <c>email</c> configurado.
    ///
    /// Servicio compartido: lo usan Gestión Administrativa (a quién se le manda a
    /// aprobar una solicitud de salida) y SSOMA · Salud Ocupacional (a qué jefe se le
    /// notifica un EMO). Antes vivía como <c>ISalidaRevisorResolver</c> dentro de
    /// SolicitudSalidasFeature; al usarlo dos módulos se movió a <c>Shared/</c>.
    /// Reemplaza al algoritmo de jerarquía de áreas (ApproverResolver / JefeResolver),
    /// que queda sin uso pero se conserva en el código.
    /// </summary>
    public interface IJefeRevisorResolver
    {
        Task<JefeRevisorResolution?> ResolveAsync(int workerId);
    }

    /// <summary>
    /// Jefe/revisor resuelto: un trabajador (WorkerId) o un área (AreaScopeId, el
    /// fallback de GTH) — exactamente uno de los dos — y el correo a usar.
    /// </summary>
    public record JefeRevisorResolution(int? WorkerId, int? AreaScopeId, string Email);
}
