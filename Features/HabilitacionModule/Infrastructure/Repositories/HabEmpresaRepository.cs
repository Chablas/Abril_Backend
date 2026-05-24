using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.HabEmpresa;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
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

            var query = ctx.SsHabEmpresa
                .Where(h => h.EmpresaId == empresaId && h.ProyectoId == proyectoId);

            if (mes.HasValue) query = query.Where(h => h.Mes == mes.Value);
            if (anio.HasValue) query = query.Where(h => h.Anio == anio.Value);

            var registros = await query.ToListAsync();
            if (registros.Count == 0) return [];

            var itemIds = registros.Select(h => h.ItemId).Distinct().ToList();
            var itemMap = await ctx.SsItemEmpresa
                .Where(i => itemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id);

            return registros
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

            var entregable = await ctx.SsHabEmpresa
                .Include(h => h.Item)
                .FirstOrDefaultAsync(h => h.Id == id)
                ?? throw new AbrilException("Entregable no encontrado.", 404);

            if (!string.IsNullOrWhiteSpace(dto.ArchivoUrl) && dto.ArchivoUrl != entregable.ArchivoUrl)
            {
                int? ssEmpresaId = empresaId;

                var versionActual = await ctx.SsHabDocumentoVersion
                    .CountAsync(v => v.HabEmpresaId == id);

                ctx.SsHabDocumentoVersion.Add(new SsHabDocumentoVersion
                {
                    HabEmpresaId = id,
                    Version = versionActual + 1,
                    ArchivoUrl = dto.ArchivoUrl,
                    SubidoPorUserId = userId,
                    SubidoPorEmpresaId = ssEmpresaId,
                    EstadoAlSubir = dto.Estado,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (!string.IsNullOrEmpty(dto.Estado))
                entregable.Estado = dto.Estado;
            if (!string.IsNullOrEmpty(dto.Estado) || dto.Vigencia.HasValue)
                entregable.Vigencia = HabilitacionDateHelper.ResolverVigencia(entregable.Item?.RequiereVigencia ?? true, entregable.Estado, dto.Vigencia);
            if (dto.ArchivoUrl is not null) entregable.ArchivoUrl = dto.ArchivoUrl;
            if (dto.ObsAbril is not null) entregable.ObsAbril = dto.ObsAbril;
            if (dto.ObsContratista is not null) entregable.ObsContratista = dto.ObsContratista;
            if (dto.Mes is not null) entregable.Mes = dto.Mes;
            if (dto.Anio is not null) entregable.Anio = dto.Anio;
            entregable.UpdatedAt = DateTime.UtcNow;

            if (string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
            {
                entregable.AprobadoPor = userId;
                entregable.FechaAprobacion = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
            return entregable;
        }

        public async Task<List<SsHabDocumentoVersionDto>> GetVersionesDocumentoEmpresaAsync(int empresaId, int itemId)
        {
            using var ctx = _factory.CreateDbContext();

            var habEmpresaIds = await ctx.SsHabEmpresa
                .Where(h => h.EmpresaId == empresaId && h.ItemId == itemId)
                .Select(h => h.Id)
                .ToListAsync();

            if (habEmpresaIds.Count == 0) return [];

            var versiones = await ctx.SsHabDocumentoVersion
                .Where(v => v.HabEmpresaId.HasValue && habEmpresaIds.Contains(v.HabEmpresaId.Value))
                .OrderByDescending(v => v.Version)
                .ToListAsync();

            var userIds = versiones
                .Where(v => v.SubidoPorUserId.HasValue)
                .Select(v => v.SubidoPorUserId!.Value)
                .Distinct()
                .ToList();

            var nombresPorUserId = new Dictionary<int, string?>();
            if (userIds.Count > 0)
            {
                var users = await (
                    from u in ctx.User
                    join p in ctx.Person on u.UserId equals p.UserId
                    where userIds.Contains(u.UserId)
                    select new { u.UserId, p.FullName }
                  ).ToListAsync();

                foreach (var x in users)
                    nombresPorUserId[x.UserId] = x.FullName;
            }

            return versiones.Select(v => new SsHabDocumentoVersionDto
            {
                Id = v.Id,
                HabTrabajadorId = v.HabTrabajadorId,
                Version = v.Version,
                ArchivoUrl = v.ArchivoUrl,
                SubidoPorUserId = v.SubidoPorUserId,
                SubidoPorNombre = v.SubidoPorUserId.HasValue && nombresPorUserId.TryGetValue(v.SubidoPorUserId.Value, out var nombre)
                    ? nombre
                    : null,
                SubidoPorEmpresaId = v.SubidoPorEmpresaId,
                EstadoAlSubir = v.EstadoAlSubir,
                EstadoAnterior = v.EstadoAnterior,
                ProyectoId = v.ProyectoId,
                EmpresaId = v.EmpresaId,
                AprobadoPorUserId = v.AprobadoPorUserId,
                MotivoRechazo = v.MotivoRechazo,
                CreatedAt = v.CreatedAt
            }).ToList();
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

        public async Task ActivarProyectoAsync(int empresaId, int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var proyectoExiste = await ctx.Project.AnyAsync(p => p.ProjectId == proyectoId);
            if (!proyectoExiste)
                throw new AbrilException("Proyecto no encontrado.", 404);

            var yaActiva = await ctx.SsEmpresaProyecto
                .AnyAsync(ep => ep.EmpresaId == empresaId && ep.ProyectoId == proyectoId && ep.Activo);
            if (yaActiva)
                throw new AbrilException("La empresa ya está activa en este proyecto.", 409);

            ctx.SsEmpresaProyecto.Add(new SsEmpresaProyecto
            {
                EmpresaId = empresaId,
                ProyectoId = proyectoId,
                Activo = true,
                FechaInicio = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            await InicializarEntregablesEmpresaAsync(empresaId, proyectoId);
        }

        public async Task<List<ProyectoDisponibleDto>> GetProyectosDisponiblesAsync(int empresaId)
        {
            using var ctx = _factory.CreateDbContext();

            var proyectos = await ctx.Project
                .Where(p => p.State)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new { p.ProjectId, p.ProjectDescription })
                .ToListAsync();

            var activas = await ctx.SsEmpresaProyecto
                .Where(ep => ep.EmpresaId == empresaId && ep.Activo)
                .Select(ep => new { ep.ProyectoId, ep.FechaInicio })
                .ToListAsync();

            var activasMap = activas.ToDictionary(ep => ep.ProyectoId, ep => ep.FechaInicio);

            return proyectos.Select(p => new ProyectoDisponibleDto
            {
                Id = p.ProjectId,
                Nombre = p.ProjectDescription,
                EstaActiva = activasMap.ContainsKey(p.ProjectId),
                FechaInicio = activasMap.TryGetValue(p.ProjectId, out var fi) && fi.HasValue
                    ? DateOnly.FromDateTime(fi.Value)
                    : null
            }).ToList();
        }

        public async Task DesactivarProyectoAsync(int empresaId, int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var registro = await ctx.SsEmpresaProyecto
                .FirstOrDefaultAsync(ep => ep.EmpresaId == empresaId && ep.ProyectoId == proyectoId && ep.Activo)
                ?? throw new AbrilException("No existe una activación activa para esa empresa y proyecto.", 404);

            registro.Activo = false;
            registro.FechaFin = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

    }
}
