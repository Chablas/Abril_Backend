using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services;

namespace Abril_Backend.Features.Ssoma.Penalidad.Services;

public class PenalidadNotificationService : IPenalidadNotificationService
{
    private const string SsomaBuzon = "ssoma@abril.pe";

    private readonly IEmailService _email;
    private readonly IProyectoResponsablesResolver _responsables;
    private readonly ILogger<PenalidadNotificationService> _logger;

    public PenalidadNotificationService(
        IEmailService email,
        IProyectoResponsablesResolver responsables,
        ILogger<PenalidadNotificationService> logger)
    {
        _email        = email;
        _responsables = responsables;
        _logger       = logger;
    }

    public async Task NotificarPendienteResidenteAsync(PenalidadDetalleDto p)
    {
        await EnviarSeguro(p.Codigo, "PendienteResidente", async () =>
        {
            var r = await _responsables.ResolverAsync(p.ProyectoId);
            if (string.IsNullOrWhiteSpace(r.ResidenteEmail)) return;

            var body = $@"
<h2>Penalidad pendiente de tu aprobación — {p.Codigo}</h2>
<p>Se registró una posible penalidad a <b>{p.EmpresaNombre ?? "-"}</b> por <b>{p.InfraccionNombre ?? "-"}</b>
(severidad {p.Severidad}, monto estimado S/ {p.MontoCalculado:N2}). Revisa y aprueba o rechaza en el sistema.</p>";

            await _email.SendAsync([r.ResidenteEmail], $"[Penalidad] Pendiente de aprobación: {p.Codigo}", body, true);
        });
    }

    public async Task NotificarPendienteGerenciaAsync(PenalidadDetalleDto p)
    {
        await EnviarSeguro(p.Codigo, "PendienteGerencia", async () =>
        {
            var r = await _responsables.ResolverAsync(p.ProyectoId);
            if (string.IsNullOrWhiteSpace(r.GerenteInmobiliarioEmail)) return;

            var body = $@"
<h2>Penalidad aprobada por Residencia, pendiente de tu aprobación — {p.Codigo}</h2>
<p>Empresa: <b>{p.EmpresaNombre ?? "-"}</b> — Infracción: <b>{p.InfraccionNombre ?? "-"}</b>
— Monto estimado: S/ {p.MontoCalculado:N2}. Revisa y aprueba o rechaza en el sistema.</p>";

            await _email.SendAsync([r.GerenteInmobiliarioEmail], $"[Penalidad] Pendiente de aprobación: {p.Codigo}", body, true);
        });
    }

    public async Task NotificarDescargoAlContratistaAsync(PenalidadDetalleDto p, string contratistaEmail)
    {
        await EnviarSeguro(p.Codigo, "DescargoAlContratista", async () =>
        {
            if (string.IsNullOrWhiteSpace(contratistaEmail)) return;

            var body = $@"
<p>En el marco de nuestro compromiso con la seguridad e integridad de los trabajadores y el
cumplimiento de la normativa legal vigente, le informamos que se ha identificado una posible
infracción (<b>{p.InfraccionNombre ?? "-"}</b>) asociada a su empresa en el proyecto
<b>{p.ProyectoNombre ?? "-"}</b>.</p>
<p>Nuestro objetivo no es sancionar sino asegurar que se mantengan las condiciones de trabajo
seguras y el cumplimiento contractual y legal aplicable.</p>
<p>Se le otorgan <b>{p.PlazoDescargoVenceEn:dd/MM/yyyy HH:mm}</b> (48 horas desde esta notificación,
salvo ampliación autorizada) para presentar su descargo y/o sustento documentario. De no recibir
respuesta en dicho plazo, se dará por aceptada la observación conforme al procedimiento vigente.</p>
<p>Código de penalidad: <b>{p.Codigo}</b></p>";

            await _email.SendAsync([contratistaEmail], $"[Penalidad] Notificación y derecho a descargo: {p.Codigo}", body, true);
        });
    }

    public async Task RecordatorioDescargoAsync(PenalidadDetalleDto p, string contratistaEmail)
    {
        await EnviarSeguro(p.Codigo, "RecordatorioDescargo", async () =>
        {
            if (string.IsNullOrWhiteSpace(contratistaEmail)) return;

            var body = $@"
<p>Le recordamos que el plazo para presentar su descargo sobre la penalidad <b>{p.Codigo}</b>
vence el <b>{p.PlazoDescargoVenceEn:dd/MM/yyyy HH:mm}</b>. De no recibir respuesta, se dará por
aceptada la observación conforme al procedimiento vigente.</p>";

            await _email.SendAsync([contratistaEmail], $"[Penalidad] Recordatorio de plazo: {p.Codigo}", body, true);
        });
    }

