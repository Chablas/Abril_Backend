using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Repositories
{
    public class SolicitudSalidaRepository : ISolicitudSalidaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public SolicitudSalidaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<SolicitudSalidaFormDataDto> GetFormData()
        {
            using var ctx = _factory.CreateDbContext();

            var motivos = await ctx.GaMotivoSalida
                .Where(m => m.Activo)
                .OrderBy(m => m.Descripcion)
                .Select(m => new MotivoSalidaDto { Id = m.Id, Descripcion = m.Descripcion })
                .ToListAsync();

            var lugares = await (
                from l in ctx.GaLugar
                join p in ctx.Project on l.ProjectId equals p.ProjectId into pGroup
                from p in pGroup.DefaultIfEmpty()
                where l.Activo
                orderby l.Orden
                select new LugarSalidaDto
                {
                    Id            = l.Id,
                    NombreDisplay = l.Tipo == "proyecto" ? (p != null ? p.ProjectDescription : "[Sin proyecto]")
                                  : l.Tipo == "libre"    ? "Otro lugar"
                                  : l.Nombre ?? string.Empty,
                    EsLibre       = l.Tipo == "libre"
                }
            ).ToListAsync();

            return new SolicitudSalidaFormDataDto
            {
                Motivos = motivos,
                Lugares = lugares
            };
        }

        public async Task<List<SolicitudSalidaListItemDto>> GetByUserId(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => (int?)w.Id)
                .FirstOrDefaultAsync();

            if (workerId == null) return new();

            return await (
                from s  in ctx.GaSolicitudSalida
                join m  in ctx.GaMotivoSalida on s.MotivoId          equals m.Id into mGroup
                from m  in mGroup.DefaultIfEmpty()
                join lo in ctx.GaLugar        on s.LugarOrigenId     equals lo.Id into loGroup
                from lo in loGroup.DefaultIfEmpty()
                join po in ctx.Project        on lo.ProjectId        equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar        on s.LugarDestinoId    equals ld.Id into ldGroup
                from ld in ldGroup.DefaultIfEmpty()
                join pd in ctx.Project        on ld.ProjectId        equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                where s.WorkerId == workerId.Value
                orderby s.CreatedAt descending
                select new SolicitudSalidaListItemDto
                {
                    Id           = s.Id,
                    FechaSalida  = s.FechaSalida,
                    HoraSalida   = s.HoraSalida,
                    HoraRetorno  = s.HoraRetorno,
                    Motivo       = m != null ? m.Descripcion : (s.MotivoLibre ?? string.Empty),
                    LugarOrigen  = lo == null ? s.LugarOrigenLibre
                                 : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                 : lo.Nombre,
                    LugarDestino = ld == null ? s.LugarDestinoLibre
                                 : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                 : ld.Nombre,
                    Estado       = s.Estado,
                    CreatedAt    = s.CreatedAt
                }
            ).ToListAsync();
        }

        public async Task<(int Id, Worker Solicitante)> Create(SolicitudSalidaCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var solicitante = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No se encontró un trabajador asociado a su usuario.", 404);

            if (dto.MotivoId.HasValue)
            {
                _ = await ctx.GaMotivoSalida.FirstOrDefaultAsync(m => m.Id == dto.MotivoId.Value)
                    ?? throw new AbrilException("Motivo no encontrado.", 404);
            }

            var ent = new GaSolicitudSalida
            {
                WorkerId        = solicitante.Id,
                FechaSalida     = dto.FechaSalida,
                HoraSalida      = dto.HoraSalida,
                HoraRetorno     = dto.HoraRetorno,
                MotivoId        = dto.MotivoId,
                MotivoLibre     = string.IsNullOrWhiteSpace(dto.MotivoLibre) ? null : dto.MotivoLibre.Trim(),
                LugarOrigenId   = dto.LugarOrigenId,
                LugarOrigenLibre = string.IsNullOrWhiteSpace(dto.LugarOrigenLibre) ? null : dto.LugarOrigenLibre.Trim(),
                LugarDestinoId  = dto.LugarDestinoId,
                LugarDestinoLibre = string.IsNullOrWhiteSpace(dto.LugarDestinoLibre) ? null : dto.LugarDestinoLibre.Trim(),
                Estado          = "Pendiente",
                RegistradoPorId = userId,
                CreatedAt       = DateTimeOffset.UtcNow,
                UpdatedAt       = DateTimeOffset.UtcNow
            };
            ctx.GaSolicitudSalida.Add(ent);
            await ctx.SaveChangesAsync();
            return (ent.Id, solicitante);
        }

        public async Task SetAprobadorEmail(int solicitudId, string aprobadorEmail)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == solicitudId);
            if (s == null) return;
            s.AprobadorEmail = aprobadorEmail;
            s.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<GaSolicitudSalida?> Aprobar(int solicitudId)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == solicitudId);
            if (s == null || s.Estado != "Pendiente") return null;
            s.Estado        = "Aprobado";
            s.FechaDecision = DateTimeOffset.UtcNow;
            s.UpdatedAt     = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
            return s;
        }

        public async Task<GaSolicitudSalida?> Rechazar(int solicitudId, string? motivoRechazo)
        {
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.GaSolicitudSalida.FirstOrDefaultAsync(x => x.Id == solicitudId);
            if (s == null || s.Estado != "Pendiente") return null;
            s.Estado        = "Rechazado";
            s.MotivoRechazo = string.IsNullOrWhiteSpace(motivoRechazo) ? null : motivoRechazo.Trim();
            s.FechaDecision = DateTimeOffset.UtcNow;
            s.UpdatedAt     = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
            return s;
        }
    }
}
