using System.Globalization;
using System.Net;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Graph.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Services
{
    /// <summary>
    /// Los dos recordatorios del Cronograma de Hitos. Vivían en ReminderService; se mudaron acá para
    /// que el cron y el envío manual de la Configuración usen el mismo plan: qué día sale cada uno,
    /// a quién (Cronograma de Hitos → Configuración) y con qué cuerpo.
    /// <list type="bullet">
    ///   <item>Cronograma pendiente: los días hábiles 1.º a 4.º de los últimos cinco del mes, uno
    ///     por residente que todavía no subió la versión del mes.</item>
    ///   <item>Resumen del mes: el día 1, las versiones que se subieron el mes que cerró.</item>
    /// </list>
    /// </summary>
    public class CronogramaRecordatoriosService : ICronogramaRecordatoriosService
    {
        private static readonly CultureInfo EsPe = new("es-PE");

        private readonly IMilestoneScheduleRepository _scheduleRepo;
        private readonly IMilestoneScheduleHistoryRepository _historyRepo;
        private readonly ICronogramaCorreosRepository _correosRepo;
        private readonly ICronogramaCorreoDestinatariosResolver _destinatarios;
        private readonly IEmailService _emailService;
        private readonly IEmailGroupResolver _emailGroupResolver;
        private readonly ILogger<CronogramaRecordatoriosService> _logger;
        private readonly string _frontendUrl;

        public CronogramaRecordatoriosService(
            IMilestoneScheduleRepository scheduleRepo,
            IMilestoneScheduleHistoryRepository historyRepo,
            ICronogramaCorreosRepository correosRepo,
            ICronogramaCorreoDestinatariosResolver destinatarios,
            IEmailService emailService,
            IEmailGroupResolver emailGroupResolver,
            IConfiguration configuration,
            ILogger<CronogramaRecordatoriosService> logger)
        {
            _scheduleRepo = scheduleRepo;
            _historyRepo = historyRepo;
            _correosRepo = correosRepo;
            _destinatarios = destinatarios;
            _emailService = emailService;
            _emailGroupResolver = emailGroupResolver;
            _logger = logger;
            _frontendUrl = configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
        }

        // ── Entradas ─────────────────────────────────────────────────────────

        public async Task EnviarDelDiaAsync(DateOnly hoy)
        {
            // Primero el resumen, como cuando los mandaba ReminderService. Cada uno decide solo si
            // hoy le toca: la mayoría de los días no sale ninguno.
            foreach (var codigo in new[] { CronogramaHitosCorreos.ResumenMensual, CronogramaHitosCorreos.CronogramaPendiente })
            {
                var plan = await PlanificarAsync(codigo, hoy);
                if (!plan.SeEnvia)
                    continue;

                var (enviados, fallidos) = await EnviarAsync(codigo, plan);
                _logger.LogInformation(
                    "Recordatorio {Codigo} del cronograma: {Enviados} enviado(s), {Fallidos} con error.",
                    codigo, enviados, fallidos);
            }
        }

        public async Task<CronogramaRecordatorioSimulacionDto> SimularAsync(string codigo, DateOnly fecha)
        {
            var plan = await PlanificarAsync(Validar(codigo), fecha);
            if (!plan.SeEnvia)
                return new CronogramaRecordatorioSimulacionDto { Motivo = plan.Motivo };

            // Todos los correos juntos y sin repetir a nadie: quien ya está en el Para no se repite
            // en la copia, ni quien está en la copia en la oculta.
            var para = Unicos(plan.Correos.SelectMany(c => c.Para), null);
            var copia = Unicos(plan.Correos.SelectMany(c => c.Cc), para);
            var oculta = Unicos(plan.Correos.SelectMany(c => c.Cco), para.Concat(copia));

            return new CronogramaRecordatorioSimulacionDto
            {
                SeEnvia = true,
                Correos = plan.Correos.Count,
                Para = para,
                Copia = copia,
                CopiaOculta = oculta,
            };
        }

        public async Task<CronogramaRecordatorioEnvioDto> EnviarManualAsync(string codigo, DateOnly fecha)
        {
            codigo = Validar(codigo);
            var plan = await PlanificarAsync(codigo, fecha);
            if (!plan.SeEnvia)
                return new CronogramaRecordatorioEnvioDto { Motivo = plan.Motivo };

            var (enviados, fallidos) = await EnviarAsync(codigo, plan);
            return new CronogramaRecordatorioEnvioDto { Enviados = enviados, Fallidos = fallidos };
        }

        private static string Validar(string codigo)
        {
            var limpio = (codigo ?? string.Empty).Trim().ToUpperInvariant();
            return CronogramaHitosCorreos.Recordatorios.Contains(limpio)
                ? limpio
                : throw new AbrilException("Solo los recordatorios se envían manualmente.", 400);
        }

        // ── Plan ─────────────────────────────────────────────────────────────

        private sealed record CorreoPlaneado(List<string> Para, List<string> Cc, List<string> Cco, string Asunto, string Cuerpo);

        /// <summary>Lo que sale ese día. Sin correos, <see cref="Motivo"/> dice por qué.</summary>
        private sealed record Plan(List<CorreoPlaneado> Correos, string? Motivo = null)
        {
            public bool SeEnvia => Correos.Count > 0;
            public static Plan Nada(string motivo) => new(new List<CorreoPlaneado>(), motivo);
        }

        private Task<Plan> PlanificarAsync(string codigo, DateOnly fecha) => codigo switch
        {
            CronogramaHitosCorreos.CronogramaPendiente => PlanPendienteAsync(fecha),
            CronogramaHitosCorreos.ResumenMensual => PlanResumenAsync(fecha),
            _ => throw new AbrilException("Solo los recordatorios se envían manualmente.", 400),
        };

        /// <summary>Uno por residente que no subió la versión del mes de <paramref name="fecha"/>.</summary>
        private async Task<Plan> PlanPendienteAsync(DateOnly fecha)
        {
            var feriados = await _correosRepo.GetFeriadosAsync(fecha.Year, fecha.Month);
            var dias = DiasDelPendiente(fecha.Year, fecha.Month, feriados);
            var mes = Mes(fecha.Year, fecha.Month);

            if (!dias.Contains(fecha))
                return Plan.Nada(dias.Count == 0
                    ? $"El {Dia(fecha)} no sale."
                    : $"El {Dia(fecha)} no sale. En {mes} sale los días hábiles del {DiaMes(dias[0])} al {DiaMes(dias[^1])}.");

            var lista = await _destinatarios.ObtenerAsync(CronogramaHitosCorreos.CronogramaPendiente);
            if (!lista.Activo)
                return Plan.Nada("El recordatorio está desactivado.");

            var pendientes = await _historyRepo.GetUsersWithoutScheduleHistoryAsync(fecha.Year, fecha.Month);
            if (pendientes.Count == 0)
                return Plan.Nada($"Todos los residentes subieron la versión de {mes}.");

            var platformUrl = $"{_frontendUrl}/auth/login";
            var correos = new List<CorreoPlaneado>();

            foreach (var item in pendientes)
            {
                // Armar no repite a nadie: si el residente también está en la lista, le llega una vez.
                var envio = lista.Armar(new[] { item.Email ?? string.Empty });
                if (!envio.Enviar)
                    continue;

                var proyectos = string.Join("",
                    (item.Projects ?? new()).Select(p => $"<li>{WebUtility.HtmlEncode(p.ProjectDescription)}</li>"));

                var cuerpo = $@"
                <p>Estimado(a) <strong>{WebUtility.HtmlEncode(item.UserFullName)}</strong>,</p>

                <p>
                    Te recordamos que tienes pendiente el envío mensual de
                    <strong>cronograma de hitos</strong> correspondiente a
                    <strong>{mes}</strong> en los siguientes proyectos:
                </p>

                <ul>
                    {proyectos}
                </ul>

                <p>
                    Por favor ingresa a la plataforma y completa el envío:
                </p>

                <p>
                    👉 <a href='{platformUrl}' target='_blank'>
                        Acceder a la plataforma
                    </a>
                </p>

                <p style='font-size: 12px; color: #666;'>
                    Este recordatorio se envía de manera automática a quienes aún no han registrado si tuvieron cambios o no
                    en su cronograma de hitos durante este mes.
                </p>

                <p>Gracias por tu compromiso con la mejora continua.</p>
                ";

                correos.Add(new CorreoPlaneado(envio.Para, envio.Cc, envio.Cco, CronogramaHitosAsuntos.CronogramaPendiente, cuerpo));
            }

            return correos.Count == 0 ? Plan.Nada("No hay destinatarios activos.") : new Plan(correos);
        }

        /// <summary>El día 1: las versiones que se subieron el mes que acaba de cerrar.</summary>
        private async Task<Plan> PlanResumenAsync(DateOnly fecha)
        {
            if (fecha.Day != 1)
                return Plan.Nada($"El {Dia(fecha)} no sale: sale el día 1 de cada mes.");

            var lista = await _destinatarios.ObtenerAsync(CronogramaHitosCorreos.ResumenMensual);
            if (!lista.Activo)
                return Plan.Nada("El recordatorio está desactivado.");

            var envio = lista.Armar();
            if (!envio.Enviar)
                return Plan.Nada("No hay destinatarios activos.");

            // El mes que cerró, en hora de Perú (UTC-5, sin horario de verano): empieza a las 05:00 UTC.
            var mesAnterior = new DateTime(fecha.Year, fecha.Month, 1).AddMonths(-1);
            var desdeUtc = new DateTime(mesAnterior.Year, mesAnterior.Month, 1, 5, 0, 0, DateTimeKind.Utc);
            var periodo = Mes(mesAnterior.Year, mesAnterior.Month);

            var cambios = await _scheduleRepo.GetSchedulesWithChangesAsync(desdeUtc, desdeUtc.AddMonths(1));
            if (cambios.Count == 0)
                return Plan.Nada($"Nadie subió versiones del cronograma en {periodo}.");

            var platformUrl = $"{_frontendUrl}/auth/login";

            var proyectos = string.Join("",
                cambios.Select(x =>
                {
                    // created_date_time está en UTC: el correo la muestra en hora de Perú.
                    var fechas = string.Join("<br/>",
                        x.ChangeDate
                            .OrderBy(d => d)
                            .Select(d => $"📅 {d.AddHours(-5):dd/MM/yyyy HH:mm}"));

                    return $@"
            <li>
                <strong>{WebUtility.HtmlEncode(x.ProjectDescription)}</strong><br/>
                👤 Usuario: {WebUtility.HtmlEncode(x.ChangedBy)}<br/>
                {fechas}
            </li>
            ";
                }));

            var cuerpo = $@"
            <p>Estimados,</p>

            <p>
                Se detectaron cambios en el cronograma de los siguientes proyectos
                durante <strong>{periodo}</strong>.
            </p>

            <p>Detalle:</p>

            <ul>
                {proyectos}
            </ul>

            <p>
                👉 <a href='{platformUrl}' target='_blank'>
                    Acceder a la plataforma
                </a>
            </p>

            <p style='font-size: 12px; color: #666;'>
                Este mensaje se envía automáticamente el primer día laboral de cada mes.
            </p>
            ";

            return new Plan(new List<CorreoPlaneado>
            {
                new(envio.Para, envio.Cc, envio.Cco, CronogramaHitosAsuntos.ResumenMensual(periodo), cuerpo),
            });
        }

        /// <summary>
        /// Los días en que sale el de cronograma pendiente: de los últimos cinco días hábiles del mes,
        /// los cuatro primeros (el quinto es el último hábil y ese día ya no sale). Es la misma
        /// ventana que la de Lecciones Aprendidas en ReminderService, sin sábados, domingos ni
        /// feriados; la lógica está copiada, como en LessonUploadWindow.
        /// </summary>
        private static List<DateOnly> DiasDelPendiente(int anio, int mes, HashSet<DateOnly> feriados)
        {
            // Del último hábil hacia atrás: [0] = el último del mes.
            var habiles = new List<DateOnly>();
            for (var d = new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes)); d.Month == mes; d = d.AddDays(-1))
            {
                var finDeSemana = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
                if (!finDeSemana && !feriados.Contains(d))
                    habiles.Add(d);
                if (habiles.Count == 5) break;
            }

            // Ordinal = habiles.Count - índice (1 = el más temprano): salen del 1 al 4.
            return habiles
                .Where((_, i) => habiles.Count - i <= 4)
                .OrderBy(d => d)
                .ToList();
        }

        // ── Envío ────────────────────────────────────────────────────────────

        /// <summary>
        /// Uno por uno: el que falla no corta a los demás. Los grupos de correo (p. ej. un buzón de
        /// jefaturas) se reemplazan por sus miembros antes de enviar, como hacía ReminderService.
        /// </summary>
        private async Task<(int Enviados, int Fallidos)> EnviarAsync(string codigo, Plan plan)
        {
            int enviados = 0, fallidos = 0;
            foreach (var correo in plan.Correos)
            {
                try
                {
                    await _emailService.SendAsync(
                        to: await ExpandirAsync(correo.Para) ?? correo.Para,
                        subject: correo.Asunto,
                        body: correo.Cuerpo,
                        isHtml: true,
                        cc: await ExpandirAsync(correo.Cc),
                        bcc: await ExpandirAsync(correo.Cco));
                    enviados++;
                }
                catch (Exception ex)
                {
                    fallidos++;
                    _logger.LogError(ex,
                        "No se pudo enviar el recordatorio {Codigo} del cronograma a {Para}.",
                        codigo, string.Join(", ", correo.Para));
                }
            }
            return (enviados, fallidos);
        }

        private async Task<List<string>?> ExpandirAsync(List<string>? correos)
        {
            if (correos == null || correos.Count == 0)
                return correos;

            // Si por algún motivo vuelve vacío, se conserva la lista original para no perder a nadie.
            var expandidos = await _emailGroupResolver.ExpandAsync(correos);
            return expandidos.Count > 0 ? expandidos : correos;
        }

        // ── Ayudantes ────────────────────────────────────────────────────────

        private static string Mes(int anio, int mes) =>
            new DateTime(anio, mes, 1).ToString("MMMM yyyy", EsPe);

        private static string Dia(DateOnly fecha) =>
            fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private static string DiaMes(DateOnly fecha) =>
            fecha.ToString("dd/MM", CultureInfo.InvariantCulture);

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