    public async Task NotificarEvaluacionPendienteAsync(PenalidadDetalleDto p)
    {
        await EnviarSeguro(p.Codigo, "EvaluacionPendiente", async () =>
        {
            var motivo = p.DescargoPorIncomparecencia ? "el contratista no respondió en el plazo" : "se recibió el descargo";
            var body = $@"<h2>Penalidad {p.Codigo} lista para evaluación</h2><p>{motivo}. Revisa y registra tu recomendación.</p>";
            await _email.SendAsync([SsomaBuzon], $"[Penalidad] Evaluar descargo: {p.Codigo}", body, true);
        });
    }

    public async Task NotificarDecisionPendienteAsync(PenalidadDetalleDto p)
    {
        await EnviarSeguro(p.Codigo, "DecisionPendiente", async () =>
        {
            var r = await _responsables.ResolverAsync(p.ProyectoId);
            if (string.IsNullOrWhiteSpace(r.GerenteInmobiliarioEmail)) return;

            var body = $@"
<h2>Penalidad {p.Codigo} — decisión final pendiente</h2>
<p>SSOMA recomienda: <b>{p.RecomendacionSsoma}</b>. Revisa el descargo y el argumento, y registra la decisión final.</p>";

            await _email.SendAsync([r.GerenteInmobiliarioEmail], $"[Penalidad] Decisión final pendiente: {p.Codigo}", body, true);
        });
    }

    public async Task NotificarDecisionFinalAsync(PenalidadDetalleDto p, string? contratistaEmail)
    {
        await EnviarSeguro(p.Codigo, "DecisionFinal", async () =>
        {
            var r = await _responsables.ResolverAsync(p.ProyectoId);

            var to = new List<string> { SsomaBuzon };
            if (!string.IsNullOrWhiteSpace(contratistaEmail)) to.Add(contratistaEmail);

            var cc = new List<string?> { r.ResidenteEmail, r.CoordSsomaEmail }
                .Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e!).Distinct().ToList();

            var body = $@"
<h2>Penalidad {p.Codigo} — {p.ResolucionTipo}</h2>
<table>
  <tr><td><b>Empresa:</b></td><td>{p.EmpresaNombre ?? "-"}</td></tr>
  <tr><td><b>Proyecto:</b></td><td>{p.ProyectoNombre ?? "-"}</td></tr>
  <tr><td><b>Infracción:</b></td><td>{p.InfraccionNombre ?? "-"}</td></tr>
  <tr><td><b>Monto final:</b></td><td>S/ {(p.MontoFinal ?? p.MontoCalculado):N2}</td></tr>
  <tr><td><b>Resolución:</b></td><td>{p.ResolucionTipo}</td></tr>
</table>
<p>{p.ResolucionTexto}</p>
<p>Se comunica para su ejecución a Oficina Técnica del proyecto y Costos y Presupuestos de
oficina central.</p>";

            await _email.SendAsync(to, $"[Penalidad] {p.ResolucionTipo}: {p.Codigo}", body, true, cc: cc);
        });
    }

    public async Task NotificarApelacionPresentadaAsync(PenalidadDetalleDto p)
    {
        await EnviarSeguro(p.Codigo, "ApelacionPresentada", async () =>
        {
            var r = await _responsables.ResolverAsync(p.ProyectoId);
            if (string.IsNullOrWhiteSpace(r.GerenteInmobiliarioEmail)) return;

            var body = $@"
<h2>Apelación presentada — Penalidad {p.Codigo}</h2>
<p>La empresa <b>{p.EmpresaNombre ?? "-"}</b> presentó una apelación con evidencia nueva sobre la
penalidad ya aplicada. Es la única apelación admitida para este caso. Revisa la evidencia y
registra la decisión final.</p>";

            await _email.SendAsync([r.GerenteInmobiliarioEmail], $"[Penalidad] Apelación presentada: {p.Codigo}", body, true);
        });
    }

    private async Task EnviarSeguro(string codigo, string paso, Func<Task> accion)
    {
        try { await accion(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enviando notificación de penalidad {Codigo} ({Paso})", codigo, paso);
        }
    }
}
