using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Services;

public class AccidenteIncidenteService : IAccidenteIncidenteService
{
    private readonly IAccidenteIncidenteRepository _repo;
    private readonly ISharePointHabService _sp;
    private readonly IEmailService _email;
    private readonly ILogger<AccidenteIncidenteService> _logger;

    public AccidenteIncidenteService(
        IAccidenteIncidenteRepository repo,
        ISharePointHabService sp,
        IEmailService email,
        ILogger<AccidenteIncidenteService> logger)
    {
        _repo = repo;
        _sp = sp;
        _email = email;
        _logger = logger;
    }

    public Task<FlashReportInicializarDto> GetInicializarAsync() => _repo.GetInicializarAsync();

    public async Task<object> GetListAsync(int? proyectoId, int? tipoId, string? estado,
        DateTime? fechaDesde, DateTime? fechaHasta, bool? soloEnviados, int page, int pageSize)
    {
        var (items, total) = await _repo.GetListAsync(proyectoId, tipoId, estado, fechaDesde, fechaHasta, soloEnviados, page, pageSize);
        return new { items, total, page, pageSize, totalPages = (int)Math.Ceiling((double)total / pageSize) };
    }

    public async Task<FlashReportDetalleDto> GetDetalleAsync(int id)
    {
        return await _repo.GetDetalleAsync(id)
            ?? throw new AbrilException("Flash Report no encontrado.", 404);
    }

    public async Task<int> CrearAsync(CrearFlashReportRequest request, int? usuarioId)
    {
        // Obtener código del tipo
        var init = await _repo.GetInicializarAsync();
        var tipo = init.Tipos.FirstOrDefault(t => t.Id == request.TipoId)
            ?? throw new AbrilException("Tipo de flash report no válido.", 400);

        var codigo = await _repo.GenerarCodigoAsync(request.ProyectoId, tipo.Codigo ?? "XX");

        var urlFoto1 = await SubirFotoAsync(request.Foto1Base64, codigo, "foto1");
        var urlFoto2 = await SubirFotoAsync(request.Foto2Base64, codigo, "foto2");

        return await _repo.CrearAsync(request, codigo, urlFoto1, urlFoto2, usuarioId);
    }

    public async Task ActualizarAsync(int id, ActualizarFlashReportRequest request)
    {
        var existente = await _repo.GetDetalleAsync(id)
            ?? throw new AbrilException("Flash Report no encontrado.", 404);

        var urlFoto1 = request.Foto1Base64 != null
            ? await SubirFotoAsync(request.Foto1Base64, existente.Codigo, "foto1")
            : null;
        var urlFoto2 = request.Foto2Base64 != null
            ? await SubirFotoAsync(request.Foto2Base64, existente.Codigo, "foto2")
            : null;

        await _repo.ActualizarAsync(id, request, urlFoto1, urlFoto2);
    }

    public async Task EnviarFlashReportAsync(int id)
    {
        var fr = await _repo.GetDetalleAsync(id)
            ?? throw new AbrilException("Flash Report no encontrado.", 404);

        if (fr.Enviado)
            throw new AbrilException("El Flash Report ya fue enviado.", 400);

        // Descargar fotos si existen
        byte[]? foto1Bytes = null;
        byte[]? foto2Bytes = null;
        if (!string.IsNullOrEmpty(fr.UrlFoto1))
            foto1Bytes = await DescargarArchivoAsync(fr.UrlFoto1);
        if (!string.IsNullOrEmpty(fr.UrlFoto2))
            foto2Bytes = await DescargarArchivoAsync(fr.UrlFoto2);

        // Generar PDF
        var pdfBytes = FlashReportPdfService.Generar(fr, foto1Bytes, foto2Bytes);

        // Subir PDF a SharePoint
        var pdfNombre = $"FlashReport_{fr.Codigo}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        await using var pdfStream = new MemoryStream(pdfBytes);
        var urlPdf = await _sp.SubirArchivoEnRutaAsync(
            pdfStream, pdfNombre, "flash-report", $"flash-reports/{fr.Codigo}");

        // Marcar como enviado
        await _repo.MarcarEnviadoAsync(id, urlPdf ?? "");

        // Enviar email
        await EnviarEmailAsync(fr, pdfBytes, pdfNombre);
    }

