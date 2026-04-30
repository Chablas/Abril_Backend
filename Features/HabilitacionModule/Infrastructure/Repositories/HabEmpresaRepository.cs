using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.HabEmpresa;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class HabEmpresaRepository : IHabEmpresaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public HabEmpresaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<EmpresaEntregableDto>> GetEntregablesEmpresaAsync(
            int empresaId, int proyectoId, int? mes, int? anio)
        {
            using var ctx = _factory.CreateDbContext();

            var items = await ctx.SsItemEmpresa
                .Where(i => i.Activo)
                .OrderBy(i => i.Orden)
                .ToListAsync();

            var itemIds = items.Select(i => i.Id).ToList();

            var existentesQuery = ctx.SsHabEmpresa
                .Where(h => h.EmpresaId == empresaId && h.ProyectoId == proyectoId && itemIds.Contains(h.ItemId));

            if (mes.HasValue) existentesQuery = existentesQuery.Where(h => h.Mes == mes.Value);
            if (anio.HasValue) existentesQuery = existentesQuery.Where(h => h.Anio == anio.Value);

            var existentes = await existentesQuery.ToListAsync();

            var faltantes = items
                .Where(i => !existentes.Any(h => h.ItemId == i.Id))
                .Select(i => new SsHabEmpresa
                {
                    EmpresaId = empresaId,
                    ProyectoId = proyectoId,
                    ItemId = i.Id,
                    Mes = mes,
                    Anio = anio,
                    Estado = "Falta",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            if (faltantes.Count > 0)
            {
                ctx.SsHabEmpresa.AddRange(faltantes);
                await ctx.SaveChangesAsync();
                existentes.AddRange(faltantes);
            }

            var itemMap = items.ToDictionary(i => i.Id);

            return existentes
                .Where(h => itemMap.ContainsKey(h.ItemId))
                .Select(h =>
                {
                    var item = itemMap[h.ItemId];
                    return new EmpresaEntregableDto
                    {
                        Id = h.Id,
                        ItemId = h.ItemId,
                        NombreItem = item.Nombre,
                        Estado = h.Estado,
                        Vigencia = h.Vigencia,
                        ArchivoUrl = h.ArchivoUrl,
                        ObsAbril = h.ObsAbril,
                        ObsContratista = h.ObsContratista,
                        RequiereVigencia = item.RequiereVigencia,
                        Responsable = item.Responsable,
                        Mes = h.Mes,
                        Anio = h.Anio
                    };
                })
                .OrderBy(d => itemMap[d.ItemId].Orden)
                .ToList();
        }

        public async Task<SsHabEmpresa> UpdateEntregableEmpresaAsync(
            int id, EmpresaEntregableUpdateDto dto, int? userId, int? empresaId = null)
        {
            using var ctx = _factory.CreateDbContext();

            var entregable = await ctx.SsHabEmpresa.FirstOrDefaultAsync(h => h.Id == id)
                ?? throw new AbrilException("Entregable no encontrado.", 404);

            if (!string.IsNullOrWhiteSpace(dto.ArchivoUrl) && dto.ArchivoUrl != entregable.ArchivoUrl)
            {
                var versionActual = await ctx.SsHabDocumentoVersion
                    .CountAsync(v => v.HabEmpresaId == id);

                ctx.SsHabDocumentoVersion.Add(new SsHabDocumentoVersion
                {
                    HabEmpresaId = id,
                    Version = versionActual + 1,
                    ArchivoUrl = dto.ArchivoUrl,
                    SubidoPorUserId = userId,
                    SubidoPorEmpresaId = empresaId,
                    EstadoAlSubir = dto.Estado,
                    CreatedAt = DateTime.UtcNow
                });
            }

            entregable.Estado = dto.Estado;
            entregable.Vigencia = HabilitacionDateHelper.AsUtc(dto.Vigencia);
            entregable.ArchivoUrl = dto.ArchivoUrl;
            entregable.ObsAbril = dto.ObsAbril;
            entregable.ObsContratista = dto.ObsContratista;
            entregable.Mes = dto.Mes;
            entregable.Anio = dto.Anio;
            entregable.UpdatedAt = DateTime.UtcNow;

            if (string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
            {
                entregable.AprobadoPor = userId;
                entregable.FechaAprobacion = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
            return entregable;
        }

        public async Task InicializarEntregablesEmpresaAsync(int empresaId, int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var items = await ctx.SsItemEmpresa
                .Where(i => i.Activo)
                .ToListAsync();

            var existentesIds = await ctx.SsHabEmpresa
                .Where(h => h.EmpresaId == empresaId && h.ProyectoId == proyectoId)
                .Select(h => h.ItemId)
                .ToListAsync();

            var faltantes = items
                .Where(i => !existentesIds.Contains(i.Id))
                .Select(i => new SsHabEmpresa
                {
                    EmpresaId = empresaId,
                    ProyectoId = proyectoId,
                    ItemId = i.Id,
                    Estado = "Falta",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            if (faltantes.Count > 0)
            {
                ctx.SsHabEmpresa.AddRange(faltantes);
                await ctx.SaveChangesAsync();
            }
        }
    }
}
