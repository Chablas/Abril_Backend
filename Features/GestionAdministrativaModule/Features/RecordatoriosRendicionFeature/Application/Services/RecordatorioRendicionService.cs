using System.Globalization;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Services
{
    /// <summary>
    /// Los dos recordatorios del plazo de rendición (RG-33 y RG-34). El cron llama todos los días y
    /// acá se decide si hoy toca alguno:
    ///
    /// <list type="bullet">
    ///   <item>Primer día hábil del mes → se abrió el plazo para rendir el mes anterior.</item>
    ///   <item>Último día apto para rendir → hoy vence.</item>
    /// </list>
    ///
    /// El "último día apto" no es una fecha fija: sale del plazo configurado en Solicitud de
    /// Salidas → Configuración → Días reembolsables, contado sobre los días hábiles del mes. Por
    /// eso la ventana se pregunta todos los días en vez de escribir un número acá.
    ///
    /// Va un correo POR TRABAJADOR con la lista de sus salidas sin rendir, y cada uno respeta la
    /// configuración de destinatarios de su recordatorio (sección Recordatorios): apagar el
    /// recordatorio no envía nada, y apagar al destinatario principal lo saca del envío dejando el
    /// correo en manos de los destinatarios configurados.
    ///
    /// El envío manual de esa misma sección simula un día cualquiera: pasa por la misma ventana y
    /// arma los mismos correos (<see cref="ArmarCorreosAsync"/>), así que dice exactamente lo que el
    /// cron mandaría ese día.
    /// </summary>
    public class RecordatorioRendicionService : IRecordatorioRendicionService
    {
        private readonly IRecordatorioRendicionRepository       _repo;
        private readonly ICorreoSalidaRecipientResolver         _correoResolver;
        private readonly IEmailService                          _emailService;
        private readonly IConfiguration                         _configuration;
        private readonly ILogger<RecordatorioRendicionService>  _logger;

        private static readonly CultureInfo EsPe = CultureInfo.GetCultureInfo("es-PE");

        /// <summary>
        /// Tope de líneas de detalle en la respuesta. Con toda la oficina rindiendo, el detalle
        /// completo puede pasar del tamaño que el cron externo guarda y cortarle la respuesta a la
        /// mitad. Los contadores van igual y el detalle completo queda en los correos enviados.
        /// </summary>
        private const int MaxDetallesEnRespuesta = 30;

        public RecordatorioRendicionService(
            IRecordatorioRendicionRepository repo,
            ICorreoSalidaRecipientResolver correoResolver,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<RecordatorioRendicionService> logger)
        {
            _repo           = repo;
            _correoResolver = correoResolver;
            _emailService   = emailService;
            _configuration  = configuration;
            _logger         = logger;
        }

        // ── Cron ─────────────────────────────────────────────────────────────

        public async Task<RecordatorioRendicionResultDto> EjecutarAsync()
        {
            var ventana = await _repo.GetVentanaAsync(MesAnteriorPeru.HoyPeru());
            var periodo = NombreDeMes(ventana.PeriodoAnio, ventana.PeriodoMes);

            var resultado = new RecordatorioRendicionResultDto
            {
                Hoy      = ventana.Hoy,
                Momento  = ventana.Momento.ToString().ToUpperInvariant(),
                Periodo  = periodo,
            };

            // La mayoría de los días no toca ninguno de los dos: se corta acá y no se buscan
            // pendientes. El cron igual recibe una respuesta que dice por qué no hizo nada.
            if (ventana.Momento == RecordatorioRendicionMomento.Ninguno)
            {
                resultado.Mensaje =
                    $"Hoy no toca recordatorio. El plazo de {periodo} se abrió el "
                    + $"{ventana.PrimerDiaHabil:dd/MM/yyyy} y vence el {ventana.LimiteRendicion:dd/MM/yyyy}.";
                return resultado;
            }

            resultado.LimiteRendicion = ventana.LimiteRendicion;

            var pendientes = await _repo.GetPendientesAsync(ventana.PeriodoDesde, ventana.PeriodoHasta);

            resultado.TrabajadoresConPendientes = pendientes.Count;
            resultado.SalidasPendientes         = pendientes.Sum(t => t.Salidas.Count);

            if (pendientes.Count == 0)
            {
                resultado.Mensaje = $"Nadie tiene salidas de {periodo} sin rendir: no se envió ningún recordatorio.";
                return resultado;
            }

            var esApertura = ventana.Momento == RecordatorioRendicionMomento.Apertura;
            var eventoCodigo = esApertura
                ? CorreoEventoCodigos.RecordatorioRendicionApertura
                : CorreoEventoCodigos.RecordatorioRendicionCierre;

            foreach (var correo in await ArmarCorreosAsync(eventoCodigo, ventana, periodo, pendientes))
            {
                var trabajador = correo.Trabajador;

                if (!correo.SeEnvia)
                {
                    resultado.CorreosOmitidos++;
                    Anotar(resultado,
                        $"{trabajador.Nombre}: omitido (recordatorio apagado o sin destinatarios).");
                    continue;
                }

                // Un correo que falla no corta el recorrido: al resto igual hay que avisarle.
                if (await EnviarAsync(eventoCodigo, correo))
                {
                    resultado.CorreosEnviados++;
                    Anotar(resultado,
                        $"{trabajador.Nombre}: {trabajador.Salidas.Count} sin rendir → "
                        + string.Join(", ", correo.Envio.Para));
                }
                else
                {
                    resultado.CorreosOmitidos++;
                    Anotar(resultado, $"{trabajador.Nombre}: error al enviar.");
                }
            }

            resultado.Mensaje = esApertura
                ? $"Se abrió el plazo de {periodo}: {resultado.CorreosEnviados} recordatorio(s) enviado(s) "
                  + $"de {pendientes.Count} trabajador(es) con salidas sin rendir."
                : $"Último día para rendir {periodo}: {resultado.CorreosEnviados} recordatorio(s) enviado(s) "
                  + $"de {pendientes.Count} trabajador(es) con salidas sin rendir.";

            return resultado;
        }

        // ── Envío manual (Configuración → Recordatorios) ─────────────────────

        public async Task<RecordatorioSimulacionDto> SimularAsync(string eventoCodigo, DateOnly fecha)
        {
            var (correos, motivo) = await PlanManualAsync(eventoCodigo, fecha);
            if (motivo != null)
                return new RecordatorioSimulacionDto { Motivo = motivo };

            // Todos los correos juntos y sin repetir a nadie: quien ya está en el Para no se repite
            // en la copia.
            var para = Unicos(correos.SelectMany(c => c.Envio.Para), null);
            return new RecordatorioSimulacionDto
            {
                SeEnvia = true,
                Correos = correos.Count,
                Para    = para,
                Copia   = Unicos(correos.SelectMany(c => c.Envio.Copia), para),
            };
        }

        public async Task<RecordatorioEnvioManualDto> EnviarManualAsync(string eventoCodigo, DateOnly fecha)
        {
            var (correos, motivo) = await PlanManualAsync(eventoCodigo, fecha);
            if (motivo != null)
                return new RecordatorioEnvioManualDto { Motivo = motivo };

            var resultado = new RecordatorioEnvioManualDto();
            foreach (var correo in correos)
            {
                if (await EnviarAsync(eventoCodigo, correo)) resultado.Enviados++;
                else resultado.Fallidos++;
            }
            return resultado;
        }

        /// <summary>
        /// Los correos que saldrían ese día, o por qué no sale ninguno. Ese día tiene que ser el del
        /// recordatorio pedido según la misma ventana del cron: con el plazo en 1 día hábil los dos
        /// caen el mismo día, y ese día el cron manda solo el de cierre.
        /// </summary>
        private async Task<(List<CorreoRecordatorio> Correos, string? Motivo)> PlanManualAsync(string eventoCodigo, DateOnly fecha)
        {
            var esApertura = eventoCodigo == CorreoEventoCodigos.RecordatorioRendicionApertura;
            if (!esApertura && eventoCodigo != CorreoEventoCodigos.RecordatorioRendicionCierre)
                throw new AbrilException("Solo los recordatorios se envían manualmente.", 400);

            var ventana = await _repo.GetVentanaAsync(fecha);
            var periodo = NombreDeMes(ventana.PeriodoAnio, ventana.PeriodoMes);
            var momento = esApertura ? RecordatorioRendicionMomento.Apertura : RecordatorioRendicionMomento.Cierre;

            if (ventana.Momento != momento)
            {
                var mes = NombreDeMes(fecha.Year, fecha.Month);
                var motivo = esApertura && ventana.PrimerDiaHabil == ventana.LimiteRendicion
                    ? $"En {mes} no sale: el primer día hábil también es el último para rendir, y ese día sale el de cierre."
                    : $"El {Dia(fecha)} no sale. En {mes} sale el {Dia(esApertura ? ventana.PrimerDiaHabil : ventana.LimiteRendicion)}.";
                return (new(), motivo);
            }

            if (!await _repo.RecordatorioActivoAsync(eventoCodigo))
                return (new(), "El recordatorio está desactivado.");

            var pendientes = await _repo.GetPendientesAsync(ventana.PeriodoDesde, ventana.PeriodoHasta);
            if (pendientes.Count == 0)
                return (new(), $"Nadie tiene salidas de {periodo} sin rendir.");

            var correos = (await ArmarCorreosAsync(eventoCodigo, ventana, periodo, pendientes))
                .Where(c => c.SeEnvia)
                .ToList();

            return correos.Count == 0 ? (correos, "No hay destinatarios activos.") : (correos, null);
        }

        // ── Correos ──────────────────────────────────────────────────────────

        /// <summary>Un correo del recordatorio: a quién va y, si sale, con qué.</summary>
        private sealed record CorreoRecordatorio(
            RecordatorioTrabajadorDto Trabajador, CorreoSalidaEnvioDto Envio, string Asunto, string Cuerpo)
        {
            public bool SeEnvia => Envio.Enviar && Envio.Para.Count > 0;
        }

        /// <summary>
        /// Uno por trabajador. La configuración de destinatarios se lee una sola vez: es la misma
        /// para todos y lo único que cambia es el principal, que es cada trabajador.
        /// </summary>
        private async Task<List<CorreoRecordatorio>> ArmarCorreosAsync(
            string eventoCodigo, RecordatorioVentanaDto ventana, string periodo, List<RecordatorioTrabajadorDto> pendientes)
        {
            var armar = await _correoResolver.PrepararEnvioAsync(eventoCodigo);
            var esApertura = eventoCodigo == CorreoEventoCodigos.RecordatorioRendicionApertura;

            var layout = SalidaEmailLayout.Desde(_configuration);
            // Los recordatorios hablan de varias salidas, así que el botón lleva a la pantalla donde
            // se rinde y no al detalle de ninguna.
            var url = SalidaEnlaces.Autoservicio(_configuration);
            var asunto = esApertura
                ? CorreoSalidaAsuntos.RecordatorioApertura(periodo)
                : CorreoSalidaAsuntos.RecordatorioCierre(periodo);

            var correos = new List<CorreoRecordatorio>();
            foreach (var trabajador in pendientes)
            {
                var envio = armar(new List<string> { trabajador.Email });
                if (!envio.Enviar || envio.Para.Count == 0)
                {
                    correos.Add(new CorreoRecordatorio(trabajador, envio, asunto, string.Empty));
                    continue;
                }

                var datos = new RecordatorioCorreoDatos
                {
                    Trabajador           = trabajador.Nombre,
                    Periodo              = periodo,
                    LimiteRendicion      = ventana.LimiteRendicion,
                    DiasHabilesRestantes = ventana.DiasHabilesRestantes,
                    Salidas              = trabajador.Salidas
                        .Select(s => new RecordatorioCorreoSalida(
                            s.Codigo, s.FechaSalida, s.Motivo, s.Origen, s.Destino, s.TrayectosCount))
                        .ToList(),
                };

                var cuerpo = esApertura
                    ? RecordatorioRendicionEmailTemplates.Apertura(layout, datos, url)
                    : RecordatorioRendicionEmailTemplates.Cierre(layout, datos, url);

                correos.Add(new CorreoRecordatorio(trabajador, envio, asunto, cuerpo));
            }
            return correos;
        }

        /// <summary>false = el envío falló (queda en el log); los demás siguen.</summary>
        private async Task<bool> EnviarAsync(string eventoCodigo, CorreoRecordatorio correo)
        {
            try
            {
                await _emailService.SendAsync(
                    to: correo.Envio.Para,
                    subject: correo.Asunto,
                    body: correo.Cuerpo,
                    isHtml: true,
                    cc: correo.Envio.Copia.Count > 0 ? correo.Envio.Copia : null);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error enviando el recordatorio {Evento} al trabajador {WorkerId}",
                    eventoCodigo, correo.Trabajador.WorkerId);
                return false;
            }
        }

        // ── Ayudantes ────────────────────────────────────────────────────────

        /// <summary>
        /// Agrega una línea al detalle sin pasarse del tope. Al llegar al límite deja una última
        /// línea diciendo que se recortó, para que el log no parezca haberse cortado solo.
        /// </summary>
        private static void Anotar(RecordatorioRendicionResultDto resultado, string linea)
        {
            if (resultado.Detalle.Count < MaxDetallesEnRespuesta)
                resultado.Detalle.Add(linea);
            else if (resultado.Detalle.Count == MaxDetallesEnRespuesta)
                resultado.Detalle.Add("… detalle recortado; los totales de arriba sí están completos.");
        }

        /// <summary>El mes en palabras ("agosto 2026"), que es como lo nombran el correo y el log.</summary>
        private static string NombreDeMes(int anio, int mes) =>
            new DateTime(anio, mes, 1).ToString("MMMM yyyy", EsPe);

        private static string Dia(DateOnly fecha) =>
            fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private static List<string> Unicos(IEnumerable<string> correos, IEnumerable<string>? yaNombrados)
        {
            var vistos = new HashSet<string>(yaNombrados ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var resultado = new List<string>();
            foreach (var c in correos)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                var limpio = c.Trim();
                if (vistos.Add(limpio)) resultado.Add(limpio);
            }
            return resultado;
        }
    }
}
