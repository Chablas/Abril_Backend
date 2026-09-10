using Abril_Backend.Features.Ssoma.Rac.Dtos;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.Rac.Services;

public class RacNotificationService : IRacNotificationService
{
    private readonly IEmailService _email;
    private readonly ILogger<RacNotificationService> _logger;

    public RacNotificationService(IEmailService email, ILogger<RacNotificationService> logger)
    {
        _email  = email;
        _logger = logger;
    }

    public async Task NotificarRacCreadoAsync(RacDetalleDto detalle)
    {
        try
        {
            var body = $@"
<h2>Nuevo RAC registrado — {detalle.Codigo}</h2>
<table>
  <tr><td><b>Tipo:</b></td><td>{detalle.Tipo}</td></tr>
  <tr><td><b>Categoría:</b></td><td>{detalle.CategoriaNombre}</td></tr>
  <tr><td><b>Severidad:</b></td><td>{detalle.Severidad}</td></tr>
  <tr><td><b>Proyecto:</b></td><td>{detalle.ProyectoNombre ?? "-"}</td></tr>
  <tr><td><b>Empresa reportada:</b></td><td>{detalle.EmpresaReportadaNombre ?? "-"}</td></tr>
  <tr><td><b>Fecha:</b></td><td>{detalle.FechaReporte:dd/MM/yyyy HH:mm}</td></tr>
  <tr><td><b>Descripción:</b></td><td>{detalle.Descripcion}</td></tr>
</table>";

            await _email.SendAsync(
                to:     ["ssoma@abril.pe"],
                subject: $"[RAC] Nuevo registro: {detalle.Codigo} — {detalle.Severidad}",
                body:    body,
                isHtml:  true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enviando notificación RAC creado {Codigo}", detalle.Codigo);
        }
    }

}