    public async Task EliminarAsync(int id)
    {
        var fr = await _repo.GetDetalleAsync(id)
            ?? throw new AbrilException("Flash Report no encontrado.", 404);
        if (fr.Enviado)
            throw new AbrilException("No se puede eliminar un Flash Report ya enviado.", 400);
        await _repo.EliminarAsync(id);
    }

    // ── Privados ─────────────────────────────────────────────────────────────

    private async Task<string?> SubirFotoAsync(string? base64, string codigo, string nombreFoto)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64.Contains(',')
                ? base64[(base64.IndexOf(',') + 1)..]
                : base64);
            await using var stream = new MemoryStream(bytes);
            var ext = DetectarExtension(bytes);
            var fileName = $"{nombreFoto}_{DateTime.UtcNow:yyyyMMddHHmmss}.{ext}";
            return await _sp.SubirArchivoEnRutaAsync(stream, fileName, "flash-report", $"flash-reports/{codigo}/fotos");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo subir {Foto} del Flash Report {Codigo}", nombreFoto, codigo);
            return null;
        }
    }

    private async Task<byte[]?> DescargarArchivoAsync(string url)
    {
        try
        {
            var downloadUrl = await _sp.GetDownloadUrlAsync(url, "flash-report");
            if (string.IsNullOrEmpty(downloadUrl)) return null;
            using var http = new HttpClient();
            return await http.GetByteArrayAsync(downloadUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo descargar archivo {Url}", url);
            return null;
        }
    }

    private async Task EnviarEmailAsync(FlashReportDetalleDto fr, byte[] pdfBytes, string pdfNombre)
    {
        var destinatarios = new List<string> { "ssoma@abril.pe", "proyectos@abril.pe" };
        if (!string.IsNullOrWhiteSpace(fr.ElaboradoPorEmail))
            destinatarios.Add(fr.ElaboradoPorEmail);

        var asunto = $"Flash Report {fr.Codigo} - {fr.ProyectoNombre} - {fr.Fecha:dd/MM/yyyy}";
        var nivel = fr.ConsecuenciaRealPersonal.HasValue && fr.ConsecuenciaRealPersonal > 0
            ? $"Consecuencia real: Nivel {fr.ConsecuenciaRealPersonal}" : "";

        var cuerpo = $"""
            <p>Se ha generado y enviado el <strong>Flash Report {fr.Codigo}</strong>.</p>
            <table style="border-collapse:collapse;font-family:Arial;font-size:13px;">
              <tr><td style="padding:4px 12px;font-weight:bold;">Proyecto</td><td>{fr.ProyectoNombre}</td></tr>
              <tr><td style="padding:4px 12px;font-weight:bold;">Tipo</td><td>{fr.TipoNombre}</td></tr>
              <tr><td style="padding:4px 12px;font-weight:bold;">Fecha</td><td>{fr.Fecha:dd/MM/yyyy}</td></tr>
              <tr><td style="padding:4px 12px;font-weight:bold;">Trabajador</td><td>{fr.TrabajadorNombre ?? "—"}</td></tr>
              <tr><td style="padding:4px 12px;font-weight:bold;">Descripción</td><td>{fr.Descripcion}</td></tr>
              {(string.IsNullOrEmpty(nivel) ? "" : $"<tr><td style='padding:4px 12px;font-weight:bold;'>Severidad</td><td>{nivel}</td></tr>")}
              <tr><td style="padding:4px 12px;font-weight:bold;">Elaborado por</td><td>{fr.ElaboradoPorNombre ?? "—"}</td></tr>
            </table>
            <p style="color:#666;font-size:11px;margin-top:16px;">Adjunto encontrará el Flash Report en formato PDF.<br>Sistema SSOMA - Abril</p>
            """;

        await _email.SendAsync(
            destinatarios,
            asunto,
            cuerpo,
            isHtml: true,
            attachments: [new EmailAttachment { FileName = pdfNombre, Content = pdfBytes, ContentType = "application/pdf" }]
        );
    }

    private static string DetectarExtension(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8) return "jpg";
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50) return "png";
        return "jpg";
    }
}
