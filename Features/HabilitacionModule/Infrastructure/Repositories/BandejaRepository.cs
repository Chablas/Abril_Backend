using Abril_Backend.Features.Habilitacion.Application.Dtos.Bandeja;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.DTOs;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Text;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class BandejaRepository : IBandejaRepository
    {
        static BandejaRepository()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration _configuration;

        public BandejaRepository(IDbContextFactory<AppDbContext> factory, IConfiguration configuration)
        {
            _factory = factory;
            _configuration = configuration;
        }

        private IDbConnection CreateConnection()
            => new NpgsqlConnection(_configuration["Database:PostgreSQL"]);

        private const string SelectBase = @"
SELECT
    ht.id, 'TRABAJADOR' as tipo,
    i.nombre as nombre_entregable,
    w.apellido_nombre as entidad_nombre,
    ec.razon_social as empresa_nombre,
    p.nombre as proyecto_nombre,
    p.id as proyecto_id,
    ht.estado,
    ht.vigencia,
    ht.archivo_url,
    ht.obs_contratista,
    i.responsable,
    ht.updated_at as fecha_envio
FROM ss_hab_trabajador ht
JOIN ss_item_trabajador i ON i.id = ht.item_id
JOIN workers w ON w.id = ht.worker_id
LEFT JOIN worker_vinculaciones wv ON wv.worker_id = w.id AND wv.fecha_fin IS NULL
LEFT JOIN ss_empresa_contratista ec ON ec.id = wv.empresa_id
LEFT JOIN projects p ON p.id = wv.proyecto_id
WHERE ht.estado = 'Enviado'
  AND (@ProyectoId IS NULL OR wv.proyecto_id = @ProyectoId)
  AND (@EmpresaId IS NULL OR wv.empresa_id = @EmpresaId)
  AND (@Responsable IS NULL OR i.responsable = @Responsable)
  AND (@Tipo IS NULL OR @Tipo = 'TRABAJADOR')

UNION ALL

SELECT
    he.id, 'EMPRESA' as tipo,
    i.nombre as nombre_entregable,
    ec.razon_social as entidad_nombre,
    ec.razon_social as empresa_nombre,
    p.nombre as proyecto_nombre,
    he.proyecto_id,
    he.estado,
    he.vigencia,
    he.archivo_url,
    he.obs_contratista,
    i.responsable,
    he.updated_at as fecha_envio
FROM ss_hab_empresa he
JOIN ss_item_empresa i ON i.id = he.item_id
JOIN ss_empresa_contratista ec ON ec.id = he.empresa_id
JOIN projects p ON p.id = he.proyecto_id
WHERE he.estado = 'Enviado'
  AND (@ProyectoId IS NULL OR he.proyecto_id = @ProyectoId)
  AND (@EmpresaId IS NULL OR he.empresa_id = @EmpresaId)
  AND (@Responsable IS NULL OR i.responsable = @Responsable)
  AND (@Tipo IS NULL OR @Tipo = 'EMPRESA')

UNION ALL

SELECT
    heq.id, 'EQUIPO' as tipo,
    i.nombre as nombre_entregable,
    CONCAT(eq.tipo, ' - ', eq.marca, ' ', eq.modelo) as entidad_nombre,
    ec.razon_social as empresa_nombre,
    p.nombre as proyecto_nombre,
    eq.proyecto_id,
    heq.estado,
    heq.vigencia,
    heq.archivo_url,
    NULL as obs_contratista,
    'SSOMA' as responsable,
    heq.updated_at as fecha_envio
FROM ss_hab_equipo heq
JOIN ss_item_equipo i ON i.id = heq.item_id
JOIN ss_equipo eq ON eq.id = heq.equipo_id
LEFT JOIN ss_empresa_contratista ec ON ec.id = eq.propietario_empresa_id
JOIN projects p ON p.id = eq.proyecto_id
WHERE heq.estado = 'Enviado'
  AND (@ProyectoId IS NULL OR eq.proyecto_id = @ProyectoId)
  AND (@EmpresaId IS NULL OR eq.propietario_empresa_id = @EmpresaId)
  AND (@Tipo IS NULL OR @Tipo = 'EQUIPO')
  AND (@Responsable IS NULL OR @Responsable = 'SSOMA')
";

        public async Task<(List<BandejaItemDto> Items, int Total)> GetPendientesAsync(
            string? tipo, int? proyectoId, int? empresaId,
            string? responsable, int page, int pageSize)
        {
            var parametros = new
            {
                Tipo = tipo,
                ProyectoId = proyectoId,
                EmpresaId = empresaId,
                Responsable = responsable,
                PageSize = pageSize,
                Offset = (page - 1) * pageSize
            };

            var dataSql = $@"SELECT * FROM ({SelectBase}) t
ORDER BY fecha_envio DESC NULLS LAST
LIMIT @PageSize OFFSET @Offset";

            var countSql = $"SELECT COUNT(*) FROM ({SelectBase}) t";

            using var conn = CreateConnection();
            var items = (await conn.QueryAsync<BandejaItemDto>(dataSql, parametros)).ToList();
            var total = await conn.ExecuteScalarAsync<int>(countSql, parametros);

            return (items, total);
        }

        public async Task<CursorPagedResult<BandejaItemDto>> GetPendientesCursorAsync(
            string? tipo, int? proyectoId, int? empresaId,
            string? responsable, string? cursor, int pageSize)
        {
            DateTime? cursorFecha = null;
            int? cursorId = null;
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                try
                {
                    var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
                    var parts = raw.Split('_', 2);
                    if (parts.Length == 2
                        && long.TryParse(parts[0], out var ticks)
                        && int.TryParse(parts[1], out var idVal))
                    {
                        cursorFecha = new DateTime(ticks, DateTimeKind.Utc);
                        cursorId = idVal;
                    }
                }
                catch { /* cursor inválido se ignora */ }
            }

            var parametros = new
            {
                Tipo = tipo,
                ProyectoId = proyectoId,
                EmpresaId = empresaId,
                Responsable = responsable,
                CursorFecha = cursorFecha,
                CursorId = cursorId,
                PageSize = pageSize + 1
            };

            var sql = $@"SELECT * FROM ({SelectBase}) t
WHERE (@CursorFecha IS NULL
       OR t.fecha_envio < @CursorFecha
       OR (t.fecha_envio = @CursorFecha AND t.id < @CursorId))
ORDER BY t.fecha_envio DESC NULLS LAST, t.id DESC
LIMIT @PageSize";

            var countSql = $"SELECT COUNT(*) FROM ({SelectBase}) t";

            using var conn = CreateConnection();
            var rows = (await conn.QueryAsync<BandejaItemDto>(sql, parametros)).ToList();
            var total = await conn.ExecuteScalarAsync<int>(countSql, new
            {
                Tipo = tipo,
                ProyectoId = proyectoId,
                EmpresaId = empresaId,
                Responsable = responsable
            });

            var hasMore = rows.Count > pageSize;
            var page = hasMore ? rows.Take(pageSize).ToList() : rows;

            string? nextCursor = null;
            if (hasMore && page.Count > 0)
            {
                var last = page[^1];
                var ticks = (last.FechaEnvio ?? DateTime.MinValue).Ticks;
                nextCursor = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{ticks}_{last.Id}"));
            }

            return new CursorPagedResult<BandejaItemDto>
            {
                Data = page,
                Total = total,
                NextCursor = nextCursor,
                HasMore = hasMore
            };
        }

        public async Task<SsHabTrabajador?> AprobarTrabajadorAsync(int id, BandejaAprobarDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsHabTrabajador
                .Include(h => h.Item)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (entity is null) return null;

            entity.Estado = dto.Estado;
            entity.ObsAbril = dto.ObsAbril;
            entity.Vigencia = ResolverVigencia(entity.Item?.RequiereVigencia ?? true, dto.Estado, dto.Vigencia);
            entity.AprobadoPor = userId;
            entity.FechaAprobacion = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task<SsHabEmpresa?> AprobarEmpresaAsync(int id, BandejaAprobarDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsHabEmpresa
                .Include(h => h.Item)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (entity is null) return null;

            entity.Estado = dto.Estado;
            entity.ObsAbril = dto.ObsAbril;
            entity.Vigencia = ResolverVigencia(entity.Item?.RequiereVigencia ?? true, dto.Estado, dto.Vigencia);
            entity.AprobadoPor = userId;
            entity.FechaAprobacion = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task<SsHabEquipo?> AprobarEquipoAsync(int id, BandejaAprobarDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsHabEquipo
                .Include(h => h.Item)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (entity is null) return null;

            entity.Estado = dto.Estado;
            entity.ObsAbril = dto.ObsAbril;
            entity.Vigencia = ResolverVigencia(entity.Item?.RequiereVigencia ?? true, dto.Estado, dto.Vigencia);
            entity.AprobadoPor = userId;
            entity.UpdatedAt = DateTime.UtcNow;

            await ctx.SaveChangesAsync();
            return entity;
        }

        private static DateTime? ResolverVigencia(bool requiereVigencia, string estado, DateTime? dtoVigencia)
        {
            if (!string.Equals(estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
                return dtoVigencia;
            return requiereVigencia
                ? dtoVigencia
                : new DateOnly(2040, 12, 31).ToDateTime(TimeOnly.MinValue);
        }
    }
}
