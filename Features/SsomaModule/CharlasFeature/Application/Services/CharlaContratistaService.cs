using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Services;

public class CharlaContratistaService : ICharlaContratistaService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ISharePointHabService _sp;

    public CharlaContratistaService(IDbContextFactory<AppDbContext> factory, ISharePointHabService sp)
    {
        _factory = factory;
        _sp = sp;
    }

    public async Task<List<CharlaContratistaPendienteDto>> GetPendientesAsync(int empresaId, DateOnly fecha)
    {
        using var ctx = _factory.CreateDbContext();

        var tareados = await (
            from t in ctx.SsTareo
            join d in ctx.SsTareoDetalleContratista on t.Id equals d.TareoId
            join p in ctx.Project on t.ProyectoId equals p.ProjectId
            where t.Fecha == fecha && d.EmpresaId == empresaId
            select new { t.ProyectoId, ProyectoNombre = p.ProjectDescription, d.CantidadPersonas }
        ).ToListAsync();

        if (tareados.Count == 0) return new List<CharlaContratistaPendienteDto>();

        var proyectoIds = tareados.Select(t => t.ProyectoId).Distinct().ToList();
        var subidas = await ctx.SsCharlaContratista
            .Where(c => c.State && c.EmpresaId == empresaId && c.Fecha == fecha && proyectoIds.Contains(c.ProyectoId))
            .ToDictionaryAsync(c => c.ProyectoId, c => c.Id);

        return tareados
            .GroupBy(t => new { t.ProyectoId, t.ProyectoNombre })
            .Select(g => new CharlaContratistaPendienteDto
            {
                ProyectoId = g.Key.ProyectoId,
                ProyectoNombre = g.Key.ProyectoNombre,
                Fecha = fecha,
                CantidadPersonasTareadas = g.Sum(x => x.CantidadPersonas),
                YaSubida = subidas.ContainsKey(g.Key.ProyectoId),
                CharlaId = subidas.TryGetValue(g.Key.ProyectoId, out var id) ? id : null,
            })
            .OrderBy(p => p.YaSubida)
            .ThenBy(p => p.ProyectoNombre)
            .ToList();
    }

    public async Task<List<CharlaContratistaDto>> GetHistorialAsync(int empresaId, int page, int pageSize)
    {
        using var ctx = _factory.CreateDbContext();
        page = page < 1 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        return await (
            from c in ctx.SsCharlaContratista
            join p in ctx.Project on c.ProyectoId equals p.ProjectId
            where c.State && c.EmpresaId == empresaId
            orderby c.Fecha descending, c.Id descending
            select new CharlaContratistaDto
            {
                Id = c.Id,
                ProyectoId = c.ProyectoId,
                ProyectoNombre = p.ProjectDescription,
                Fecha = c.Fecha,
                Tema = c.Tema,
                Descripcion = c.Descripcion,
                EvidenciaUrl = c.EvidenciaUrl,
                EvidenciaNombre = c.EvidenciaNombre,
                CreatedAt = c.CreatedAt,
            }
        ).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public async Task<CharlaContratistaDto> SubirAsync(int empresaId, CharlaContratistaUploadRequest req, int userId)
    {
        if (string.IsNullOrWhiteSpace(req.Tema))
            throw new AbrilException("El tema de la charla es requerido.", 400);
        if (!DateOnly.TryParse(req.Fecha, out var fecha))
            throw new AbrilException("Fecha inválida.", 400);

        using var ctx = _factory.CreateDbContext();

        // El contratista solo puede subir la charla de un día en el que su empresa
        // fue efectivamente tareada en ese proyecto (control de acceso).
        var fueTareado = await (
            from t in ctx.SsTareo
            join d in ctx.SsTareoDetalleContratista on t.Id equals d.TareoId
            where t.Fecha == fecha && t.ProyectoId == req.ProyectoId && d.EmpresaId == empresaId
            select t.Id
        ).AnyAsync();
        if (!fueTareado)
            throw new AbrilException("Tu empresa no fue tareada en ese proyecto para la fecha indicada.", 400);

        var yaExiste = await ctx.SsCharlaContratista.AnyAsync(c =>
            c.State && c.EmpresaId == empresaId && c.ProyectoId == req.ProyectoId && c.Fecha == fecha);
        if (yaExiste)
            throw new AbrilException("Ya registraste la charla de ese día para este proyecto.", 400);

        string? evidenciaUrl = null;
        if (!string.IsNullOrEmpty(req.EvidenciaBase64))
        {
            var base64 = req.EvidenciaBase64.Contains(',') ? req.EvidenciaBase64.Split(',')[1] : req.EvidenciaBase64;
            var bytes = Convert.FromBase64String(base64);
            var nombre = string.IsNullOrWhiteSpace(req.EvidenciaNombre) ? "evidencia.jpg" : req.EvidenciaNombre;
            var ext = Path.GetExtension(nombre);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"charla-contratista-{empresaId}-{fecha:yyyyMMdd}-{DateTime.UtcNow:HHmmssfff}{ext}";
            using var stream = new MemoryStream(bytes);
            evidenciaUrl = await _sp.SubirArchivoYObtenerUrlAsync(
                stream, fileName, "charlas-evidencias", $"Contratistas/{empresaId}/{fecha:yyyy}");
        }

        var entidad = new SsCharlaContratista
        {
            ProyectoId = req.ProyectoId,
            EmpresaId = empresaId,
            Fecha = fecha,
            Tema = req.Tema.Trim(),
            Descripcion = req.Descripcion,
            EvidenciaUrl = evidenciaUrl,
            EvidenciaNombre = req.EvidenciaNombre,
            SubidoPorUserId = userId,
        };
        ctx.SsCharlaContratista.Add(entidad);
        await ctx.SaveChangesAsync();

        var proyectoNombre = await ctx.Project
            .Where(p => p.ProjectId == req.ProyectoId)
            .Select(p => p.ProjectDescription)
            .FirstOrDefaultAsync() ?? "";

        return new CharlaContratistaDto
        {
            Id = entidad.Id,
            ProyectoId = entidad.ProyectoId,
            ProyectoNombre = proyectoNombre,
            Fecha = entidad.Fecha,
            Tema = entidad.Tema,
            Descripcion = entidad.Descripcion,
            EvidenciaUrl = entidad.EvidenciaUrl,
            EvidenciaNombre = entidad.EvidenciaNombre,
            CreatedAt = entidad.CreatedAt,
        };
    }

    public async Task<List<CharlaContratistaPendienteDto>> GetIncumplimientosAsync(DateOnly fecha, int? proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var tareadosQuery =
            from t in ctx.SsTareo
            join d in ctx.SsTareoDetalleContratista on t.Id equals d.TareoId
            join p in ctx.Project on t.ProyectoId equals p.ProjectId
            join emp in ctx.Contributor on d.EmpresaId equals emp.ContributorId
            where t.Fecha == fecha
            select new { t.ProyectoId, ProyectoNombre = p.ProjectDescription, d.EmpresaId, EmpresaNombre = emp.ContributorName, d.CantidadPersonas };

        if (proyectoId.HasValue)
            tareadosQuery = tareadosQuery.Where(x => x.ProyectoId == proyectoId.Value);

        var tareados = await tareadosQuery.ToListAsync();
        if (tareados.Count == 0) return new List<CharlaContratistaPendienteDto>();

        var subidas = await ctx.SsCharlaContratista
            .Where(c => c.State && c.Fecha == fecha)
            .Select(c => new { c.ProyectoId, c.EmpresaId })
            .ToListAsync();
        var subidasSet = subidas.Select(s => (s.ProyectoId, s.EmpresaId)).ToHashSet();

        return tareados
            .Where(t => !subidasSet.Contains((t.ProyectoId, t.EmpresaId)))
            .Select(t => new CharlaContratistaPendienteDto
            {
                ProyectoId = t.ProyectoId,
                ProyectoNombre = $"{t.ProyectoNombre} — {t.EmpresaNombre}",
                Fecha = fecha,
                CantidadPersonasTareadas = t.CantidadPersonas,
                YaSubida = false,
            })
            .OrderBy(p => p.ProyectoNombre)
            .ToList();
    }
}
