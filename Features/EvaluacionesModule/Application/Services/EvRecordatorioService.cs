using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Evaluaciones.Application.Services
{
    public class EvRecordatorioService : IEvRecordatorioService
    {
        private readonly IEvRecordatorioRepository _repo;
        private readonly IEmailService _email;
        private readonly ILogger<EvRecordatorioService> _logger;
        private const string GerenteProyectosEmail = "coriundo@abril.pe";

        public EvRecordatorioService(
            IEvRecordatorioRepository repo,
            IEmailService email,
            ILogger<EvRecordatorioService> logger)
        {
            _repo = repo;
            _email = email;
            _logger = logger;
        }

        public async Task<object> ProcesarRecordatoriosAsync()
        {
            var periodo = await _repo.GetPeriodoActivoAsync();
            if (periodo == null)
                return new { mensaje = "Sin período activo", enviados = 0 };

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            if (hoy < periodo.FechaApertura || hoy > periodo.FechaCierre)
                return new { mensaje = "Fuera de ventana de recordatorio", enviados = 0 };

            // Día 25 = primer aviso a todos; resto = solo pendientes
            bool esPrimerDia = hoy.Day == periodo.FechaApertura.Day;
            var tipoLog = esPrimerDia ? "PRIMER_AVISO" : $"RECORDATORIO_DIA_{hoy.Day}";

            var evaluadores = await _repo.GetEvaluadoresPendientesAsync(periodo.Id, !esPrimerDia);

            var mesAnio = new DateTime(periodo.Anio, periodo.Mes, 1)
                .ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-PE"));

            int enviados = 0;
            foreach (var ev in evaluadores)
            {
                // Evitar duplicado si el cron corre más de una vez al día
                if (await _repo.YaEnvioRecordatorioHoyAsync(periodo.Id, ev.UserId, tipoLog))
                    continue;

                var cc = new List<string>();
                if (!string.IsNullOrEmpty(ev.JefeEmail))
                    cc.Add(ev.JefeEmail);

                var asunto = $"[Evaluación Residentes] Recordatorio — {mesAnio}";
                var cuerpo = BuildCuerpoRecordatorio(ev, mesAnio, esPrimerDia);

                try
                {
                    await _email.SendAsync(
                        to: [ev.EmailPersonal],
                        subject: asunto,
                        body: cuerpo,
                        isHtml: true,
                        cc: cc.Count > 0 ? cc : null);

                    await _repo.RegistrarLogAsync(
                        periodo.Id, ev.UserId, tipoLog,
                        ev.EmailPersonal,
                        ccJefatura: cc.Count > 0,
                        ccGerencia: false);

                    enviados++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando recordatorio a {Email}", ev.EmailPersonal);
                }
            }

            return new { mensaje = "OK", enviados, fecha = hoy.ToString("yyyy-MM-dd"), esPrimerDia };
        }

        public async Task<object> ProcesarDescargoAsync()
        {
            var periodo = await _repo.GetPeriodoCerradoAyerAsync();
            if (periodo == null)
                return new { mensaje = "Sin período cerrado ayer", enviados = 0 };

            var noEvaluaron = await _repo.GetEvaluadoresPendientesAsync(periodo.Id, true);

            var mesAnio = new DateTime(periodo.Anio, periodo.Mes, 1)
                .ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-PE"));

            int enviados = 0;
            foreach (var ev in noEvaluaron)
            {
                var cc = new List<string> { GerenteProyectosEmail };
                if (!string.IsNullOrEmpty(ev.JefeEmail) && ev.JefeEmail != GerenteProyectosEmail)
                    cc.Add(ev.JefeEmail);

                var asunto = $"[Evaluación Residentes] Solicitud de descargo — {mesAnio}";
                var cuerpo = BuildCuerpoDescargo(ev, mesAnio);

                try
                {
                    await _email.SendAsync(
                        to: [ev.EmailPersonal],
                        subject: asunto,
                        body: cuerpo,
                        isHtml: true,
                        cc: cc);

                    await _repo.RegistrarLogAsync(
                        periodo.Id, ev.UserId, "DESCARGO",
                        ev.EmailPersonal,
                        ccJefatura: true,
                        ccGerencia: true);

                    enviados++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando descargo a {Email}", ev.EmailPersonal);
                }
            }

            return new { mensaje = "OK", enviados, periodo = mesAnio };
        }

        private static string BuildCuerpoRecordatorio(EvaluadorDto ev, string mesAnio, bool esPrimerDia)
        {
            var saludo = esPrimerDia
                ? "Se inicia el período de evaluación de residentes."
                : "Este es un recordatorio: aún tienes evaluaciones pendientes.";

            return $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px'>
  <div style='background:#1E3A5F;padding:16px 24px;border-radius:8px 8px 0 0'>
    <h2 style='color:#fff;margin:0;font-size:18px'>Evaluación de Residentes — {mesAnio}</h2>
  </div>
  <div style='background:#f8fafc;padding:24px;border:1px solid #e2e8f0;border-radius:0 0 8px 8px'>
    <p>Estimado/a <strong>{ev.NombreCompleto}</strong>,</p>
    <p>{saludo}</p>
    <p>El período de evaluación corresponde a <strong>{mesAnio}</strong>.</p>
    <p>Por favor ingresa a la plataforma Abril y completa la evaluación de los residentes a tu cargo.</p>
    <div style='margin:24px 0;text-align:center'>
      <a href='https://abril-frontend-m21l.onrender.com/evaluaciones/evaluar'
         style='background:#1E3A5F;color:#fff;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:bold'>
        Ir a Evaluaciones
      </a>
    </div>
    <p style='color:#64748b;font-size:0.85rem'>
      El período cierra el {ev.Subarea}. Si tienes consultas, contacta a tu jefe directo.
    </p>
  </div>
</div>";
        }

        private static string BuildCuerpoDescargo(EvaluadorDto ev, string mesAnio)
        {
            return $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px'>
  <div style='background:#dc2626;padding:16px 24px;border-radius:8px 8px 0 0'>
    <h2 style='color:#fff;margin:0;font-size:18px'>Solicitud de Descargo — Evaluación {mesAnio}</h2>
  </div>
  <div style='background:#f8fafc;padding:24px;border:1px solid #e2e8f0;border-radius:0 0 8px 8px'>
    <p>Estimado/a <strong>{ev.NombreCompleto}</strong>,</p>
    <p>El período de evaluación de residentes correspondiente a <strong>{mesAnio}</strong>
       ha concluido y <strong>no se registra ninguna evaluación</strong> de su parte.</p>
    <p>Se le solicita remitir el <strong>descargo correspondiente</strong> explicando
       los motivos por los cuales no completó las evaluaciones en el plazo establecido.</p>
    <p>Este correo ha sido enviado con copia a la Gerencia de Proyectos y a su jefe directo.</p>
    <hr style='border:none;border-top:1px solid #e2e8f0;margin:20px 0'>
    <p style='color:#64748b;font-size:0.85rem'>
      Sistema de Evaluaciones — Abril Grupo Inmobiliario
    </p>
  </div>
</div>";
        }
    }
}
