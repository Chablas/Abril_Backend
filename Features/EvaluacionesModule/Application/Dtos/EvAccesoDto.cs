namespace Abril_Backend.Features.Evaluaciones.Application.Dtos
{
    /// <summary>
    /// Acceso real del usuario a los distintos flujos de evaluaciones, resuelto por
    /// PUESTO (igual criterio que usan los controllers para autorizar cada endpoint),
    /// no por featureKey/rol de sistema. El frontend lo consulta una vez al entrar a
    /// Evaluaciones y lo usa para no mostrar pestañas a las que igual le negaría el
    /// acceso el backend (ver EvJefeSsomaController, EvSupervisorContratistaController,
    /// EvGestionSsomaController, EvPrevencionistaController).
    /// </summary>
    public class EvAccesoDto
    {
        public bool EsJefeSsoma { get; set; }
        public bool EsCoordinadorSsoma { get; set; }
        public bool EsPrevencionista { get; set; }

        /// <summary>Coordinador SSOMA o Prevencionista — el "equipo SSOMA" que evalúa al Jefe SSOMA.</summary>
        public bool EsEquipoSsoma => EsCoordinadorSsoma || EsPrevencionista;
    }
}
