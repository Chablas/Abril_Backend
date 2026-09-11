using System.Globalization;
using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Services
{
    /// <summary>
    /// Los dos recordatorios del plazo de rendición (RG-33 y RG-34), en un solo punto de entrada
    /// porque hay un solo endpoint: el cron llama todos los días y acá se decide si hoy toca alguno.
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

        public async Task<RecordatorioRendicionResultDto> EjecutarAsync()
        {
            var ventana = await _repo.GetVentanaAsync();
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

            var layout = SalidaEmailLayout.Desde(_configuration);
            // Los recordatorios hablan de varias salidas, así que el botón lleva a la pantalla donde
            // se rinde y no al detalle de ninguna.
            var url = SalidaEnlaces.Autoservicio(_configuration);

            foreach (var trabajador in pendientes)
            {
                // Un correo que falla no corta el recorrido: al resto igual hay que avisarle.
                try
                {
                    // Se resuelve por trabajador porque el destinatario principal es él. Los
                    // destinatarios configurados son los mismos en todos, pero resolverlos acá es lo
                    // que garantiza que el recordatorio respete su configuración igual que el resto
                    // de los correos del módulo (mismo patrón que el aviso de pago masivo).
                    var envio = await _correoResolver.ResolveEnvioAsync(
                        eventoCodigo, new List<string> { trabajador.Email });

                    if (!envio.Enviar || envio.Para.Count == 0)
                    {
                        resultado.CorreosOmitidos++;
                        Anotar(resultado,
                            $"{trabajador.Nombre}: omitido (recordatorio apagado o sin destinatarios).");
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

                    var body = esApertura
                        ? RecordatorioRendicionEmailTemplates.Apertura(layout, datos, url)
                        : RecordatorioRendicionEmailTemplates.Cierre(layout, datos, url);

                    var asunto = esApertura
                        ? $"Ya puedes rendir tus movilidades de {periodo}"
                        : $"Hoy vence el plazo para rendir tus movilidades de {periodo}";

                    await _emailService.SendAsync(
                        to: envio.Para,
                        subject: asunto,
                        body: body,
                        isHtml: true,
                        cc: envio.Copia.Count > 0 ? envio.Copia : null);

                    resultado.CorreosEnviados++;
                    Anotar(resultado,
                        $"{trabajador.Nombre}: {trabajador.Salidas.Count} sin rendir → "
                        + string.Join(", ", envio.Para));
                }
                catch (Exception ex)
                {
                    resultado.CorreosOmitidos++;
                    Anotar(resultado, $"{trabajador.Nombre}: error al enviar.");
                    _logger.LogError(ex,
                        "Error enviando el recordatorio {Evento} al trabajador {WorkerId}",
                        eventoCodigo, trabajador.WorkerId);
                }
            }

            resultado.Mensaje = esApertura
                ? $"Se abrió el plazo de {periodo}: {resultado.CorreosEnviados} recordatorio(s) enviado(s) "
                  + $"de {pendientes.Count} trabajador(es) con salidas sin rendir."
                : $"Último día para rendir {periodo}: {resultado.CorreosEnviados} recordatorio(s) enviado(s) "
                  + $"de {pendientes.Count} trabajador(es) con salidas sin rendir.";

            return resultado;
        }

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
    }
}
