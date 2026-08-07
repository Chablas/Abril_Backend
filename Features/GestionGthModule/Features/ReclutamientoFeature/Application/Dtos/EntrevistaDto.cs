namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>Body del PATCH que marca/desmarca el check informativo del Multitest de un candidato.</summary>
    public class MultitestUpdateDto
    {
        /// <summary>true = el candidato ya rindió el Multitest.</summary>
        public bool Realizado { get; set; }
    }

    /// <summary>
    /// Body del POST que programa (o reprograma) la entrevista de un candidato. El correo del
    /// postulante no viaja en el body: lo resuelve el backend desde el formulario aprobado del
    /// candidato, para que GTH no pueda citar a una dirección distinta de la registrada.
    /// </summary>
    public class EntrevistaGuardarDto
    {
        public DateOnly Fecha { get; set; }

        /// <summary>Hora de la cita en formato "HH:mm" (24h), igual que el <c>app-time-picker</c>.</summary>
        public string Hora { get; set; } = string.Empty;

        /// <summary>Id de <c>gth_lugar_entrevista</c>.</summary>
        public int LugarId { get; set; }
    }

    /// <summary>Entrevista programada de un candidato, como la muestra la vista de GTH.</summary>
    public class EntrevistaResumenDto
    {
        public DateOnly Fecha { get; set; }

        /// <summary>Hora en formato "HH:mm" (24h).</summary>
        public string Hora { get; set; } = string.Empty;

        public int LugarId { get; set; }
        public string LugarNombre { get; set; } = string.Empty;

        /// <summary>Correo al que se envió la invitación.</summary>
        public string CorreoEnvio { get; set; } = string.Empty;

        /// <summary>Momento del último envío de la invitación (hora de Perú). Null si aún no se envió.</summary>
        public DateTime? EnviadoEn { get; set; }
    }

    /// <summary>Resultado de programar/reprogramar una entrevista: mensaje + estado resultante para refrescar la fila.</summary>
    public class EntrevistaAccionResultDto
    {
        public string Message { get; set; } = string.Empty;
        public EntrevistaResumenDto Entrevista { get; set; } = new();
    }

    /// <summary>Datos que necesita el servicio para armar el correo de invitación a la entrevista.</summary>
    public class EntrevistaEnvioContextoDto
    {
        public string CandidatoNombre { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public EntrevistaResumenDto Resumen { get; set; } = new();
    }
}
