using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.VecinosModule.Features.GestionVecinosFeature.Infrastructure.Repositories
{
    public class GestionVecinosRepository : IGestionVecinosRepository
    {
        private const int PageSize = 12;

        private readonly IDbContextFactory<AppDbContext> _factory;

        public GestionVecinosRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<VecinoFormOptionsDto> GetOptions()
        {
            using var ctx = _factory.CreateDbContext();

            var projects = await ctx.Project
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ProjectOptionDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription
                })
                .ToListAsync();

            var colindancias = await ctx.VecinoColindancia
                .Where(c => c.State && c.Active)
                .OrderBy(c => c.VecinoColindanciaId)
                .Select(c => new CatalogOptionDto { Id = c.VecinoColindanciaId, Descripcion = c.Descripcion })
                .ToListAsync();

            var tipos = await ctx.VecinoTipoConstruccion
                .Where(t => t.State && t.Active)
                .OrderBy(t => t.VecinoTipoConstruccionId)
                .Select(t => new CatalogOptionDto { Id = t.VecinoTipoConstruccionId, Descripcion = t.Descripcion })
                .ToListAsync();

            return new VecinoFormOptionsDto
            {
                Projects = projects,
                Colindancias = colindancias,
                TiposConstruccion = tipos
            };
        }

        public async Task<PagedResult<VecinoListItemDto>> GetPaged(VecinoFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var query =
                from v in ctx.Vecino
                join p in ctx.Project on v.ProjectId equals p.ProjectId
                join col in ctx.VecinoColindancia on v.VecinoColindanciaId equals col.VecinoColindanciaId
                join tc in ctx.VecinoTipoConstruccion on v.VecinoTipoConstruccionId equals tc.VecinoTipoConstruccionId
                where v.Active && v.State
                select new { v, p, col, tc };

            if (filter.ProjectId.HasValue)
                query = query.Where(x => x.v.ProjectId == filter.ProjectId.Value);

            if (filter.VecinoColindanciaId.HasValue)
                query = query.Where(x => x.v.VecinoColindanciaId == filter.VecinoColindanciaId.Value);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.ToLower();
                query = query.Where(x =>
                    x.v.NombrePropietario.ToLower().Contains(s) ||
                    x.v.Direccion.ToLower().Contains(s) ||
                    x.v.Dni.Contains(s) ||
                    (x.v.Predio != null && x.v.Predio.ToLower().Contains(s)));
            }

            var totalRecords = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.v.VecinoId)
                .Skip((filter.Page - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new VecinoListItemDto
                {
                    VecinoId = x.v.VecinoId,
                    ProjectId = x.v.ProjectId,
                    ProjectDescription = x.p.ProjectDescription,
                    Predio = x.v.Predio,
                    Direccion = x.v.Direccion,
                    InteriorDepartamento = x.v.InteriorDepartamento,
                    NombrePropietario = x.v.NombrePropietario,
                    Dni = x.v.Dni,
                    Celular = x.v.Celular,
                    VecinoColindanciaId = x.v.VecinoColindanciaId,
                    ColindanciaDescripcion = x.col.Descripcion,
                    VecinoTipoConstruccionId = x.v.VecinoTipoConstruccionId,
                    TipoConstruccionDescripcion = x.tc.Descripcion,
                    CreatedDateTime = x.v.CreatedDateTime
                })
                .ToListAsync();

            return new PagedResult<VecinoListItemDto>
            {
                Page = filter.Page,
                PageSize = PageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)PageSize),
                Data = items
            };
        }

        public async Task<int> Create(VecinoCreateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var vecino = new Vecino
            {
                ProjectId = dto.ProjectId,
                Predio = dto.Predio?.Trim(),
                Direccion = dto.Direccion.Trim(),
                InteriorDepartamento = dto.InteriorDepartamento?.Trim(),
                NombrePropietario = dto.NombrePropietario.Trim(),
                Dni = dto.Dni.Trim(),
                Celular = dto.Celular?.Trim(),
                VecinoColindanciaId = dto.VecinoColindanciaId,
                VecinoTipoConstruccionId = dto.VecinoTipoConstruccionId,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            ctx.Vecino.Add(vecino);
            await ctx.SaveChangesAsync();
            return vecino.VecinoId;
        }

        // ── Solicitudes ─────────────────────────────────────────────────────
        public async Task<bool> VecinoExists(int vecinoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Vecino.AnyAsync(v => v.VecinoId == vecinoId && v.Active && v.State);
        }

        public async Task<VecinoSolicitudesResponseDto> GetSolicitudes(int vecinoId)
        {
            using var ctx = _factory.CreateDbContext();

            var solicitudes = await (
                from s in ctx.VecinoSolicitud
                join e in ctx.VecinoSolicitudEstado on s.VecinoSolicitudEstadoId equals e.VecinoSolicitudEstadoId
                where s.VecinoId == vecinoId && s.Active && s.State
                orderby s.VecinoSolicitudId descending
                select new VecinoSolicitudItemDto
                {
                    VecinoSolicitudId = s.VecinoSolicitudId,
                    VecinoId = s.VecinoId,
                    Descripcion = s.Descripcion,
                    EsCritica = s.EsCritica,
                    VecinoSolicitudEstadoId = s.VecinoSolicitudEstadoId,
                    EstadoDescripcion = e.Descripcion,
                    CreatedDateTime = s.CreatedDateTime
                }
            ).ToListAsync();

            var estados = await ctx.VecinoSolicitudEstado
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.VecinoSolicitudEstadoId)
                .Select(e => new CatalogOptionDto { Id = e.VecinoSolicitudEstadoId, Descripcion = e.Descripcion })
                .ToListAsync();

            var compromisoEstados = await ctx.VecinoCompromisoEstado
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.VecinoCompromisoEstadoId)
                .Select(e => new CatalogOptionDto { Id = e.VecinoCompromisoEstadoId, Descripcion = e.Descripcion })
                .ToListAsync();

            var entregableEstados = await ctx.VecinoEntregableEstado
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.VecinoEntregableEstadoId)
                .Select(e => new CatalogOptionDto { Id = e.VecinoEntregableEstadoId, Descripcion = e.Descripcion })
                .ToListAsync();

            return new VecinoSolicitudesResponseDto
            {
                Solicitudes = solicitudes,
                Estados = estados,
                CompromisoEstados = compromisoEstados,
                EntregableEstados = entregableEstados
            };
        }

        public async Task<int> CreateSolicitud(int vecinoId, VecinoSolicitudCreateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Toda solicitud nueva nace en estado "Por responder".
            var estadoPorResponder = await ctx.VecinoSolicitudEstado
                .Where(e => e.Descripcion == "Por responder" && e.State)
                .Select(e => e.VecinoSolicitudEstadoId)
                .FirstOrDefaultAsync();

            var solicitud = new VecinoSolicitud
            {
                VecinoId = vecinoId,
                Descripcion = dto.Descripcion.Trim(),
                EsCritica = dto.EsCritica,
                VecinoSolicitudEstadoId = estadoPorResponder,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            ctx.VecinoSolicitud.Add(solicitud);
            await ctx.SaveChangesAsync();
            return solicitud.VecinoSolicitudId;
        }

        public async Task<bool> UpdateSolicitudEstado(int solicitudId, int estadoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var solicitud = await ctx.VecinoSolicitud
                .FirstOrDefaultAsync(s => s.VecinoSolicitudId == solicitudId && s.Active && s.State);
            if (solicitud is null) return false;

            var estadoValido = await ctx.VecinoSolicitudEstado
                .AnyAsync(e => e.VecinoSolicitudEstadoId == estadoId && e.State);
            if (!estadoValido) return false;

            solicitud.VecinoSolicitudEstadoId = estadoId;
            solicitud.UpdatedDateTime = DateTime.UtcNow;
            solicitud.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
            return true;
        }

        // ── Compromisos ─────────────────────────────────────────────────────
        public async Task<bool> SolicitudExists(int solicitudId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.VecinoSolicitud.AnyAsync(s => s.VecinoSolicitudId == solicitudId && s.Active && s.State);
        }

        public async Task<List<VecinoCompromisoItemDto>> GetCompromisos(int solicitudId)
        {
            using var ctx = _factory.CreateDbContext();

            var compromisos = await (
                from c in ctx.VecinoCompromiso
                join e in ctx.VecinoCompromisoEstado on c.VecinoCompromisoEstadoId equals e.VecinoCompromisoEstadoId
                where c.VecinoSolicitudId == solicitudId && c.Active && c.State
                orderby c.VecinoCompromisoId descending
                select new VecinoCompromisoItemDto
                {
                    VecinoCompromisoId = c.VecinoCompromisoId,
                    VecinoSolicitudId = c.VecinoSolicitudId,
                    Descripcion = c.Descripcion,
                    EsCritico = c.EsCritico,
                    VecinoCompromisoEstadoId = c.VecinoCompromisoEstadoId,
                    EstadoDescripcion = e.Descripcion,
                    FechaInicio = c.FechaInicio,
                    FechaFin = c.FechaFin,
                    CreatedDateTime = c.CreatedDateTime
                }
            ).ToListAsync();

            if (compromisos.Count == 0) return compromisos;

            var ids = compromisos.Select(c => c.VecinoCompromisoId).ToList();

            var entregables = await (
                from en in ctx.VecinoCompromisoEntregable
                join t in ctx.VecinoEntregableTipo on en.VecinoEntregableTipoId equals t.VecinoEntregableTipoId
                join es in ctx.VecinoEntregableEstado on en.VecinoEntregableEstadoId equals es.VecinoEntregableEstadoId
                where ids.Contains(en.VecinoCompromisoId) && en.Active && en.State
                orderby t.Orden
                select new
                {
                    en.VecinoCompromisoId,
                    Item = new VecinoEntregableItemDto
                    {
                        VecinoCompromisoEntregableId = en.VecinoCompromisoEntregableId,
                        VecinoEntregableTipoId = en.VecinoEntregableTipoId,
                        TipoDescripcion = t.Descripcion,
                        Orden = t.Orden,
                        VecinoEntregableEstadoId = en.VecinoEntregableEstadoId,
                        EstadoDescripcion = es.Descripcion
                    }
                }
            ).ToListAsync();

            var byCompromiso = entregables
                .GroupBy(x => x.VecinoCompromisoId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Item).ToList());

            foreach (var c in compromisos)
                c.Entregables = byCompromiso.GetValueOrDefault(c.VecinoCompromisoId, new());

            return compromisos;
        }

        public async Task<int> CreateCompromiso(int solicitudId, VecinoCompromisoCreateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var estadoId = dto.VecinoCompromisoEstadoId ?? await ctx.VecinoCompromisoEstado
                .Where(e => e.Descripcion == "Pendiente" && e.State)
                .Select(e => e.VecinoCompromisoEstadoId)
                .FirstOrDefaultAsync();

            var compromiso = new VecinoCompromiso
            {
                VecinoSolicitudId = solicitudId,
                Descripcion = dto.Descripcion.Trim(),
                EsCritico = dto.EsCritico,
                VecinoCompromisoEstadoId = estadoId,
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            ctx.VecinoCompromiso.Add(compromiso);
            await ctx.SaveChangesAsync();

            // Crear los entregables (un registro por tipo) con estado por defecto "Falta".
            var estadoFalta = await ctx.VecinoEntregableEstado
                .Where(e => e.Descripcion == "Falta" && e.State)
                .Select(e => e.VecinoEntregableEstadoId)
                .FirstOrDefaultAsync();

            var tipos = await ctx.VecinoEntregableTipo
                .Where(t => t.State && t.Active)
                .OrderBy(t => t.Orden)
                .Select(t => t.VecinoEntregableTipoId)
                .ToListAsync();

            foreach (var tipoId in tipos)
            {
                ctx.VecinoCompromisoEntregable.Add(new VecinoCompromisoEntregable
                {
                    VecinoCompromisoId = compromiso.VecinoCompromisoId,
                    VecinoEntregableTipoId = tipoId,
                    VecinoEntregableEstadoId = estadoFalta,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true
                });
            }

            await ctx.SaveChangesAsync();
            return compromiso.VecinoCompromisoId;
        }

        public async Task<bool> UpdateCompromisoEstado(int compromisoId, int estadoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var compromiso = await ctx.VecinoCompromiso
                .FirstOrDefaultAsync(c => c.VecinoCompromisoId == compromisoId && c.Active && c.State);
            if (compromiso is null) return false;

            var valido = await ctx.VecinoCompromisoEstado.AnyAsync(e => e.VecinoCompromisoEstadoId == estadoId && e.State);
            if (!valido) return false;

            compromiso.VecinoCompromisoEstadoId = estadoId;
            compromiso.UpdatedDateTime = DateTime.UtcNow;
            compromiso.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateEntregableEstado(int entregableId, int estadoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entregable = await ctx.VecinoCompromisoEntregable
                .FirstOrDefaultAsync(e => e.VecinoCompromisoEntregableId == entregableId && e.Active && e.State);
            if (entregable is null) return false;

            var valido = await ctx.VecinoEntregableEstado.AnyAsync(e => e.VecinoEntregableEstadoId == estadoId && e.State);
            if (!valido) return false;

            entregable.VecinoEntregableEstadoId = estadoId;
            entregable.UpdatedDateTime = DateTime.UtcNow;
            entregable.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
            return true;
        }

        // ── Dashboard ────────────────────────────────────────────────────────
        public async Task<VecinosDashboardDto> GetDashboard()
        {
            using var ctx = _factory.CreateDbContext();

            // Catálogos de estado (definen las "porciones" de cada donut, incluso si quedan en 0).
            var solicitudEstados = await ctx.VecinoSolicitudEstado
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.VecinoSolicitudEstadoId)
                .Select(e => new CatalogOptionDto { Id = e.VecinoSolicitudEstadoId, Descripcion = e.Descripcion })
                .ToListAsync();

            var compromisoEstados = await ctx.VecinoCompromisoEstado
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.VecinoCompromisoEstadoId)
                .Select(e => new CatalogOptionDto { Id = e.VecinoCompromisoEstadoId, Descripcion = e.Descripcion })
                .ToListAsync();

            // Todos los proyectos activos (aparecen aunque no tengan vecinos aún).
            var projects = await ctx.Project
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new { p.ProjectId, p.ProjectDescription })
                .ToListAsync();

            // Conteos agregados (una consulta por métrica para evitar N+1).
            var vecinoCounts = await ctx.Vecino
                .Where(v => v.Active && v.State)
                .GroupBy(v => v.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToListAsync();

            var solicitudCounts = await (
                from s in ctx.VecinoSolicitud
                join v in ctx.Vecino on s.VecinoId equals v.VecinoId
                where s.Active && s.State && v.Active && v.State
                group s by new { v.ProjectId, s.VecinoSolicitudEstadoId } into g
                select new { g.Key.ProjectId, EstadoId = g.Key.VecinoSolicitudEstadoId, Count = g.Count() }
            ).ToListAsync();

            var compromisoCounts = await (
                from c in ctx.VecinoCompromiso
                join s in ctx.VecinoSolicitud on c.VecinoSolicitudId equals s.VecinoSolicitudId
                join v in ctx.Vecino on s.VecinoId equals v.VecinoId
                where c.Active && c.State && s.Active && s.State && v.Active && v.State
                group c by new { v.ProjectId, c.VecinoCompromisoEstadoId } into g
                select new { g.Key.ProjectId, EstadoId = g.Key.VecinoCompromisoEstadoId, Count = g.Count() }
            ).ToListAsync();

            List<VecinosDashboardEstadoDto> SolicitudesFor(int? projectId) => solicitudEstados
                .Select(e => new VecinosDashboardEstadoDto
                {
                    EstadoId = e.Id,
                    Descripcion = e.Descripcion,
                    Count = solicitudCounts
                        .Where(x => x.EstadoId == e.Id && (projectId == null || x.ProjectId == projectId))
                        .Sum(x => x.Count)
                })
                .ToList();

            List<VecinosDashboardEstadoDto> CompromisosFor(int? projectId) => compromisoEstados
                .Select(e => new VecinosDashboardEstadoDto
                {
                    EstadoId = e.Id,
                    Descripcion = e.Descripcion,
                    Count = compromisoCounts
                        .Where(x => x.EstadoId == e.Id && (projectId == null || x.ProjectId == projectId))
                        .Sum(x => x.Count)
                })
                .ToList();

            var proyectos = projects.Select(p => new VecinosDashboardProjectDto
            {
                ProjectId = p.ProjectId,
                ProjectDescription = p.ProjectDescription,
                VecinosCount = vecinoCounts.FirstOrDefault(x => x.ProjectId == p.ProjectId)?.Count ?? 0,
                Solicitudes = SolicitudesFor(p.ProjectId),
                Compromisos = CompromisosFor(p.ProjectId)
            }).ToList();

            var resumen = new VecinosDashboardProjectDto
            {
                ProjectId = 0,
                ProjectDescription = "Resumen general",
                VecinosCount = vecinoCounts.Sum(x => x.Count),
                Solicitudes = SolicitudesFor(null),
                Compromisos = CompromisosFor(null)
            };

            return new VecinosDashboardDto { Resumen = resumen, Proyectos = proyectos };
        }

        // ── Requisitos ───────────────────────────────────────────────────────
        public async Task<VecinoRequisitosResponseDto> GetRequisitos(int vecinoId)
        {
            using var ctx = _factory.CreateDbContext();

            var tipos = await ctx.VecinoRequisitoTipo
                .Where(t => t.State && t.Active)
                .OrderBy(t => t.Orden)
                .ToListAsync();

            var estados = await ctx.VecinoRequisitoEstado
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.VecinoRequisitoEstadoId)
                .ToListAsync();

            var rows = await ctx.VecinoRequisito
                .Where(r => r.VecinoId == vecinoId && r.State)
                .ToListAsync();

            var estadoDesc = estados.ToDictionary(e => e.VecinoRequisitoEstadoId, e => e.Descripcion);
            int noSubidoId = estados.FirstOrDefault(e => e.Descripcion == "No subido")?.VecinoRequisitoEstadoId ?? 0;

            var items = tipos.Select(t =>
            {
                var row = rows.FirstOrDefault(r => r.VecinoRequisitoTipoId == t.VecinoRequisitoTipoId);
                return new VecinoRequisitoItemDto
                {
                    VecinoRequisitoId = row?.VecinoRequisitoId,
                    VecinoRequisitoTipoId = t.VecinoRequisitoTipoId,
                    TipoDescripcion = t.Descripcion,
                    Orden = t.Orden,
                    VecinoRequisitoEstadoId = row?.VecinoRequisitoEstadoId ?? noSubidoId,
                    EstadoDescripcion = row != null ? estadoDesc[row.VecinoRequisitoEstadoId] : "No subido",
                    ArchivoUrl = row?.ArchivoUrl,
                    OriginalFileName = row?.OriginalFileName,
                };
            }).ToList();

            return new VecinoRequisitosResponseDto
            {
                Requisitos = items,
                Estados = estados.Select(e => new CatalogOptionDto { Id = e.VecinoRequisitoEstadoId, Descripcion = e.Descripcion }).ToList(),
            };
        }

        public async Task<bool> TipoRequisitoExists(int tipoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.VecinoRequisitoTipo.AnyAsync(t => t.VecinoRequisitoTipoId == tipoId && t.State && t.Active);
        }

        /// <summary>Obtiene la fila activa del requisito (vecino + tipo) o crea una nueva en el contexto.</summary>
        private static async Task<VecinoRequisito> GetOrCreateRow(AppDbContext ctx, int vecinoId, int tipoId, int userId)
        {
            var row = await ctx.VecinoRequisito
                .FirstOrDefaultAsync(r => r.VecinoId == vecinoId && r.VecinoRequisitoTipoId == tipoId && r.State);

            if (row == null)
            {
                row = new VecinoRequisito
                {
                    VecinoId = vecinoId,
                    VecinoRequisitoTipoId = tipoId,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = userId,
                    Active = true,
                    State = true,
                };
                ctx.VecinoRequisito.Add(row);
            }
            else
            {
                row.UpdatedDateTime = DateTime.UtcNow;
                row.UpdatedUserId = userId;
            }
            return row;
        }

        public async Task UploadRequisito(int vecinoId, int tipoId, string archivoUrl, string? originalFileName, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var subidoId = await ctx.VecinoRequisitoEstado
                .Where(e => e.Descripcion == "Subido" && e.State)
                .Select(e => e.VecinoRequisitoEstadoId)
                .FirstOrDefaultAsync();

            var row = await GetOrCreateRow(ctx, vecinoId, tipoId, userId);
            row.ArchivoUrl = archivoUrl;
            row.OriginalFileName = originalFileName;
            row.VecinoRequisitoEstadoId = subidoId; // subir archivo ⇒ Subido (sobrescribe "No aplica").

            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Marca/desmarca "No aplica". Al desmarcar, el estado vuelve a derivarse automáticamente
        /// según haya o no archivo (Subido / No subido). El usuario nunca fija Subido/No subido a mano.
        /// </summary>
        public async Task SetRequisitoNoAplica(int vecinoId, int tipoId, bool noAplica, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var estados = await ctx.VecinoRequisitoEstado
                .Where(e => e.State)
                .Select(e => new { e.VecinoRequisitoEstadoId, e.Descripcion })
                .ToListAsync();
            int noAplicaId = estados.First(e => e.Descripcion == "No aplica").VecinoRequisitoEstadoId;
            int subidoId = estados.First(e => e.Descripcion == "Subido").VecinoRequisitoEstadoId;
            int noSubidoId = estados.First(e => e.Descripcion == "No subido").VecinoRequisitoEstadoId;

            var row = await GetOrCreateRow(ctx, vecinoId, tipoId, userId);
            row.VecinoRequisitoEstadoId = noAplica
                ? noAplicaId
                : (string.IsNullOrEmpty(row.ArchivoUrl) ? noSubidoId : subidoId);

            await ctx.SaveChangesAsync();
        }
    }
}
