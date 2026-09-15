using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services;

public class ProyectoResponsablesResolver : IProyectoResponsablesResolver
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ProyectoResponsablesResolver(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<ProyectoResponsablesDto> ResolverAsync(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var proyecto = await ctx.Project
            .Where(p => p.ProjectId == proyectoId)
            .Select(p => new { p.ResidenteWorkersId, p.EmailCoordSsoma })
            .FirstOrDefaultAsync();

        string? residenteEmail = null;
        if (proyecto?.ResidenteWorkersId is int residenteId)
        {
            residenteEmail = await ctx.Worker
                .Where(w => w.Id == residenteId)
                .Select(w => w.EmailCorporativo)
                .FirstOrDefaultAsync();
        }

        // Rol único a nivel de toda la compañía — mismo criterio que ya usa
        // InspeccionRepository.ResolverDestinatariosCierreAsync. Si en algún momento hay más de
        // un Worker activo con este puesto, FirstOrDefaultAsync (sin orden) elegiría uno
        // arbitrariamente: revisar ese caso antes de confiar en este resolver para montos.
        var gerenteInmobiliarioEmail = await ctx.Worker.AsNoTracking()
            .Where(w => w.PuestoCatalogo != null && w.PuestoCatalogo.Nombre.ToUpper() == "GERENTE INMOBILIARIO"
                     && w.Estado == "ACTIVO")
            .Select(w => w.EmailCorporativo)
            .FirstOrDefaultAsync();

        return new ProyectoResponsablesDto
        {
            ResidenteEmail           = residenteEmail,
            CoordSsomaEmail          = proyecto?.EmailCoordSsoma,
            GerenteInmobiliarioEmail = gerenteInmobiliarioEmail,
        };
    }
}
