namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Resultado de anular una vacante desde «Solicitud de Personal». Sirve para armar el mensaje:
    /// se anula SIEMPRE una vacante, pero cuando es la última que le quedaba viva a su solicitud,
    /// la solicitud entera se va con ella y conviene decirlo.
    /// </summary>
    public class AnularVacanteResultDto
    {
        /// <summary>Código de la vacante anulada (REQ-AAAA-NNNN), para nombrarla en el aviso.</summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>
        /// true si la vacante era la única viva de su solicitud, así que también se dieron de baja
        /// la solicitud y su aprobación. false = la solicitud sigue viva con sus otras vacantes.
        /// </summary>
        public bool SolicitudDadaDeBaja { get; set; }

        /// <summary>Cuántas vacantes le quedan vivas a la solicitud después de anular.</summary>
        public int VacantesRestantes { get; set; }
    }
}
