using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.RevisionDescansos;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class RevisionDescansosRepository : IRevisionDescansosRepository
    {
        private const int PageSize = 20;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ReactivosCacheVersion _reactivosCacheVersion;

        public RevisionDescansosRepository(IDbContextFactory<AppDbContext> factory, ReactivosCacheVersion reactivosCacheVersion)
        {
            _factory = factory;
            _reactivosCacheVersion = reactivosCacheVersion;
        }

        public async Task<RevisionDescansosInitDto> GetInit(RevisionDescansosFiltroDto filtro)
        {
            using var ctx = _factory.CreateDbContext();

            // Árbol completo de áreas (area_scope) para el filtro en cascada.
            var areaTree = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                orderby s.DisplayOrder
                select new RevisionAreaNodoDto
                {
                    AreaScopeId       = s.AreaScopeId,
                    AreaItemName      = ai.AreaItemName,
                    AreaScopeParentId = s.AreaScopeParentId,
                    DisplayOrder      = s.DisplayOrder,
                }
            ).ToListAsync();

            // Trabajadores con al menos una solicitud reportada desde Mi Salud.
            var workerIds = ctx.SsDescansoMedico
                .Where(d => d.State && d.ReportadoPorTrabajador)
                .Select(d => d.WorkerId)
                .Distinct();

            var trabajadores = await (
                from w in ctx.Worker
                where workerIds.Contains(w.Id)
                join p in ctx.Person on w.PersonId equals (int?)p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                orderby p != null ? p.FullName : null
                select new RevisionTrabajadorOpcionDto
                {
                    WorkerId       = w.Id,
                    NombreCompleto = p != null ? (p.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                }
            ).ToListAsync();

            var tabla = await ListPagedCore(ctx, filtro);

            return new RevisionDescansosInitDto
            {
                AreaTree     = areaTree,
                Trabajadores = trabajadores,
                Tabla        = tabla,
            };
        }

        public async Task<PagedResult<RevisionDescansoListItemDto>> ListPaged(RevisionDescansosFiltroDto filtro)
        {
            using var ctx = _factory.CreateDbContext();
            return await ListPagedCore(ctx, filtro);
        }

        private static async Task<PagedResult<RevisionDescansoListItemDto>> ListPagedCore(AppDbContext ctx, RevisionDescansosFiltroDto filtro)
        {
            var q =
                from d in ctx.SsDescansoMedico
                where d.State && d.ReportadoPorTrabajador
                join w in ctx.Worker on d.WorkerId equals w.Id
                join p in ctx.Person on w.PersonId equals (int?)p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join sc in ctx.AreaScope on w.AreaScopeId equals (int?)sc.AreaScopeId into scj
                from sc in scj.DefaultIfEmpty()
                join ai in ctx.AreaItem on sc.AreaItemId equals (int?)ai.AreaItemId into aij
                from ai in aij.DefaultIfEmpty()
                select new { d, w, p, ai };

            if (filtro.WorkerId.HasValue)
                q = q.Where(x => x.d.WorkerId == filtro.WorkerId.Value);
            if (!string.IsNullOrWhiteSpace(filtro.Estado))
                q = q.Where(x => x.d.Estado == filtro.Estado);
            if (filtro.FechaDesde.HasValue)
                q = q.Where(x => x.d.FechaInicio >= filtro.FechaDesde.Value);
            if (filtro.FechaHasta.HasValue)
                q = q.Where(x => x.d.FechaInicio <= filtro.FechaHasta.Value);
            if (filtro.AreaScopeIds is { Count: > 0 })
            {
                var areaIds = filtro.AreaScopeIds;
                q = q.Where(x => x.w.AreaScopeId != null && areaIds.Contains(x.w.AreaScopeId.Value));
            }

            var total = await q.CountAsync();
            var page  = filtro.Page < 1 ? 1 : filtro.Page;

            // Ordenamiento server-side (aplica a todos los registros, no solo a la página).
            var desc = string.Equals(filtro.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
            q = filtro.SortBy switch
            {
                "trabajador"  => desc ? q.OrderByDescending(x => x.p != null ? x.p.FullName : null)
                                      : q.OrderBy(x => x.p != null ? x.p.FullName : null),
                "area"        => desc ? q.OrderByDescending(x => x.ai != null ? x.ai.AreaItemName : null)
                                      : q.OrderBy(x => x.ai != null ? x.ai.AreaItemName : null),
                "fechaInicio" => desc ? q.OrderByDescending(x => x.d.FechaInicio) : q.OrderBy(x => x.d.FechaInicio),
                "dias"        => desc ? q.OrderByDescending(x => x.d.Dias)        : q.OrderBy(x => x.d.Dias),
                "estado"      => desc ? q.OrderByDescending(x => x.d.Estado)      : q.OrderBy(x => x.d.Estado),
                "createdAt"   => desc ? q.OrderByDescending(x => x.d.CreatedAt)   : q.OrderBy(x => x.d.CreatedAt),
                _             => q.OrderByDescending(x => x.d.CreatedAt),
            };

            var rows = await q
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new
                {
                    x.d.Id,
                    x.d.WorkerId,
                    WorkerNombre = x.p != null ? x.p.FullName : null,
                    WorkerDni    = x.p != null ? x.p.DocumentIdentityCode : null,
                    AreaNombre   = x.ai != null ? x.ai.AreaItemName : null,
                    x.d.FechaInicio,
                    x.d.FechaFin,
                    x.d.Dias,
                    Motivo = ctx.SsDescansoMotivo.Where(mm => mm.Id == x.d.MotivoId).Select(mm => mm.Nombre).FirstOrDefault() ?? x.d.Motivo,
                    x.d.Estado,
                    AdjuntosCount = ctx.SsDescansoMedicoAdjunto.Count(a => a.DescansoId == x.d.Id && a.State),
                    x.d.UrlCertificado,
                    x.d.CreatedAt,
                })
                .ToListAsync();

            var items = rows.Select(r => new RevisionDescansoListItemDto
            {
                Id           = r.Id,
                WorkerId     = r.WorkerId,
                WorkerNombre = r.WorkerNombre,
                WorkerDni    = r.WorkerDni,
                AreaNombre   = r.AreaNombre,
                FechaInicio  = r.FechaInicio,
                FechaFin     = r.FechaFin,
                Dias         = r.Dias,
                Motivo       = r.Motivo,
                Estado       = r.Estado,
                // Descansos antiguos guardaban un único archivo en url_certificado, sin filas en la tabla de adjuntos.
                AdjuntosCount = r.AdjuntosCount > 0 ? r.AdjuntosCount : (string.IsNullOrWhiteSpace(r.UrlCertificado) ? 0 : 1),
                CreatedAt    = r.CreatedAt,
            }).ToList();

            return new PagedResult<RevisionDescansoListItemDto>
            {
                Page         = page,
                PageSize     = PageSize,
                TotalRecords = total,
                TotalPages   = (int)Math.Ceiling(total / (double)PageSize),
                Data         = items,
            };
        }

        public async Task<RevisionDescansoDetalleDto> GetDetalle(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var row = await (
                from d in ctx.SsDescansoMedico
                where d.Id == id && d.State
                join w in ctx.Worker on d.WorkerId equals w.Id
                join p in ctx.Person on w.PersonId equals (int?)p.PersonId into pj
                from p in pj.DefaultIfEmpty()
                join sc in ctx.AreaScope on w.AreaScopeId equals (int?)sc.AreaScopeId into scj
                from sc in scj.DefaultIfEmpty()
                join ai in ctx.AreaItem on sc.AreaItemId equals (int?)ai.AreaItemId into aij
                from ai in aij.DefaultIfEmpty()
                join em in ctx.Contributor on d.EmpresaId equals (int?)em.ContributorId into emj
                from em in emj.DefaultIfEmpty()
                join m in ctx.SsDescansoMotivo on d.MotivoId equals (int?)m.Id into mj
                from m in mj.DefaultIfEmpty()
                select new
                {
                    d,
                    WorkerNombre  = p != null ? p.FullName : null,
                    WorkerDni     = p != null ? p.DocumentIdentityCode : null,
                    AreaNombre    = ai != null ? ai.AreaItemName : null,
                    EmpresaNombre = em != null ? em.ContributorName : null,
                    MotivoNombre  = m != null ? m.Nombre : null,
                }
            ).FirstOrDefaultAsync()
              ?? throw new AbrilException("Solicitud de descanso médico no encontrada.", 404);

            var adjuntos = await ctx.SsDescansoMedicoAdjunto
                .Where(a => a.DescansoId == id && a.State)
                .OrderBy(a => a.Id)
                .Select(a => new RevisionDescansoAdjuntoDto { Url = a.Url, Nombre = a.NombreArchivo })
                .ToListAsync();

            // Descansos antiguos guardaban un único archivo en url_certificado, sin filas en la tabla de adjuntos.
            if (adjuntos.Count == 0 && !string.IsNullOrWhiteSpace(row.d.UrlCertificado))
                adjuntos.Add(new RevisionDescansoAdjuntoDto { Url = row.d.UrlCertificado, Nombre = "Certificado médico" });

            string? aprobadoPorNombre = null;
            if (row.d.AprobadoPorId.HasValue)
            {
                var uid = row.d.AprobadoPorId.Value;
                aprobadoPorNombre = await ctx.Person
                    .Where(p => p.UserId == uid)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync()
                    ?? await ctx.User
                        .Where(u => u.UserId == uid)
                        .Select(u => u.Email)
                        .FirstOrDefaultAsync();
            }

            return new RevisionDescansoDetalleDto
            {
                Id                = row.d.Id,
                WorkerId          = row.d.WorkerId,
                WorkerNombre      = row.WorkerNombre,
                WorkerDni         = row.WorkerDni,
                AreaNombre        = row.AreaNombre,
                EmpresaNombre     = row.EmpresaNombre,
                Tipo              = row.d.Tipo,
                FechaInicio       = row.d.FechaInicio,
                FechaFin          = row.d.FechaFin,
                Dias              = row.d.Dias,
                Motivo            = row.MotivoNombre ?? row.d.Motivo,
                Diagnostico       = row.d.Diagnostico,
                Estado            = row.d.Estado,
                MotivoRechazo     = row.d.MotivoRechazo,
                AprobadoPorNombre = aprobadoPorNombre,
                FechaAprobacion   = row.d.FechaAprobacion,
                Observaciones     = row.d.Observaciones,
                CreatedAt         = row.d.CreatedAt,
                Adjuntos          = adjuntos,
            };
        }

        public async Task<int> Aprobar(List<int> ids, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var pendientes = await ctx.SsDescansoMedico
                .Include(d => d.Worker)
                    .ThenInclude(w => w!.Person)
                .Where(d => ids.Contains(d.Id) && d.State && d.Estado == "Pendiente")
                .ToListAsync();

            if (pendientes.Count == 0)
                throw new AbrilException("No hay solicitudes pendientes para aprobar en la selección.", 400);

            var ahora = DateTimeOffset.UtcNow;
            foreach (var d in pendientes)
            {
                d.Estado          = "Aprobado";
                d.AprobadoPorId   = userId;
                d.FechaAprobacion = ahora;
                d.UpdatedAt       = ahora;
            }

            // Bloqueo automático en Control de Acceso (misma regla que la aprobación individual
            // de DescansoMedicoRepository): un registro activo por trabajador.
            var workerIds = pendientes.Select(d => d.WorkerId).Distinct().ToList();

            var restriccionesExistentes = await ctx.SsTrabajadorRestringido
                .Where(r => r.WorkerId != null && workerIds.Contains(r.WorkerId.Value))
                .ToListAsync();

            foreach (var d in pendientes.GroupBy(d => d.WorkerId).Select(g => g.First()))
            {
                var delWorker = restriccionesExistentes.Where(r => r.WorkerId == d.WorkerId).ToList();
                if (delWorker.Any(r => r.Activo)) continue;

                var motivo = $"Descanso médico aprobado (ID {d.Id})";
                var anterior = delWorker.FirstOrDefault(r => !r.Activo);
                if (anterior is not null)
                {
                    anterior.Activo    = true;
                    anterior.Motivo    = motivo;
                    anterior.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    ctx.SsTrabajadorRestringido.Add(new SsTrabajadorRestringido
                    {
                        WorkerId         = d.WorkerId,
                        Dni              = d.Worker?.Person?.DocumentIdentityCode,
                        ApellidoNombre   = d.Worker?.Person?.FullName,
                        Motivo           = motivo,
                        FechaRestriccion = DateOnly.FromDateTime(DateTime.UtcNow),
                        Activo           = true,
                        CreatedAt        = DateTime.UtcNow,
                    });
                }
            }

            await ctx.SaveChangesAsync();

            // Recalcular dias_descanso_reales de los accidentes vinculados (si los hay).
            var accidenteIds = pendientes
                .Where(d => d.AccidenteId.HasValue)
                .Select(d => d.AccidenteId!.Value)
                .Distinct()
                .ToList();

            if (accidenteIds.Count > 0)
            {
                using var ctx2 = _factory.CreateDbContext();

                var totales = await ctx2.SsDescansoMedico
                    .Where(d => d.AccidenteId != null && accidenteIds.Contains(d.AccidenteId.Value)
                             && d.Estado == "Aprobado" && d.State)
                    .GroupBy(d => d.AccidenteId!.Value)
                    .Select(g => new { AccidenteId = g.Key, Dias = g.Sum(d => d.Dias) })
                    .ToListAsync();

                var accidentes = await ctx2.SsAccidenteTrabajo
                    .Where(a => accidenteIds.Contains(a.Id))
                    .ToListAsync();

                foreach (var a in accidentes)
                {
                    a.DiasDescansoReales = totales.FirstOrDefault(t => t.AccidenteId == a.Id)?.Dias ?? 0;
                    a.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await ctx2.SaveChangesAsync();
                _reactivosCacheVersion.Bump();
            }

            return pendientes.Count;
        }

        public async Task<int> Rechazar(List<int> ids, string motivoRechazo, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var pendientes = await ctx.SsDescansoMedico
                .Where(d => ids.Contains(d.Id) && d.State && d.Estado == "Pendiente")
                .ToListAsync();

            if (pendientes.Count == 0)
                throw new AbrilException("No hay solicitudes pendientes para rechazar en la selección.", 400);

            var ahora = DateTimeOffset.UtcNow;
            foreach (var d in pendientes)
            {
                d.Estado        = "Rechazado";
                d.MotivoRechazo = motivoRechazo;
                d.UpdatedAt     = ahora;
            }

            await ctx.SaveChangesAsync();
            return pendientes.Count;
        }
    }
}
