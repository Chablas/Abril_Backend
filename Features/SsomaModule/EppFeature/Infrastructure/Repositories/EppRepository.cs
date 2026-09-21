using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Repositories
{
    public class EppRepository : IEppRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EppRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ─────────────────────────────────────────────────────────────────
        // CATEGORÍAS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<EppCategoriaDto>> GetCategoriasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsEppCategoria
                .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
                .Select(c => new EppCategoriaDto
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Orden = c.Orden,
                    Activo = c.Activo,
                    TotalItems = c.Familias.SelectMany(f => f.Items).Count(i => i.Activo)
                })
                .ToListAsync();
        }

        public async Task<SsEppCategoria> CreateCategoriaAsync(EppCategoriaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entity = new SsEppCategoria
            {
                Nombre = dto.Nombre,
                Orden = dto.Orden,
                Activo = true,
                CreatedAt = now
            };
            ctx.SsEppCategoria.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateCategoriaAsync(int categoriaId, EppCategoriaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppCategoria.FindAsync(categoriaId)
                ?? throw new KeyNotFoundException($"Categoría {categoriaId} no encontrada.");

            entity.Nombre = dto.Nombre;
            entity.Orden = dto.Orden;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task SetCategoriaActivoAsync(int categoriaId, bool activo)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppCategoria.FindAsync(categoriaId)
                ?? throw new KeyNotFoundException($"Categoría {categoriaId} no encontrada.");

            entity.Activo = activo;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // Solo se puede borrar una categoría que no tenga ninguna familia — evita borrar
        // en cascada data que alguien ya cargó por error.
        public async Task DeleteCategoriaAsync(int categoriaId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppCategoria.FindAsync(categoriaId)
                ?? throw new KeyNotFoundException($"Categoría {categoriaId} no encontrada.");

            if (await ctx.SsEppFamilia.AnyAsync(f => f.CategoriaId == categoriaId))
                throw new InvalidOperationException("No se puede eliminar: la categoría tiene familias registradas.");

            ctx.SsEppCategoria.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // FAMILIAS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<EppFamiliaDto>> GetFamiliasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsEppFamilia
                .Include(f => f.Categoria)
                .OrderBy(f => f.Categoria!.Orden).ThenBy(f => f.Orden).ThenBy(f => f.Nombre)
                .Select(f => new EppFamiliaDto
                {
                    Id = f.Id,
                    Nombre = f.Nombre,
                    CategoriaId = f.CategoriaId,
                    CategoriaNombre = f.Categoria != null ? f.Categoria.Nombre : "",
                    Orden = f.Orden,
                    Activo = f.Activo,
                    TotalItems = f.Items.Count(i => i.Activo)
                })
                .ToListAsync();
        }

        public async Task<SsEppFamilia> CreateFamiliaAsync(EppFamiliaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            if (!await ctx.SsEppCategoria.AnyAsync(c => c.Id == dto.CategoriaId))
                throw new KeyNotFoundException($"Categoría {dto.CategoriaId} no encontrada.");

            var now = DateTimeOffset.UtcNow;
            var entity = new SsEppFamilia
            {
                Nombre = dto.Nombre,
                CategoriaId = dto.CategoriaId,
                Orden = dto.Orden,
                Activo = true,
                CreatedAt = now
            };
            ctx.SsEppFamilia.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateFamiliaAsync(int familiaId, EppFamiliaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppFamilia.FindAsync(familiaId)
                ?? throw new KeyNotFoundException($"Familia {familiaId} no encontrada.");

            if (!await ctx.SsEppCategoria.AnyAsync(c => c.Id == dto.CategoriaId))
                throw new KeyNotFoundException($"Categoría {dto.CategoriaId} no encontrada.");

            entity.Nombre = dto.Nombre;
            entity.CategoriaId = dto.CategoriaId;
            entity.Orden = dto.Orden;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task SetFamiliaActivoAsync(int familiaId, bool activo)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppFamilia.FindAsync(familiaId)
                ?? throw new KeyNotFoundException($"Familia {familiaId} no encontrada.");

            entity.Activo = activo;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // Solo se puede borrar una familia que no tenga ningún ítem — evita borrar en
        // cascada fichas técnicas que alguien ya cargó por error.
        public async Task DeleteFamiliaAsync(int familiaId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppFamilia.FindAsync(familiaId)
                ?? throw new KeyNotFoundException($"Familia {familiaId} no encontrada.");

            if (await ctx.SsEppItem.AnyAsync(i => i.FamiliaId == familiaId))
                throw new InvalidOperationException("No se puede eliminar: la familia tiene ítems registrados.");

            ctx.SsEppFamilia.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // ÍTEMS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<EppItemListDto>> GetItemsAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsEppItem
                .Include(i => i.Familia!).ThenInclude(f => f.Categoria)
                .OrderBy(i => i.NombreTecnico)
                .Select(i => new EppItemListDto
                {
                    Id = i.Id,
                    NombreTecnico = i.NombreTecnico,
                    NombreComercial = i.NombreComercial,
                    FamiliaId = i.FamiliaId,
                    FamiliaNombre = i.Familia != null ? i.Familia.Nombre : "",
                    CategoriaId = i.Familia != null ? i.Familia.CategoriaId : 0,
                    CategoriaNombre = i.Familia != null && i.Familia.Categoria != null ? i.Familia.Categoria.Nombre : "",
                    ImagenUrl = i.ImagenUrl,
                    FichaTecnicaUrl = i.FichaTecnicaUrl,
                    FichaTecnicaNombreArchivo = i.FichaTecnicaNombreArchivo,
                    Activo = i.Activo,
                    TotalModelos = i.Modelos.Count(m => m.Activo),
                    Modelos = i.Modelos.Where(m => m.Activo).OrderBy(m => m.Marca).ThenBy(m => m.Modelo).Select(m => new EppModeloDto
                    {
                        Id = m.Id,
                        Marca = m.Marca,
                        Modelo = m.Modelo,
                        CodigoReferencia = m.CodigoReferencia,
                        ImagenUrl = m.ImagenUrl,
                        Activo = m.Activo
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task<EppItemDetalleDto?> GetItemDetalleAsync(int itemId)
        {
            using var ctx = _factory.CreateDbContext();
            var item = await ctx.SsEppItem
                .Include(i => i.Familia!).ThenInclude(f => f.Categoria)
                .Include(i => i.Modelos)
                .FirstOrDefaultAsync(i => i.Id == itemId);

            if (item == null) return null;

            var auditoria = await ctx.SsEppAuditoria
                .Where(a => a.EntidadTipo == "Item" && a.EntidadId == itemId)
                .OrderByDescending(a => a.Fecha)
                .ToListAsync();

            var modeloIds = item.Modelos.Select(m => m.Id).ToList();
            var auditoriaModelos = await ctx.SsEppAuditoria
                .Where(a => a.EntidadTipo == "Modelo" && modeloIds.Contains(a.EntidadId))
                .OrderByDescending(a => a.Fecha)
                .ToListAsync();

            var todaAuditoria = auditoria.Concat(auditoriaModelos).OrderByDescending(a => a.Fecha).ToList();
            var usuarioIds = todaAuditoria.Where(a => a.UsuarioId.HasValue).Select(a => a.UsuarioId!.Value).Distinct().ToList();
            var usuarios = await ctx.User
                .Where(u => usuarioIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Person.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            return new EppItemDetalleDto
            {
                Id = item.Id,
                NombreTecnico = item.NombreTecnico,
                NombreComercial = item.NombreComercial,
                FamiliaId = item.FamiliaId,
                FamiliaNombre = item.Familia?.Nombre ?? "",
                CategoriaId = item.Familia?.CategoriaId ?? 0,
                CategoriaNombre = item.Familia?.Categoria?.Nombre ?? "",
                ImagenUrl = item.ImagenUrl,
                FichaTecnicaUrl = item.FichaTecnicaUrl,
                FichaTecnicaNombreArchivo = item.FichaTecnicaNombreArchivo,
                Activo = item.Activo,
                Descripcion = item.Descripcion,
                TotalModelos = item.Modelos.Count(m => m.Activo),
                Modelos = item.Modelos.OrderBy(m => m.Marca).ThenBy(m => m.Modelo).Select(m => new EppModeloDto
                {
                    Id = m.Id,
                    Marca = m.Marca,
                    Modelo = m.Modelo,
                    CodigoReferencia = m.CodigoReferencia,
                    ImagenUrl = m.ImagenUrl,
                    Activo = m.Activo
                }).ToList(),
                Auditoria = todaAuditoria.Select(a => new EppAuditoriaDto
                {
                    EntidadTipo = a.EntidadTipo,
                    Accion = a.Accion,
                    Detalle = a.Detalle,
                    UsuarioNombre = a.UsuarioId.HasValue && usuarios.TryGetValue(a.UsuarioId.Value, out var nombre) ? nombre : null,
                    Fecha = a.Fecha
                }).ToList()
            };
        }

        public async Task<SsEppItem> CreateItemAsync(EppItemUpsertDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            if (!await ctx.SsEppFamilia.AnyAsync(f => f.Id == dto.FamiliaId))
                throw new KeyNotFoundException($"Familia {dto.FamiliaId} no encontrada.");

            var now = DateTimeOffset.UtcNow;
            var entity = new SsEppItem
            {
                NombreTecnico = dto.NombreTecnico,
                NombreComercial = dto.NombreComercial,
                FamiliaId = dto.FamiliaId,
                Descripcion = dto.Descripcion,
                Activo = true,
                CreatedAt = now,
                CreatedById = userId
            };
            ctx.SsEppItem.Add(entity);
            await ctx.SaveChangesAsync();

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Item",
                EntidadId = entity.Id,
                Accion = "Creado",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateItemAsync(int itemId, EppItemUpsertDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppItem.FindAsync(itemId)
                ?? throw new KeyNotFoundException($"Ítem {itemId} no encontrado.");

            if (!await ctx.SsEppFamilia.AnyAsync(f => f.Id == dto.FamiliaId))
                throw new KeyNotFoundException($"Familia {dto.FamiliaId} no encontrada.");

            var now = DateTimeOffset.UtcNow;
            entity.NombreTecnico = dto.NombreTecnico;
            entity.NombreComercial = dto.NombreComercial;
            entity.FamiliaId = dto.FamiliaId;
            entity.Descripcion = dto.Descripcion;
            entity.UpdatedAt = now;
            entity.UpdatedById = userId;

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Item",
                EntidadId = itemId,
                Accion = "Editado",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
        }

        public async Task SetItemActivoAsync(int itemId, bool activo, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppItem.FindAsync(itemId)
                ?? throw new KeyNotFoundException($"Ítem {itemId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            entity.Activo = activo;
            entity.UpdatedAt = now;
            entity.UpdatedById = userId;

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Item",
                EntidadId = itemId,
                Accion = activo ? "Activado" : "Desactivado",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
        }

        public async Task SetItemImagenAsync(int itemId, string imagenUrl, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppItem.FindAsync(itemId)
                ?? throw new KeyNotFoundException($"Ítem {itemId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            entity.ImagenUrl = imagenUrl;
            entity.UpdatedAt = now;
            entity.UpdatedById = userId;

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Item",
                EntidadId = itemId,
                Accion = "Editado",
                Detalle = "Imagen actualizada",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
        }

        public async Task SetItemFichaTecnicaAsync(int itemId, string fichaTecnicaUrl, string nombreArchivo, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppItem.FindAsync(itemId)
                ?? throw new KeyNotFoundException($"Ítem {itemId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            entity.FichaTecnicaUrl = fichaTecnicaUrl;
            entity.FichaTecnicaNombreArchivo = nombreArchivo;
            entity.UpdatedAt = now;
            entity.UpdatedById = userId;

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Item",
                EntidadId = itemId,
                Accion = "Editado",
                Detalle = "Ficha técnica actualizada",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
        }

        public async Task QuitarFichaTecnicaAsync(int itemId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppItem.FindAsync(itemId)
                ?? throw new KeyNotFoundException($"Ítem {itemId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            entity.FichaTecnicaUrl = null;
            entity.FichaTecnicaNombreArchivo = null;
            entity.UpdatedAt = now;
            entity.UpdatedById = userId;

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Item",
                EntidadId = itemId,
                Accion = "Editado",
                Detalle = "Ficha técnica eliminada",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // MODELOS
        // ─────────────────────────────────────────────────────────────────

        public async Task<SsEppModelo> CreateModeloAsync(int itemId, EppModeloUpsertDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            if (!await ctx.SsEppItem.AnyAsync(i => i.Id == itemId))
                throw new KeyNotFoundException($"Ítem {itemId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            var entity = new SsEppModelo
            {
                EppItemId = itemId,
                Marca = dto.Marca,
                Modelo = dto.Modelo,
                CodigoReferencia = dto.CodigoReferencia,
                Activo = true,
                CreatedAt = now,
                CreatedById = userId
            };
            ctx.SsEppModelo.Add(entity);
            await ctx.SaveChangesAsync();

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Modelo",
                EntidadId = entity.Id,
                Accion = "Creado",
                Detalle = $"{dto.Marca} — {dto.Modelo}",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateModeloAsync(int modeloId, EppModeloUpsertDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppModelo.FindAsync(modeloId)
                ?? throw new KeyNotFoundException($"Modelo {modeloId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            entity.Marca = dto.Marca;
            entity.Modelo = dto.Modelo;
            entity.CodigoReferencia = dto.CodigoReferencia;
            entity.UpdatedAt = now;
            entity.UpdatedById = userId;

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Modelo",
                EntidadId = modeloId,
                Accion = "Editado",
                Detalle = $"{dto.Marca} — {dto.Modelo}",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
        }

        public async Task SetModeloActivoAsync(int modeloId, bool activo, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppModelo.FindAsync(modeloId)
                ?? throw new KeyNotFoundException($"Modelo {modeloId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            entity.Activo = activo;
            entity.UpdatedAt = now;
            entity.UpdatedById = userId;

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Modelo",
                EntidadId = modeloId,
                Accion = activo ? "Activado" : "Desactivado",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
        }

        public async Task SetModeloImagenAsync(int modeloId, string imagenUrl, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsEppModelo.FindAsync(modeloId)
                ?? throw new KeyNotFoundException($"Modelo {modeloId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            entity.ImagenUrl = imagenUrl;
            entity.UpdatedAt = now;
            entity.UpdatedById = userId;

            ctx.SsEppAuditoria.Add(new SsEppAuditoria
            {
                EntidadTipo = "Modelo",
                EntidadId = modeloId,
                Accion = "Editado",
                Detalle = "Imagen actualizada",
                UsuarioId = userId,
                Fecha = now
            });
            await ctx.SaveChangesAsync();
        }
    }
}
