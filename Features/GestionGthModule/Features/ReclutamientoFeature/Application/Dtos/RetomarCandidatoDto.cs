namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Resultado de retomar el proceso con un candidato del historial de rechazados: a qué fase
    /// volvió el requerimiento y desde qué etapa se lo retomó.
    ///
    /// Es la salida del camino que abre un EMO de ingreso No Apto: el requerimiento queda en
    /// EMO_NO_APTO y GTH elige a quién de los que ya se descartaron vuelve a poner en carrera. La
    /// fase de destino no la elige GTH — la decide la etapa en la que se rechazó al candidato,
    /// porque retomarlo significa continuar justo desde ahí y no volver a empezar el proceso.
    /// </summary>
    public class RetomarCandidatoResultDto
    {
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>Nombre del candidato retomado (para el aviso de la pantalla).</summary>
        public string CandidatoNombre { get; set; } = string.Empty;

        /// <summary>
        /// Etapa en la que se lo había rechazado y desde la que se retoma:
        /// <c>LONG_LIST</c>, <c>FORMULARIO</c>, <c>ENTREVISTAS</c> o <c>DECISION_FINAL</c>.
        /// </summary>
        public string EtapaCodigo { get; set; } = string.Empty;

        /// <summary>Nombre de esa etapa ("Long list", "Formulario", "Entrevistas", "Decisión final").</summary>
        public string EtapaNombre { get; set; } = string.Empty;

        /// <summary>Qué le toca hacer a GTH ahora, para el mensaje de confirmación de la pantalla.</summary>
        public string SiguientePaso { get; set; } = string.Empty;
    }
}
