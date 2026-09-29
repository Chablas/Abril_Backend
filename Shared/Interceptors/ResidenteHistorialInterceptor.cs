using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace Abril_Backend.Shared.Interceptors
{
    /// <summary>
    /// Escribe <see cref="ProjectResidenteHistorial"/>: cada vez que un proyecto nace con residente
    /// o cambia de residente, deja una fila con el anterior, el nuevo, quién y cuándo. Va en el
    /// mismo SaveChanges, así que cubre cualquier pantalla que lo cambie por EF (Configuración →
    /// Proyectos, Habilitación → Gestión de responsables). Un UPDATE por SQL crudo no pasa por acá.
    /// Mismo patrón que <c>PenalidadEstadoHistorialInterceptor</c>.
    /// </summary>
    public class ResidenteHistorialInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ResidenteHistorialInterceptor(IHttpContextAccessor httpContextAccessor)
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
            var proyectos = ctx.ChangeTracker.Entries<Project>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified)
                .ToList();
            if (proyectos.Count == 0) return;

            var pendientes = ctx.ChangeTracker.Entries<ProjectResidenteHistorial>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();

            var ahora = DateTime.UtcNow;

            foreach (var entry in proyectos)
            {
                var residente = entry.Property(p => p.ResidenteWorkersId);
                int? anterior;

                if (entry.State == EntityState.Added)
                {
                    if (residente.CurrentValue == null) continue;
                    anterior = null;
                }
                else
                {
                    if (!residente.IsModified) continue;
                    if (Equals(residente.OriginalValue, residente.CurrentValue)) continue;
                    anterior = residente.OriginalValue;
                }

                var proyecto = entry.Entity;
                if (pendientes.Any(h => MismoProyecto(h, proyecto) && h.WorkersIdNuevo == residente.CurrentValue))
                    continue;

                var fila = new ProjectResidenteHistorial
                {
                    ProjectId         = proyecto.ProjectId,
                    Project           = proyecto.ProjectId == 0 ? proyecto : null,
                    WorkersIdAnterior = anterior,
                    WorkersIdNuevo    = residente.CurrentValue,
                    CambioDateTime    = ahora,
                    CambioUserId      = UserIdDeLaSesion(),
                    CreatedDateTime   = ahora,
                };

                ctx.Add(fila);
                pendientes.Add(fila);
            }
        }

        private static bool MismoProyecto(ProjectResidenteHistorial fila, Project proyecto) =>
            proyecto.ProjectId != 0 ? fila.ProjectId == proyecto.ProjectId : ReferenceEquals(fila.Project, proyecto);

        private int? UserIdDeLaSesion()
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
