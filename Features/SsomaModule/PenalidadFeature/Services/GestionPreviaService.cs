using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using Abril_Backend.Features.Ssoma.Penalidad.Entities;
using Abril_Backend.Features.Ssoma.Rac.Services;
using Abril_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.Penalidad.Services;

public class GestionPreviaService : IGestionPreviaService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IRacSharePointService _spService;

    public GestionPreviaService(IDbContextFactory<AppDbContext> factory, IRacSharePointService spService)
    {
        _factory   = factory;
        _spService = spService;
    }

    public async Task<string> SubirAdjuntoAsync(int empresaId, IFormFile file)
    {
        using var stream = file.OpenReadStream();
        return await _spService.SubirGestionPreviaAdjuntoAsync(stream, file.FileName, empresaId);
    }

    public async Task<GestionPreviaDto> RegistrarAsync(GestionPreviaRegistrarRequest req, int userId)
    {
        if (string.IsNullOrWhiteSpace(req.Descripcion))
            throw new AbrilException("La descripción es obligatoria.", 400);
        if (req.Tipo is not ("Correo" or "CartaPreocupacion" or "Reunion" or "Llamada" or "Otro"))
            throw new AbrilException("Tipo inválido.", 400);

        using var ctx = _factory.CreateDbContext();

        var entidad = new GestionPreviaEmpresa
        {
            EmpresaId       = req.EmpresaId,
            ProyectoId      = req.ProyectoId,
            Tipo            = req.Tipo,
            Fecha           = DateTime.SpecifyKind(req.Fecha, DateTimeKind.Utc),
            Descripcion     = req.Descripcion,
            AdjuntoUrl      = req.AdjuntoUrl,
            RegistradoPorId = userId,
            CreatedAt       = DateTime.UtcNow,
        };
        ctx.GestionPreviaEmpresas.Add(entidad);
        await ctx.SaveChangesAsync();

        return await MapAsync(ctx, entidad);
    }

    public async Task<List<GestionPreviaDto>> GetListAsync(int empresaId)
    {
        using var ctx = _factory.CreateDbContext();
        var lista = await ctx.GestionPreviaEmpresas
            .Where(g => g.EmpresaId == empresaId)
            .OrderByDescending(g => g.Fecha)
            .ToListAsync();

        var result = new List<GestionPreviaDto>();
        foreach (var g in lista) result.Add(await MapAsync(ctx, g));
        return result;
    }

    public async Task<ContextoEmpresaDto> GetContextoEmpresaAsync(int empresaId)
    {
        using var ctx = _factory.CreateDbContext();
        var haceUnAnio = DateTime.UtcNow.AddMonths(-12);

        var aplicadasUltimos12Meses = await ctx.SsomaPenalidades
            .CountAsync(p => p.EmpresaId == empresaId && p.Estado == "Aplicada" && p.ResueltaEn >= haceUnAnio);
        var totalHistorico = await ctx.SsomaPenalidades
            .CountAsync(p => p.EmpresaId == empresaId && p.Estado == "Aplicada");

        return new ContextoEmpresaDto
        {
            PenalidadesAplicadasUltimos12Meses = aplicadasUltimos12Meses,
            PenalidadesTotalHistorico           = totalHistorico,
            GestionPrevia                       = await GetListAsync(empresaId),
        };
    }

    private static async Task<GestionPreviaDto> MapAsync(AppDbContext ctx, GestionPreviaEmpresa g)
    {
        string? registradoPorNombre = null;
        if (g.RegistradoPorId.HasValue)
        {
            registradoPorNombre = await ctx.Person
                .Where(p => p.UserId == g.RegistradoPorId.Value)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync();
        }

        return new GestionPreviaDto
        {
            Id                  = g.Id,
            EmpresaId           = g.EmpresaId,
            ProyectoId          = g.ProyectoId,
            Tipo                = g.Tipo,
            Fecha               = g.Fecha,
            Descripcion         = g.Descripcion,
            AdjuntoUrl          = g.AdjuntoUrl,
            RegistradoPorNombre = registradoPorNombre,
            CreatedAt           = g.CreatedAt,
        };
    }
}
