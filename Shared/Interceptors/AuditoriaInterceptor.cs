using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace Abril_Backend.Shared.Interceptors
{
    public class AuditoriaInterceptor : SaveChangesInterceptor
    {
        private static readonly HashSet<string> TablasAuditar = new()
        {
            "ss_hab_trabajador", "ss_hab_empresa", "ss_hab_equipo",
            "ss_sctr_vidaley", "ss_empresa_contratista", "ss_equipo",
            "ss_induccion", "ss_eval_supervisor"
        };

        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditoriaInterceptor(IHttpContextAccessor httpContextAccessor)
            => _httpContextAccessor = httpContextAccessor;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is null)
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            var ctx = eventData.Context;
            var ahora = DateTime.UtcNow;

            foreach (var entry in ctx.ChangeTracker.Entries())
            {
                if (entry.Entity is AuditoriaCambio) continue;

                if (entry.State == EntityState.Added)
                {
                    SetTimestampIfPresent(entry, "CreatedAt", ahora);
                    SetTimestampIfPresent(entry, "UpdatedAt", ahora);
                }
                else if (entry.State == EntityState.Modified)
                {
                    SetTimestampIfPresent(entry, "UpdatedAt", ahora);
                }
            }

            var userId = ObtenerUserId();
            var usuarioNombre = ObtenerUsuarioNombre();
            var empresaId = ObtenerEmpresaId();
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

            var auditorias = new List<AuditoriaCambio>();

            var entriesParaAuditar = ctx.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Entity is not AuditoriaCambio)
                .ToList();

            foreach (var entry in entriesParaAuditar)
            {
                var tableName = entry.Metadata.GetTableName() ?? string.Empty;
                if (!TablasAuditar.Contains(tableName)) continue;

                var accion = entry.State switch
                {
                    EntityState.Added => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => "UNKNOWN"
                };

                var registroId = 0;
                if (entry.Properties.Any(p => p.Metadata.Name == "Id"))
                {
                    var idVal = entry.Property("Id").CurrentValue;
                    registroId = idVal switch
                    {
                        int i => i,
                        long l => (int)l,
                        _ => 0
                    };
                }

                string? anterior = null;
                string? nuevo = null;

                if (entry.State == EntityState.Modified)
                {
                    var propsCambiadas = entry.Properties
                        .Where(p => p.IsModified)
                        .ToList();

                    anterior = JsonSerializer.Serialize(
                        propsCambiadas.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                    nuevo = JsonSerializer.Serialize(
                        propsCambiadas.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                }
                else if (entry.State == EntityState.Added)
                {
                    nuevo = JsonSerializer.Serialize(
                        entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                }
                else if (entry.State == EntityState.Deleted)
                {
                    anterior = JsonSerializer.Serialize(
                        entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                }

                auditorias.Add(new AuditoriaCambio
                {
                    Tabla = tableName,
                    RegistroId = registroId,
                    Accion = accion,
                    DatosAnteriores = anterior,
                    DatosNuevos = nuevo,
                    UsuarioId = userId,
                    UsuarioNombre = usuarioNombre,
                    EmpresaContratistaId = empresaId,
                    IpAddress = ip,
                    CreatedAt = ahora
                });
            }

            if (auditorias.Count > 0)
                await ctx.Set<AuditoriaCambio>().AddRangeAsync(auditorias, cancellationToken);

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void SetTimestampIfPresent(
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
            string propertyName, DateTime ahora)
        {
            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);
            if (prop is null) return;

            var clrType = prop.Metadata.ClrType;
            if (clrType == typeof(DateTime) || clrType == typeof(DateTime?))
                prop.CurrentValue = ahora;
            else if (clrType == typeof(DateTimeOffset) || clrType == typeof(DateTimeOffset?))
                prop.CurrentValue = new DateTimeOffset(ahora, TimeSpan.Zero);
        }

        private int? ObtenerUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private string? ObtenerUsuarioNombre()
            => _httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.Name)?.Value;

        private int? ObtenerEmpresaId()
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("empresaId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
