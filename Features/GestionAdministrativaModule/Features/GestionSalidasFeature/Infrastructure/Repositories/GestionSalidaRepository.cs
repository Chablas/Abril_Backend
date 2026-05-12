using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Repositories
{
    public class GestionSalidaRepository : IGestionSalidaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public GestionSalidaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            var query =
                from s  in ctx.GaSolicitudSalida
                join w  in ctx.Worker        on s.WorkerId         equals w.Id
                join per in ctx.Person       on w.PersonId         equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join m  in ctx.GaMotivoSalida on s.MotivoId        equals m.Id
                join lo in ctx.GaLugar       on s.LugarOrigenId    equals lo.Id into loGroup
                from lo in loGroup.DefaultIfEmpty()
                join po in ctx.Project       on lo.ProjectId        equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar       on s.LugarDestinoId   equals ld.Id into ldGroup
                from ld in ldGroup.DefaultIfEmpty()
                join pd in ctx.Project       on ld.ProjectId        equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                select new { s, w, per, m, lo, po, ld, pd };

            if (filters.WorkerId.HasValue)
                query = query.Where(x => x.w.Id == filters.WorkerId.Value);

            if (filters.LugarProyectoId.HasValue)
                query = query.Where(x =>
                    x.s.LugarOrigenId  == filters.LugarProyectoId.Value ||
                    x.s.LugarDestinoId == filters.LugarProyectoId.Value);

            return await query
                // Pendientes primero, luego por fecha de creación desc
                .OrderBy(x  => x.s.Estado == "Pendiente" ? 0 : 1)
                .ThenByDescending(x => x.s.CreatedAt)
                .Select(x => new GestionSalidaListItemDto
                {
                    Id           = x.s.Id,
                    WorkerId     = x.w.Id,
                    Trabajador   = x.per != null ? (x.per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                    FechaSalida  = x.s.FechaSalida,
                    HoraSalida   = x.s.HoraSalida,
                    HoraRetorno  = x.s.HoraRetorno,
                    Motivo       = x.m.Descripcion,
                    LugarOrigen  = x.lo == null ? x.s.LugarOrigenLibre
                                 : x.lo.Tipo == "proyecto" ? (x.po != null ? x.po.ProjectDescription : "[Sin proyecto]")
                                 : x.lo.Nombre,
                    LugarDestino = x.ld == null ? x.s.LugarDestinoLibre
                                 : x.ld.Tipo == "proyecto" ? (x.pd != null ? x.pd.ProjectDescription : "[Sin proyecto]")
                                 : x.ld.Nombre,
                    Estado    = x.s.Estado,
                    CreatedAt = x.s.CreatedAt,
                })
                .ToListAsync();
        }

        public async Task<GestionSalidaFilterDataDto> GetFilterData()
        {
            using var ctx = _factory.CreateDbContext();

            // Trabajadores que tienen al menos una solicitud registrada
            var workerIds = await ctx.GaSolicitudSalida
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            var trabajadores = await (
                from w   in ctx.Worker
                where workerIds.Contains(w.Id)
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                orderby per != null ? per.FullName : null
                select new TrabajadorOptionDto
                {
                    WorkerId       = w.Id,
                    NombreCompleto = per != null ? (per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                }
            ).ToListAsync();

            // Lugares de tipo proyecto activos (para filtrar por destino/origen)
            var lugaresProyecto = await (
                from g in ctx.GaLugar
                join p in ctx.Project on g.ProjectId equals p.ProjectId
                where g.Tipo == "proyecto" && g.Activo
                orderby p.ProjectDescription
                select new LugarProyectoOptionDto
                {
                    GaLugarId    = g.Id,
                    NombreDisplay = p.ProjectDescription,
                }
            ).ToListAsync();

            return new GestionSalidaFilterDataDto
            {
                Trabajadores    = trabajadores,
                LugaresProyecto = lugaresProyecto,
            };
        }

        public async Task Aprobar(int id, int reviewerUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new AbrilException("Solicitud no encontrada.", 404);

            if (s.Estado != "Pendiente")
                throw new AbrilException("Solo se pueden aprobar solicitudes en estado Pendiente.", 400);

            s.Estado    = "Aprobado";
            s.UpdatedAt = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
        }

        public async Task Rechazar(int id, int reviewerUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new AbrilException("Solicitud no encontrada.", 404);

            if (s.Estado != "Pendiente")
                throw new AbrilException("Solo se pueden rechazar solicitudes en estado Pendiente.", 400);

            s.Estado    = "Rechazado";
            s.UpdatedAt = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
        }
    }
}
