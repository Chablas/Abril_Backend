using Abril_Backend.Features.Ssoma.Penalidad.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace Abril_Backend.Features.Ssoma.Penalidad.Infrastructure;

/// <summary>
/// Escribe la bitácora de estados de la penalidad (<see cref="SsomaPenalidadEstadoHistorial"/>):
/// cada vez que una <see cref="SsomaPenalidad"/> nace o cambia de <c>Estado</c>, deja una fila
/// con el estado del que salió, al que entró, quién lo movió y cuándo. Calcado del mismo patrón
/// que ya usa GTH (RequerimientoEstadoHistorialInterceptor), adaptado a un <c>Estado</c> string
/// en vez de una FK a un catálogo — el pipeline de penalidades es fijo, no configurable.
/// </summary>
public class PenalidadEstadoHistorialInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PenalidadEstadoHistorialInterceptor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Registrar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Registrar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Registrar(DbContext ctx)
    {
        var penalidades = ctx.ChangeTracker.Entries<SsomaPenalidad>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();
        if (penalidades.Count == 0) return;

        var pendientes = ctx.ChangeTracker.Entries<SsomaPenalidadEstadoHistorial>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        var ahora      = DateTime.UtcNow;
        var userSesion = UserIdDeLaSesion();

        foreach (var entry in penalidades)
        {
            var pen      = entry.Entity;
            var estadoProp = entry.Property(p => p.Estado);

            string? estadoAnterior;
            DateTime? cuando;
            int? quien;

            if (entry.State == EntityState.Added)
            {
                estadoAnterior = null;
                cuando         = pen.CreatedAt;
                quien          = pen.CreatedBy;
            }
            else
            {
                if (!estadoProp.IsModified) continue;
                if (Equals(estadoProp.OriginalValue, estadoProp.CurrentValue)) continue;

                estadoAnterior = estadoProp.OriginalValue;
                cuando = entry.Property(p => p.UpdatedAt).IsModified ? pen.UpdatedAt : null;
                quien  = userSesion;
            }

            var estadoNuevo = estadoProp.CurrentValue!;

            if (pendientes.Any(h => MismaPenalidad(h, pen) && h.EstadoNuevo == estadoNuevo))
                continue;

            var fila = new SsomaPenalidadEstadoHistorial
            {
                PenalidadId     = pen.Id,
                Penalidad       = pen.Id == 0 ? pen : null,
                EstadoAnterior  = estadoAnterior,
                EstadoNuevo     = estadoNuevo,
                CambioDateTime  = cuando ?? ahora,
                CambioUserId    = quien ?? userSesion,
                CreatedDateTime = ahora,
            };

            ctx.Add(fila);
            pendientes.Add(fila);
        }
    }

    private static bool MismaPenalidad(SsomaPenalidadEstadoHistorial fila, SsomaPenalidad pen) =>
        pen.Id != 0 ? fila.PenalidadId == pen.Id : ReferenceEquals(fila.Penalidad, pen);

    private int? UserIdDeLaSesion()
    {
        var claim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
