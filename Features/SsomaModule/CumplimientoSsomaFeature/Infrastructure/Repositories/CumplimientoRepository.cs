using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Repositories
{
    public class CumplimientoRepository : ICumplimientoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CumplimientoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // Hora de Lima (UTC-5), consistente con el resto del sistema (notificaciones, etc.)
        private static DateOnly HoyLima() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddHours(-5).Date);

        // Periodo vigente según la frecuencia de la actividad: el día, el lunes de
        // la semana en curso, o el día 1 del mes en curso.
        public static DateOnly PeriodoActual(string frecuencia)
        {
            var hoy = HoyLima();
            return frecuencia switch
            {
                "semanal" => hoy.AddDays(-((int)hoy.DayOfWeek == 0 ? 6 : (int)hoy.DayOfWeek - 1)),
                "mensual" => new DateOnly(hoy.Year, hoy.Month, 1),
                _ => hoy
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // CATÁLOGO
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<CumplimientoActividadDto>> GetActividadesAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsCumplimientoActividad
                .OrderBy(a => a.Frecuencia)
                .ThenBy(a => a.Orden)
                .Select(a => new CumplimientoActividadDto
                {
                    Id             = a.Id,
                    Nombre         = a.Nombre,
                    Descripcion    = a.Descripcion,
                    RolResponsable = a.RolResponsable,
                    Frecuencia     = a.Frecuencia,
                    Orden          = a.Orden,
                    Activo         = a.Activo
                })
                .ToListAsync();
        }

        public async Task<SsCumplimientoActividad> CreateActividadAsync(CumplimientoActividadUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entity = new SsCumplimientoActividad
            {
                Nombre         = dto.Nombre,
                Descripcion    = dto.Descripcion,
                RolResponsable = dto.RolResponsable,
                Frecuencia     = dto.Frecuencia,
                Orden          = dto.Orden,
                Activo         = true,
                CreatedAt      = now,
                UpdatedAt      = now
            };
            ctx.SsCumplimientoActividad.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateActividadAsync(int actividadId, CumplimientoActividadUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsCumplimientoActividad.FindAsync(actividadId)
                ?? throw new KeyNotFoundException($"Actividad {actividadId} no encontrada.");

            entity.Nombre         = dto.Nombre;
            entity.Descripcion    = dto.Descripcion;
            entity.RolResponsable = dto.RolResponsable;
            entity.Frecuencia     = dto.Frecuencia;
            entity.Orden          = dto.Orden;
            entity.UpdatedAt      = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // CUMPLIMIENTO POR PROYECTO
        // ─────────────────────────────────────────────────────────────────

        public async Task<CumplimientoResumenDto> GetResumenProyectoAsync(int proyectoId, string? rol)
        {
            using var ctx = _factory.CreateDbContext();

            var actividades = await ctx.SsCumplimientoActividad
                .Where(a => a.Activo && (rol == null || a.RolResponsable == rol || a.RolResponsable == "ambos"))
                .OrderBy(a => a.Frecuencia)
                .ThenBy(a => a.Orden)
                .ToListAsync();

            var registros = await ctx.SsCumplimientoRegistro
                .Where(r => r.ProyectoId == proyectoId)
                .ToListAsync();

            var cumplidoPorIds = registros
                .Where(r => r.CumplidoPorId.HasValue)
                .Select(r => r.CumplidoPorId!.Value)
                .Distinct()
                .ToList();

            var usuarios = await ctx.User
                .Where(u => cumplidoPorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Person.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var items = actividades.Select(a =>
            {
                var periodo = PeriodoActual(a.Frecuencia);
                var registro = registros.FirstOrDefault(r => r.ActividadId == a.Id && r.Periodo == periodo);

                return new CumplimientoItemDto
                {
                    ActividadId       = a.Id,
                    Nombre            = a.Nombre,
                    Descripcion       = a.Descripcion,
                    RolResponsable    = a.RolResponsable,
                    Frecuencia        = a.Frecuencia,
                    Periodo           = periodo,
                    Cumplido          = registro?.Cumplido ?? false,
                    FechaCumplimiento = registro?.FechaCumplimiento,
                    CumplidoPor       = registro?.CumplidoPorId.HasValue == true && usuarios.TryGetValue(registro.CumplidoPorId!.Value, out var nombre) ? nombre : null,
                    Observacion       = registro?.Observacion
                };
            }).ToList();

            return new CumplimientoResumenDto { ProyectoId = proyectoId, Actividades = items };
        }

        public async Task<CumplimientoItemDto> MarcarAsync(int proyectoId, int actividadId, CumplimientoMarcarDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var actividad = await ctx.SsCumplimientoActividad.FindAsync(actividadId)
                ?? throw new KeyNotFoundException($"Actividad {actividadId} no encontrada.");

            var periodo = PeriodoActual(actividad.Frecuencia);
            var now = DateTimeOffset.UtcNow;

            var registro = await ctx.SsCumplimientoRegistro
                .FirstOrDefaultAsync(r => r.ActividadId == actividadId && r.ProyectoId == proyectoId && r.Periodo == periodo);

            if (registro == null)
            {
                registro = new SsCumplimientoRegistro
                {
                    ActividadId = actividadId,
                    ProyectoId  = proyectoId,
                    Periodo     = periodo,
                    CreatedAt   = now
                };
                ctx.SsCumplimientoRegistro.Add(registro);
            }

            registro.Cumplido          = dto.Cumplido;
            registro.FechaCumplimiento = dto.Cumplido ? now : null;
            registro.CumplidoPorId     = dto.Cumplido ? userId : null;
            registro.Observacion       = dto.Observacion;
            registro.UpdatedAt         = now;

            await ctx.SaveChangesAsync();

            string? cumplidoPorNombre = null;
            if (registro.CumplidoPorId.HasValue)
            {
                cumplidoPorNombre = await ctx.User
                    .Where(u => u.UserId == registro.CumplidoPorId.Value)
                    .Select(u => u.Person.FullName)
                    .FirstOrDefaultAsync();
            }

            return new CumplimientoItemDto
            {
                ActividadId       = actividad.Id,
                Nombre            = actividad.Nombre,
                Descripcion       = actividad.Descripcion,
                RolResponsable    = actividad.RolResponsable,
                Frecuencia        = actividad.Frecuencia,
                Periodo           = periodo,
                Cumplido          = registro.Cumplido,
                FechaCumplimiento = registro.FechaCumplimiento,
                CumplidoPor       = cumplidoPorNombre,
                Observacion       = registro.Observacion
            };
        }
    }
}
