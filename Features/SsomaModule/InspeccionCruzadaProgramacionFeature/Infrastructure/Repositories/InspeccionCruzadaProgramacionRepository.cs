using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Infrastructure.Repositories
{
    public class InspeccionCruzadaProgramacionRepository : IInspeccionCruzadaProgramacionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public InspeccionCruzadaProgramacionRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<(int ProyectoId, string Nombre)>> GetProyectosActivosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var proyectos = await ctx.Project
                .Where(p => p.State)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new { p.ProjectId, p.ProjectDescription })
                .ToListAsync();

            return proyectos.Select(p => (p.ProjectId, p.ProjectDescription)).ToList();
        }

        // ── Anillos ──────────────────────────────────────────────────────

        public async Task<List<SsInspeccionCruzadaAnillo>> GetAnillosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Set<SsInspeccionCruzadaAnillo>().OrderBy(a => a.Id).ToListAsync();
        }

        public async Task<SsInspeccionCruzadaAnillo> CrearAnilloAsync(string nombre)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = new SsInspeccionCruzadaAnillo { Nombre = nombre, CreatedAt = DateTime.UtcNow };
            ctx.Set<SsInspeccionCruzadaAnillo>().Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        // ── Miembros ──────────────────────────────────────────────────────

        public async Task<List<SsInspeccionCruzadaRotacion>> GetMiembrosAsync(int anilloId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Set<SsInspeccionCruzadaRotacion>()
                .Include(m => m.Proyecto)
                .Where(m => m.AnilloId == anilloId)
                .OrderBy(m => m.Orden)
                .ToListAsync();
        }

        public async Task<List<SsInspeccionCruzadaRotacion>> GetTodosLosMiembrosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Set<SsInspeccionCruzadaRotacion>()
                .Include(m => m.Proyecto)
                .OrderBy(m => m.AnilloId).ThenBy(m => m.Orden)
                .ToListAsync();
        }

        public async Task<SsInspeccionCruzadaRotacion> AgregarMiembroAsync(int anilloId, int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            var maxOrden = await ctx.Set<SsInspeccionCruzadaRotacion>()
                .Where(m => m.AnilloId == anilloId)
                .Select(m => (int?)m.Orden)
                .MaxAsync() ?? -1;

            var entity = new SsInspeccionCruzadaRotacion
            {
                AnilloId = anilloId,
                ProyectoId = proyectoId,
                Orden = maxOrden + 1,
                Activo = true,
                CreatedAt = DateTime.UtcNow,
            };
            ctx.Set<SsInspeccionCruzadaRotacion>().Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> ReordenarAsync(List<(int Id, int Orden)> items)
        {
            using var ctx = _factory.CreateDbContext();
            var ids = items.Select(i => i.Id).ToList();
            var entidades = await ctx.Set<SsInspeccionCruzadaRotacion>()
                .Where(m => ids.Contains(m.Id))
                .ToListAsync();

            foreach (var e in entidades)
            {
                e.Orden = items.First(i => i.Id == e.Id).Orden;
                e.UpdatedAt = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetActivoAsync(int id, bool activo)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.Set<SsInspeccionCruzadaRotacion>().FirstOrDefaultAsync(m => m.Id == id);
            if (entity is null) return false;

            entity.Activo = activo;
            entity.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return true;
        }

        // ── Cursor ──────────────────────────────────────────────────────────

        public async Task<SsInspeccionCruzadaCursor> GetOrCreateCursorAsync(int anilloId)
        {
            using var ctx = _factory.CreateDbContext();
            var cursor = await ctx.Set<SsInspeccionCruzadaCursor>().FirstOrDefaultAsync(c => c.AnilloId == anilloId);
            if (cursor is not null) return cursor;

            cursor = new SsInspeccionCruzadaCursor { AnilloId = anilloId, Offset = 1 };
            ctx.Set<SsInspeccionCruzadaCursor>().Add(cursor);
            await ctx.SaveChangesAsync();
            return cursor;
        }

        public async Task GuardarCursorAsync(int anilloId, int offset, int anio, int mes)
        {
            using var ctx = _factory.CreateDbContext();
            var cursor = await ctx.Set<SsInspeccionCruzadaCursor>().FirstOrDefaultAsync(c => c.AnilloId == anilloId);
            if (cursor is null)
            {
                cursor = new SsInspeccionCruzadaCursor { AnilloId = anilloId };
                ctx.Set<SsInspeccionCruzadaCursor>().Add(cursor);
            }

            cursor.Offset = offset;
            cursor.UltimoAnio = anio;
            cursor.UltimoMes = mes;
            cursor.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ── Programación ──────────────────────────────────────────────────

        public async Task<List<SsInspeccionCruzadaProgramacion>> GetProgramacionAsync(
            int anioDesde, int mesDesde, int anioHasta, int mesHasta)
        {
            using var ctx = _factory.CreateDbContext();
            var claveDesde = anioDesde * 100 + mesDesde;
            var claveHasta = anioHasta * 100 + mesHasta;

            var items = await ctx.Set<SsInspeccionCruzadaProgramacion>().ToListAsync();
            return items
                .Where(p => (p.Anio * 100 + p.Mes) >= claveDesde && (p.Anio * 100 + p.Mes) <= claveHasta)
                .OrderBy(p => p.Anio).ThenBy(p => p.Mes).ThenBy(p => p.AnilloId).ThenBy(p => p.ProyectoInspectorId)
                .ToList();
        }

        public async Task<SsInspeccionCruzadaProgramacion?> GetProgramacionByIdAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Set<SsInspeccionCruzadaProgramacion>().FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<SsInspeccionCruzadaProgramacion> CrearProgramacionAsync(
            int anio, int mes, int anilloId, int proyectoInspectorId, int proyectoInspeccionadoId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = new SsInspeccionCruzadaProgramacion
            {
                Anio = anio,
                Mes = mes,
                AnilloId = anilloId,
                ProyectoInspectorId = proyectoInspectorId,
                ProyectoInspeccionadoId = proyectoInspeccionadoId,
                CreatedAt = DateTime.UtcNow,
            };
            ctx.Set<SsInspeccionCruzadaProgramacion>().Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task GuardarProgramacionAsync(SsInspeccionCruzadaProgramacion programacion)
        {
            using var ctx = _factory.CreateDbContext();

            if (programacion.CreatedAt.Kind != DateTimeKind.Utc)
                programacion.CreatedAt = DateTime.SpecifyKind(programacion.CreatedAt, DateTimeKind.Utc);

            ctx.Set<SsInspeccionCruzadaProgramacion>().Update(programacion);
            programacion.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<Dictionary<int, string>> GetProyectoNombresAsync(IEnumerable<int> proyectoIds)
        {
            using var ctx = _factory.CreateDbContext();
            var ids = proyectoIds.Distinct().ToList();
            return await ctx.Project
                .Where(p => ids.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription);
        }
    }
}
