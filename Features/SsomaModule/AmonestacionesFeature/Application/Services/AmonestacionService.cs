using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Services;

public class AmonestacionService : IAmonestacionService
{
    private readonly IAmonestacionRepository _repo;
    private readonly AmonestacionNotificationService _notif;
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ILogger<AmonestacionService> _logger;
    private readonly string AbrilLogoPath =
        Path.Combine(AppContext.BaseDirectory, "Templates", "logo-abril.jpg");

    public AmonestacionService(
        IAmonestacionRepository repo,
        AmonestacionNotificationService notif,
        IDbContextFactory<AppDbContext> factory,
        ILogger<AmonestacionService> logger)
    {
        _repo    = repo;
        _notif   = notif;
        _factory = factory;
        _logger  = logger;
    }

    public Task<AmonestacionInitDto> GetInitAsync() => _repo.GetInitAsync();

    public async Task<AmonestacionCreadaDto> CrearAsync(AmonestacionCreateRequest req, int userId)
    {
        if (req.PuntosInfraccion < 0 || req.PuntosInfraccion > 10)
            throw new AbrilException("Los puntos por infracción deben estar entre 0 y 10.", 400);

        if (req.AplicaPenalizacion && req.SancionInfraccionId is null)
            throw new AbrilException("Debe seleccionar una sanción si aplica penalización.", 400);

        var codigo = await _repo.GenerarCodigoAsync(req.ProyectoId);

        // Calcular monto
        decimal monto = 0m;
        if (req.AplicaPenalizacion && req.SancionInfraccionId.HasValue)
        {
            using var ctx = _factory.CreateDbContext();
            var infraccion = await ctx.SsomaRacInfracciones
                .Where(i => i.Id == req.SancionInfraccionId.Value)
                .FirstOrDefaultAsync();
            if (infraccion is not null)
            {
                if (infraccion.MontoFijo.HasValue && infraccion.MontoFijo > 0)
                    monto = infraccion.MontoFijo.Value;
                else if (infraccion.FactorUit.HasValue && infraccion.FactorUit > 0)
                {
                    var uit = await ctx.SsomaUitAnios
                        .Where(u => u.Anio == DateTime.UtcNow.Year && u.Activo)
                        .Select(u => u.Valor)
                        .FirstOrDefaultAsync();
                    monto = infraccion.FactorUit.Value * uit;
                }
            }
        }

        // Decodificar fotos de base64
        var fotosBytes = new List<(byte[] Bytes, string Nombre)>();
        foreach (var foto in req.Fotos)
        {
            try
            {
                var base64 = foto.Base64.Contains(',')
                    ? foto.Base64.Split(',')[1]
                    : foto.Base64;
                fotosBytes.Add((Convert.FromBase64String(base64), foto.NombreArchivo));
            }
            catch
            {
                // foto inválida, ignorar
            }
        }

        // Construir DTO de detalle para crear
        var detalle = new AmonestacionDetalleDto
        {
            Codigo              = codigo,
            PersonaReportaId    = userId > 0 ? userId : null,
            ProyectoId          = req.ProyectoId,
            Fecha               = DateTime.TryParse(req.Fecha, out var fd)
                                    ? DateTime.SpecifyKind(fd, DateTimeKind.Utc)
                                    : DateTime.UtcNow,
            WorkerId            = req.WorkerId,
            PartidaId           = req.PartidaId,
            TipoSancionId       = req.TipoSancionId,
            InfraccionTipoId    = req.InfraccionTipoId,
            Descripcion         = req.Descripcion,
            AplicaPenalizacion  = req.AplicaPenalizacion,
            SancionInfraccionId = req.SancionInfraccionId,
            MontoCalculado      = monto,
            PuntosInfraccion    = req.PuntosInfraccion,
            DiasSuspension      = req.DiasSuspension,
            FechaInicioSuspension = req.FechaInicioSuspension != null
                ? DateOnly.TryParse(req.FechaInicioSuspension, out var fi) ? fi : null
                : null,
            FechaFinSuspension  = req.FechaFinSuspension != null
                ? DateOnly.TryParse(req.FechaFinSuspension, out var ff) ? ff : null
                : null,
        };

        // Guardar en BD
        var fotasParaRepo = fotosBytes.Select(f => (Convert.ToBase64String(f.Bytes), f.Nombre)).ToList();
        var id = await _repo.CrearAsync(detalle, fotasParaRepo);

        // Obtener detalle completo para PDF y notificación
        var detalleCompleto = await _repo.GetDetalleAsync(id);
        if (detalleCompleto is null) return new AmonestacionCreadaDto { Id = id, Codigo = codigo };

        // Generar PDF
        try
        {
            byte[]? logoBytes = null;
            if (detalleCompleto.EsEmpresaAbril && File.Exists(AbrilLogoPath))
                logoBytes = await File.ReadAllBytesAsync(AbrilLogoPath);
            else if (!detalleCompleto.EsEmpresaAbril && !string.IsNullOrEmpty(detalleCompleto.EmpresaLogoUrl))
            {
                // El logo de contratista está en SharePoint/storage, para PDF usamos Abril logo como fallback
                if (File.Exists(AbrilLogoPath))
                    logoBytes = await File.ReadAllBytesAsync(AbrilLogoPath);
            }

            var pdfBytes = AmonestacionPdfService.GenerarPdf(
                detalleCompleto,
                fotosBytes.Select(f => f.Bytes).ToList(),
                logoBytes);

            // Enviar correo en background
            _ = Task.Run(async () =>
            {
                try { await _notif.NotificarAmonestacionAsync(detalleCompleto, pdfBytes); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error notificando amonestacion {Id}", id); }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generando PDF amonestacion {Id}", id);
        }

        return new AmonestacionCreadaDto { Id = id, Codigo = codigo };
    }

    public async Task<AmonestacionPagedResult<AmonestacionListItemDto>> GetListAsync(AmonestacionListQuery q)
    {
        var (items, total) = await _repo.GetListAsync(q);
        return new AmonestacionPagedResult<AmonestacionListItemDto>
        {
            Items    = items,
            Total    = total,
            Page     = q.Page,
            PageSize = q.PageSize
        };
    }

    public Task<AmonestacionDetalleDto?> GetDetalleAsync(int id) => _repo.GetDetalleAsync(id);

    public Task<AmonestacionDashboardDto> GetDashboardAsync() => _repo.GetDashboardAsync();

    public Task<WorkerPuntajeDto?> GetPuntajeWorkerAsync(int workerId) =>
        _repo.GetPuntajeWorkerAsync(workerId);

    public async Task<byte[]> GetPdfAsync(int id)
    {
        var detalle = await _repo.GetDetalleAsync(id)
            ?? throw new AbrilException("Amonestación no encontrada.", 404);

        byte[]? logoBytes = null;
        if (File.Exists(AbrilLogoPath))
            logoBytes = await File.ReadAllBytesAsync(AbrilLogoPath);

        return AmonestacionPdfService.GenerarPdf(detalle, new List<byte[]>(), logoBytes);
    }
}
