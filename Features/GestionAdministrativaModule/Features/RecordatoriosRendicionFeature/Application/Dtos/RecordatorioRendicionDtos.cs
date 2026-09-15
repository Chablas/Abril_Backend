namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Dtos
{
    /// <summary>
    /// Cuál de los dos recordatorios corresponde HOY. Lo resuelve el calendario, no el cron: el
    /// cron llama todos los días y acá se decide si hay algo que mandar.
    /// </summary>
    public enum RecordatorioRendicionMomento
    {
        /// <summary>Hoy no es ni el primer día hábil ni el último día del plazo.</summary>
        Ninguno = 0,

        /// <summary>Primer día hábil del mes: se abrió el plazo para rendir el mes anterior (RG-33).</summary>
        Apertura = 1,

        /// <summary>Último día apto para rendir: hoy vence el plazo del mes anterior (RG-34).</summary>
        Cierre = 2,
    }

    /// <summary>
    /// La ventana de rendición vista desde hoy: qué mes se está rindiendo, hasta cuándo y si hoy
    /// toca alguno de los dos recordatorios. Se calcula antes de tocar las salidas — la mayoría de
    /// los días no toca ninguno y no hace falta buscar pendientes.
    /// </summary>
    public class RecordatorioVentanaDto
    {
        /// <summary>Hoy en hora de Perú (el servidor corre en UTC).</summary>
        public DateOnly Hoy { get; set; }

        /// <summary>Mes que se está rindiendo (el anterior al actual).</summary>
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        /// <summary>Primer y último día del periodo, para filtrar <c>fecha_salida</c>.</summary>
        public DateOnly PeriodoDesde { get; set; }
        public DateOnly PeriodoHasta { get; set; }

        /// <summary>Primer día hábil del mes actual: el día en que se abre el plazo.</summary>
        public DateOnly PrimerDiaHabil { get; set; }

        /// <summary>
        /// Último día apto para rendir el periodo: el N.º día hábil del mes actual, con N =
        /// <see cref="DiasHabilesPlazo"/>.
        /// </summary>
        public DateOnly LimiteRendicion { get; set; }

        /// <summary>Días hábiles de plazo configurados (Días reembolsables).</summary>
        public int DiasHabilesPlazo { get; set; }

        /// <summary>Cuántos días hábiles quedan desde hoy hasta el límite, contando hoy.</summary>
        public int DiasHabilesRestantes { get; set; }

        public RecordatorioRendicionMomento Momento { get; set; }
    }

    /// <summary>Una salida pendiente de rendir, tal como se imprime en la tabla del recordatorio.</summary>
    public class RecordatorioSalidaDto
    {
        public int SolicitudId { get; set; }

        /// <summary>Código SOL-AAAA-NNNN (o "#N" en las anteriores al código).</summary>
        public string Codigo { get; set; } = string.Empty;

        public DateOnly FechaSalida { get; set; }

        /// <summary>Motivo del primer trayecto; con varios se resume como "Motivo (+N)".</summary>
        public string Motivo { get; set; } = string.Empty;

        /// <summary>Origen del primer trayecto. Vacío si el motivo no pide lugares.</summary>
        public string Origen { get; set; } = string.Empty;

        /// <summary>Destino del ÚLTIMO trayecto: es donde termina el recorrido.</summary>
        public string Destino { get; set; } = string.Empty;

        /// <summary>Cuántos trayectos tiene la salida (1 en el caso normal).</summary>
        public int TrayectosCount { get; set; }
    }

    /// <summary>Un trabajador con salidas sin rendir y las salidas que le faltan.</summary>
    public class RecordatorioTrabajadorDto
    {
        public int WorkerId { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>workers.email_corporativo — es el destinatario principal del recordatorio.</summary>
        public string Email { get; set; } = string.Empty;

        public List<RecordatorioSalidaDto> Salidas { get; set; } = new();
    }

    /// <summary>
    /// Lo que el endpoint le devuelve al cron. No es para una pantalla: es lo que queda en el log
    /// de cron-job.org y lo único con lo que se puede saber qué pasó un día cualquiera.
    /// </summary>
    public class RecordatorioRendicionResultDto
    {
        /// <summary>Qué se hizo hoy, en una línea.</summary>
        public string Mensaje { get; set; } = string.Empty;

        /// <summary>"NINGUNO", "APERTURA" o "CIERRE".</summary>
        public string Momento { get; set; } = RecordatorioRendicionMomento.Ninguno.ToString().ToUpperInvariant();

        public DateOnly Hoy { get; set; }

        /// <summary>Periodo que se recuerda rendir ("agosto 2026").</summary>
        public string? Periodo { get; set; }

        /// <summary>Último día apto para rendir ese periodo.</summary>
        public DateOnly? LimiteRendicion { get; set; }

        /// <summary>Trabajadores con salidas sin rendir que se encontraron.</summary>
        public int TrabajadoresConPendientes { get; set; }

        /// <summary>Salidas sin rendir que suman entre todos.</summary>
        public int SalidasPendientes { get; set; }

        /// <summary>Correos efectivamente enviados (uno por trabajador).</summary>
        public int CorreosEnviados { get; set; }

        /// <summary>Correos que no salieron: el recordatorio apagado, sin destinatarios o error.</summary>
        public int CorreosOmitidos { get; set; }

        /// <summary>Una línea por trabajador, para poder auditar el día desde el log del cron.</summary>
        public List<string> Detalle { get; set; } = new();
    }
}
