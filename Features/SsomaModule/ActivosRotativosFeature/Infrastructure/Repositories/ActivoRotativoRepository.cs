using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Repositories
{
    public class ActivoRotativoRepository : IActivoRotativoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ActivoRotativoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ─────────────────────────────────────────────────────────────────
        // MATERIALES (catálogo único)
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<ActivoRotativoMaterialDto>> GetMaterialesAsync()
        {
            using var ctx = _factory.CreateDbContext();

            var materiales = await ctx.SsActivoRotativoMaterial
                .Include(m => m.PresupuestoItem)
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            // Cantidad comprada/despachada según S10 (acumulado global, todos los
            // proyectos): suma de las líneas de egreso activas del ítem vinculado.
            var itemIds = materiales.Where(m => m.PresupuestoItemId.HasValue)
                .Select(m => m.PresupuestoItemId!.Value).Distinct().ToList();

            var compradoPorItem = await ctx.SsConsumoLinea
                .Where(l => l.Activo && l.ItemId.HasValue && itemIds.Contains(l.ItemId.Value))
                .GroupBy(l => l.ItemId!.Value)
                .Select(g => new { ItemId = g.Key, Total = g.Sum(l => l.CantidadReal ?? l.Cantidad) })
                .ToDictionaryAsync(x => x.ItemId, x => x.Total);

            var registradoPorMaterial = await ctx.SsActivoRotativo
                .Where(a => a.Activo && a.Estado != "baja")
                .GroupBy(a => a.MaterialId)
                .Select(g => new { MaterialId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.MaterialId, x => x.Total);

            return materiales.Select(m => new ActivoRotativoMaterialDto
            {
                Id                    = m.Id,
                Nombre                = m.Nombre,
                Orden                 = m.Orden,
                Activo                = m.Activo,
                TotalActivos          = m.Activos.Count(a => a.Activo),
                PresupuestoItemId     = m.PresupuestoItemId,
                PresupuestoItemNombre = m.PresupuestoItem?.Nombre,
                CantidadCompradaS10   = m.PresupuestoItemId.HasValue
                    ? (compradoPorItem.TryGetValue(m.PresupuestoItemId.Value, out var total) ? total : 0)
                    : null,
                CantidadRegistrada = registradoPorMaterial.TryGetValue(m.Id, out var reg) ? reg : 0
            }).ToList();
        }

        public async Task<SsActivoRotativoMaterial> CreateMaterialAsync(ActivoRotativoMaterialUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entity = new SsActivoRotativoMaterial
            {
                Nombre            = dto.Nombre,
                Orden             = dto.Orden,
                PresupuestoItemId = dto.PresupuestoItemId,
                Activo            = true,
                CreatedAt         = now,
                UpdatedAt         = now
            };
            ctx.SsActivoRotativoMaterial.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateMaterialAsync(int materialId, ActivoRotativoMaterialUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsActivoRotativoMaterial.FindAsync(materialId)
                ?? throw new KeyNotFoundException($"Material {materialId} no encontrado.");

            entity.Nombre            = dto.Nombre;
            entity.Orden             = dto.Orden;
            entity.PresupuestoItemId = dto.PresupuestoItemId;
            entity.UpdatedAt         = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // Solo se puede borrar un material que no tenga ningún activo registrado —
        // si ya hay activos, hay que reasignarlos o dejarlos como están.
        public async Task DeleteMaterialAsync(int materialId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsActivoRotativoMaterial
                .Include(m => m.Activos)
                .FirstOrDefaultAsync(m => m.Id == materialId)
                ?? throw new KeyNotFoundException($"Material {materialId} no encontrado.");

            if (entity.Activos.Any(a => a.Activo))
                throw new InvalidOperationException("No se puede eliminar: hay activos registrados con este material.");

            ctx.SsActivoRotativoMaterial.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // Coordinadores SSOMA y Prevencionistas de Abril (staff propio — contrata_casa
        // = 'Casa', no de contratista), igual criterio que EvJefeSsomaRepository: el
        // puesto real (categoria_id 41/35) en vez de un user_role que nadie asignaba.
        public async Task<List<ResponsableSsomaDto>> GetResponsablesSsomaAsync()
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var pool = await conn.QueryAsync<ResponsableSsomaDto>(
                @"SELECT DISTINCT p.full_name AS Nombre, w.email_corporativo AS Email
                  FROM workers w
                  JOIN person p ON p.person_id = w.person_id
                  JOIN puesto pu ON pu.puesto_id = w.puesto_id AND pu.categoria_id IN (@CategoriaCoordinadorSsoma, @CategoriaPrevencionista)
                  WHERE w.state
                    AND w.contrata_casa = 'Casa'
                    AND w.workers_estado_id = @WorkersEstadoActivo
                    AND EXISTS (SELECT 1 FROM worker_vinculaciones wv WHERE wv.worker_id = w.id AND wv.fecha_fin IS NULL)
                  ORDER BY p.full_name",
                new
                {
                    CategoriaCoordinadorSsoma = CategoriaIds.CoordinadorSsoma,
                    CategoriaPrevencionista = CategoriaIds.Prevencionista,
                    WorkersEstadoActivo = WorkersEstadoIds.Activo
                });

            return pool.ToList();
        }

        public async Task<List<PresupuestoItemBuscarDto>> BuscarItemsPresupuestoAsync(string q)
        {
            using var ctx = _factory.CreateDbContext();
            var query = ctx.SsMaterialItem.Include(i => i.Familia).Where(i => i.Activo && !i.NoUsar);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(i => i.Nombre.ToLower().Contains(q.Trim().ToLower()));

            return await query
                .OrderBy(i => i.Nombre)
                .Take(50)
                .Select(i => new PresupuestoItemBuscarDto
                {
                    Id            = i.Id,
                    Nombre        = i.Nombre,
                    NombreFamilia = i.Familia.Nombre
                })
                .ToListAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // ACTIVOS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<ActivoRotativoListDto>> GetActivosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsActivoRotativo
                .Include(a => a.Material)
                .Include(a => a.ProyectoActual)
                .Where(a => a.Activo)
                .OrderBy(a => a.Material!.Nombre)
                .ThenBy(a => a.Codigo)
                .Select(a => new ActivoRotativoListDto
                {
                    Id                   = a.Id,
                    MaterialId           = a.MaterialId,
                    MaterialNombre       = a.Material != null ? a.Material.Nombre : "",
                    Codigo               = a.Codigo,
                    Estado               = a.Estado,
                    ProyectoActualId     = a.ProyectoActualId,
                    ProyectoActualNombre = a.ProyectoActual != null ? a.ProyectoActual.ProjectDescription : null,
                    ResponsableNombre    = a.ResponsableNombre,
                    ResponsableTelefono  = a.ResponsableTelefono,
                    Activo               = a.Activo
                })
                .ToListAsync();
        }

        public async Task<ActivoRotativoDetalleDto?> GetActivoDetalleAsync(int activoId)
        {
            using var ctx = _factory.CreateDbContext();
            var a = await ctx.SsActivoRotativo
                .Include(x => x.Material)
                .Include(x => x.ProyectoActual)
                .Include(x => x.Movimientos)
                    .ThenInclude(m => m.ProyectoOrigen)
                .Include(x => x.Movimientos)
                    .ThenInclude(m => m.ProyectoDestino)
                .FirstOrDefaultAsync(x => x.Id == activoId);

            if (a == null) return null;

            var movidoPorIds = a.Movimientos
                .Where(m => m.MovidoPorId.HasValue)
                .Select(m => m.MovidoPorId!.Value)
                .Distinct()
                .ToList();

            var usuarios = await ctx.User
                .Where(u => movidoPorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Person.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            return new ActivoRotativoDetalleDto
            {
                Id                   = a.Id,
                MaterialId           = a.MaterialId,
                MaterialNombre       = a.Material?.Nombre ?? "",
                Codigo               = a.Codigo,
                Estado               = a.Estado,
                ProyectoActualId     = a.ProyectoActualId,
                ProyectoActualNombre = a.ProyectoActual?.ProjectDescription,
                ResponsableNombre    = a.ResponsableNombre,
                ResponsableTelefono  = a.ResponsableTelefono,
                Activo               = a.Activo,
                Observaciones        = a.Observaciones,
                Historial = a.Movimientos
                    .OrderByDescending(m => m.FechaMovimiento)
                    .Select(m => new ActivoRotativoMovimientoDto
                    {
                        Id                   = m.Id,
                        ProyectoOrigenNombre = m.ProyectoOrigen?.ProjectDescription,
                        ProyectoDestinoNombre = m.ProyectoDestino?.ProjectDescription,
                        FechaMovimiento      = m.FechaMovimiento,
                        MovidoPor            = m.MovidoPorId.HasValue && usuarios.TryGetValue(m.MovidoPorId.Value, out var nombre) ? nombre : null,
                        Observacion          = m.Observacion
                    }).ToList()
            };
        }

        public async Task<SsActivoRotativo> CreateActivoAsync(ActivoRotativoUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var material = await ctx.SsActivoRotativoMaterial.FindAsync(dto.MaterialId)
                ?? throw new KeyNotFoundException($"Material {dto.MaterialId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            var entity = new SsActivoRotativo
            {
                Nombre              = material.Nombre,
                MaterialId          = dto.MaterialId,
                Codigo              = dto.Codigo,
                Estado              = dto.Estado,
                ProyectoActualId    = dto.ProyectoActualId,
                ResponsableNombre   = dto.ResponsableNombre,
                ResponsableTelefono = dto.ResponsableTelefono,
                Observaciones       = dto.Observaciones,
                Activo              = true,
                CreatedAt           = now,
                UpdatedAt           = now
            };
            ctx.SsActivoRotativo.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateActivoAsync(int activoId, ActivoRotativoUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsActivoRotativo.FindAsync(activoId)
                ?? throw new KeyNotFoundException($"Activo {activoId} no encontrado.");

            var material = await ctx.SsActivoRotativoMaterial.FindAsync(dto.MaterialId)
                ?? throw new KeyNotFoundException($"Material {dto.MaterialId} no encontrado.");

            entity.Nombre              = material.Nombre;
            entity.MaterialId          = dto.MaterialId;
            entity.Codigo              = dto.Codigo;
            entity.Estado              = dto.Estado;
            entity.ProyectoActualId    = dto.ProyectoActualId;
            entity.ResponsableNombre   = dto.ResponsableNombre;
            entity.ResponsableTelefono = dto.ResponsableTelefono;
            entity.Observaciones       = dto.Observaciones;
            entity.UpdatedAt           = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // Borrado real (no "dar de baja"): para activos de prueba que nunca debieron
        // contabilizarse, no un decomiso real que deba quedar de historial.
        public async Task DeleteActivoAsync(int activoId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsActivoRotativo
                .Include(a => a.Movimientos)
                .FirstOrDefaultAsync(a => a.Id == activoId)
                ?? throw new KeyNotFoundException($"Activo {activoId} no encontrado.");

            ctx.SsActivoRotativoMovimiento.RemoveRange(entity.Movimientos);
            ctx.SsActivoRotativo.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        public async Task<SsActivoRotativo> MoverActivoAsync(int activoId, ActivoRotativoMoverDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsActivoRotativo.FindAsync(activoId)
                ?? throw new KeyNotFoundException($"Activo {activoId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            var proyectoOrigen = entity.ProyectoActualId;

            var movimiento = new SsActivoRotativoMovimiento
            {
                ActivoId          = activoId,
                ProyectoOrigenId  = proyectoOrigen,
                ProyectoDestinoId = dto.NuevoProyectoId,
                FechaMovimiento   = now,
                MovidoPorId       = userId,
                Observacion       = dto.Observacion,
                CreatedAt         = now
            };
            ctx.SsActivoRotativoMovimiento.Add(movimiento);

            entity.ProyectoActualId = dto.NuevoProyectoId;
            entity.UpdatedAt        = now;

            await ctx.SaveChangesAsync();
            return entity;
        }
    }
}
