using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    public class ReclutamientoRepository : IReclutamientoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ReclutamientoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<ReclutamientoFormDataDto> GetFormData(int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var dto = new ReclutamientoFormDataDto { MaxVacantes = 10 };

            if (userId.HasValue)
            {
                var (areaNombre, areaScopeId, _) = await ResolveSolicitanteInternal(ctx, userId.Value);
                dto.AreaNombre = areaNombre;
                dto.AreaScopeId = areaScopeId;
            }

            dto.Puestos = await ctx.GthPuesto
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
                .Select(p => new OpcionDto { Id = p.GthPuestoId, Nombre = p.Nombre })
                .ToListAsync();

            dto.TiposRequerimiento = await ctx.GthTipoRequerimiento
                .Where(t => t.State && t.Active)
                .OrderBy(t => t.Orden)
                .Select(t => new OpcionDto { Id = t.GthTipoRequerimientoId, Nombre = t.Nombre })
                .ToListAsync();

            dto.Proyectos = await ctx.Project
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new OpcionDto { Id = p.ProjectId, Nombre = p.ProjectDescription })
                .ToListAsync();

            return dto;
        }

        public async Task<(string? AreaNombre, int? AreaScopeId, int? WorkerId)> ResolveSolicitante(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ResolveSolicitanteInternal(ctx, userId);
        }

        public async Task<string?> GetSustentoFolderUrl()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.GthSustentoFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GthSustentoFolderId)
                .Select(f => f.LinkUrl)
                .FirstOrDefaultAsync();
        }

        public async Task<List<SolicitudVacanteListItemDto>> GetMisSolicitudesVacante(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ProjectRequerimientos(
                ctx,
                ctx.GthRequerimiento.Where(r => r.State && r.Solicitud!.State && r.Solicitud.SolicitanteUserId == userId));
        }

        public async Task<List<SolicitudVacanteListItemDto>> GetRequerimientosBySolicitud(int solicitudId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ProjectRequerimientos(
                ctx,
                ctx.GthRequerimiento.Where(r => r.State && r.GthSolicitudId == solicitudId));
        }

        public async Task<BandejaReclutamientoDto> GetBandeja()
        {
            using var ctx = _factory.CreateDbContext();

            // Todos los requerimientos vigentes (de cualquier área), más recientes primero.
            // Left join a gth_prioridad porque la prioridad es opcional (nullable).
            var raw = await (
                from r in ctx.GthRequerimiento
                where r.State && r.Solicitud!.State
                join p in ctx.GthPuesto on r.GthPuestoId equals p.GthPuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                join prio in ctx.GthPrioridad on r.GthPrioridadId equals prio.GthPrioridadId into prioJoin
                from prio in prioJoin.DefaultIfEmpty()
                orderby r.CreatedDateTime descending, r.GthRequerimientoId descending
                select new
                {
                    r.GthRequerimientoId,
                    r.Codigo,
                    Area            = r.Solicitud!.AreaNombre,
                    Puesto          = p.Nombre,
                    ProyectoObra    = pr.ProjectDescription,
                    r.CreatedDateTime,
                    r.FechaRequeridaIngreso,
                    PrioridadId     = prio != null ? (int?)prio.GthPrioridadId : null,
                    PrioridadNombre = prio != null ? prio.Nombre : null,
                    EstadoCodigo    = e.Codigo,
                    EstadoNombre    = e.Nombre,
                }).ToListAsync();

            // Conversión a hora Perú en memoria (evita traducir ToOffset en el join).
            var solicitudes = raw.Select(x => new RequerimientoGthListItemDto
            {
                RequerimientoId       = x.GthRequerimientoId,
                Codigo                = x.Codigo,
                Area                  = x.Area,
                Puesto                = x.Puesto,
                ProyectoObra          = x.ProyectoObra,
                FechaLlegada          = x.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                FechaRequeridaIngreso = x.FechaRequeridaIngreso,
                PrioridadId           = x.PrioridadId,
                PrioridadNombre       = x.PrioridadNombre,
                EstadoCodigo          = x.EstadoCodigo,
                EstadoNombre          = x.EstadoNombre,
            }).ToList();

            // Catálogo de prioridades para el desplegable de la columna (orden semántico Alta→Media→Baja).
            var prioridades = await ctx.GthPrioridad
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.Orden)
                .Select(p => new OpcionDto { Id = p.GthPrioridadId, Nombre = p.Nombre })
                .ToListAsync();

            return new BandejaReclutamientoDto
            {
                // "En proceso" = requerimientos vigentes en curso. Aún no hay estado de cierre modelado,
                // así que por ahora son todos los vigentes; cuando exista un estado terminal se excluirá aquí.
                Resumen     = new ResumenReclutamientoDto { EnProceso = solicitudes.Count },
                Solicitudes = solicitudes,
                Prioridades = prioridades,
            };
        }

        public async Task UpdatePrioridad(int requerimientoId, int prioridadId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var prioridadOk = await ctx.GthPrioridad
                .AnyAsync(p => p.GthPrioridadId == prioridadId && p.State && p.Active);
            if (!prioridadOk)
                throw new AbrilException("La prioridad seleccionada no es válida.", 400);

            var req = await ctx.GthRequerimiento
                .FirstOrDefaultAsync(r => r.GthRequerimientoId == requerimientoId && r.State);
            if (req == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            req.GthPrioridadId  = prioridadId;
            req.UpdatedDateTime = DateTimeOffset.UtcNow;
            req.UpdatedUserId   = userId;
            await ctx.SaveChangesAsync();
        }

        /// <summary>Tope de trabajadores por razón social para el cálculo de cupos (los practicantes no consumen cupo).</summary>
        private const int TopeCuposRazonSocial = 20;

        public async Task<DetalleRequerimientoGthDto?> GetDetalleGth(int requerimientoId)
        {
            using var ctx = _factory.CreateDbContext();

            // Cabecera + asignación interna actual (sin scope por usuario: es la vista de GTH).
            var head = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId && r.State && r.Solicitud!.State
                join p in ctx.GthPuesto on r.GthPuestoId equals p.GthPuestoId
                join t in ctx.GthTipoRequerimiento on r.GthTipoRequerimientoId equals t.GthTipoRequerimientoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                select new
                {
                    r.GthRequerimientoId,
                    r.Codigo,
                    Puesto       = p.Nombre,
                    Tipo         = t.Nombre,
                    Area         = r.Solicitud!.AreaNombre,
                    ProyectoObra = pr.ProjectDescription,
                    r.FechaRequeridaIngreso,
                    EstadoCodigo = e.Codigo,
                    EstadoNombre = e.Nombre,
                    r.GthResponsableProcesoId,
                    r.GthTipoProcesoId,
                    r.GthPrioridadId,
                    r.ContributorId,
                }).FirstOrDefaultAsync();

            if (head == null) return null;

            // Responsables del proceso (miembros GTH): nombre desde la base maestra.
            var responsables = await ctx.GthResponsableProceso
                .Where(rp => rp.State && rp.Active)
                .OrderBy(rp => rp.Orden)
                .Select(rp => new OpcionDto
                {
                    Id     = rp.GthResponsableProcesoId,
                    Nombre = rp.Worker!.Person!.FullName ?? rp.Worker.ApellidoNombre ?? "",
                })
                .ToListAsync();

            var tiposProceso = await ctx.GthTipoProceso
                .Where(t => t.State && t.Active)
                .OrderBy(t => t.Orden)
                .Select(t => new TipoProcesoOpcionDto
                {
                    Id          = t.GthTipoProcesoId,
                    Nombre      = t.Nombre,
                    SlaDias     = t.SlaDias,
                    Descripcion = t.Descripcion,
                })
                .ToListAsync();

            var prioridades = await ctx.GthPrioridad
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.Orden)
                .Select(p => new OpcionDto { Id = p.GthPrioridadId, Nombre = p.Nombre })
                .ToListAsync();

            // Razones sociales activas del grupo (contributor.operativo = true).
            var razones = await ctx.Contributor
                .Where(c => c.State && c.Active && c.Operativo)
                .OrderBy(c => c.ContributorName)
                .Select(c => new { c.ContributorId, c.ContributorName })
                .ToListAsync();

            // Ocupación por razón social desde la base maestra: trabajadores no retirados;
            // los practicantes no consumen el tope de 20.
            var ocupados = await ctx.Worker
                .Where(w => w.ContributorId != null
                            && w.Estado != "RETIRADO"
                            && (w.Categoria == null || w.Categoria.Trim().ToLower() != "practicante"))
                .GroupBy(w => w.ContributorId!.Value)
                .Select(g => new { ContributorId = g.Key, Total = g.Count() })
                .ToListAsync();
            var ocupadosPorRazon = ocupados.ToDictionary(o => o.ContributorId, o => o.Total);

            var razonesSociales = razones.Select(c => new RazonSocialOpcionDto
            {
                Id     = c.ContributorId,
                Nombre = c.ContributorName,
                CuposDisponibles = Math.Max(0,
                    TopeCuposRazonSocial - ocupadosPorRazon.GetValueOrDefault(c.ContributorId)),
            }).ToList();

            // Canales de publicación + publicaciones ya registradas de este requerimiento.
            var canales = await ctx.GthCanalPublicacion
                .Where(c => c.State && c.Active)
                .OrderBy(c => c.Orden)
                .Select(c => new CanalPublicacionDto
                {
                    Id            = c.GthCanalPublicacionId,
                    Nombre        = c.Nombre,
                    ApiDisponible = c.ApiDisponible,
                })
                .ToListAsync();

            var publicados = await ctx.GthRequerimientoCanal
                .Where(rc => rc.GthRequerimientoId == requerimientoId && rc.State && rc.Active)
                .Select(rc => rc.GthCanalPublicacionId)
                .ToListAsync();
            foreach (var c in canales)
                c.Publicado = publicados.Contains(c.Id);

            return new DetalleRequerimientoGthDto
            {
                RequerimientoId       = head.GthRequerimientoId,
                Codigo                = head.Codigo,
                Puesto                = head.Puesto,
                Area                  = head.Area,
                ProyectoObra          = head.ProyectoObra,
                TipoRequerimiento     = head.Tipo,
                Vacantes              = 1, // cada vacante de una solicitud genera su propio requerimiento
                FechaRequeridaIngreso = head.FechaRequeridaIngreso,
                EstadoCodigo          = head.EstadoCodigo,
                EstadoNombre          = head.EstadoNombre,
                Asignacion = new AsignacionGthDto
                {
                    ResponsableId = head.GthResponsableProcesoId,
                    TipoProcesoId = head.GthTipoProcesoId,
                    PrioridadId   = head.GthPrioridadId,
                    ContributorId = head.ContributorId,
                },
                Responsables    = responsables,
                TiposProceso    = tiposProceso,
                Prioridades     = prioridades,
                RazonesSociales = razonesSociales,
                Canales         = canales,
            };
        }

        public async Task UpdateAsignacionGth(int requerimientoId, AsignacionGthUpdateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var req = await ctx.GthRequerimiento
                .FirstOrDefaultAsync(r => r.GthRequerimientoId == requerimientoId && r.State);
            if (req == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            // Validar cada id no nulo contra su catálogo vigente.
            if (dto.ResponsableId.HasValue)
            {
                var ok = await ctx.GthResponsableProceso
                    .AnyAsync(r => r.GthResponsableProcesoId == dto.ResponsableId.Value && r.State && r.Active);
                if (!ok) throw new AbrilException("El responsable seleccionado no es válido.", 400);
            }
            if (dto.TipoProcesoId.HasValue)
            {
                var ok = await ctx.GthTipoProceso
                    .AnyAsync(t => t.GthTipoProcesoId == dto.TipoProcesoId.Value && t.State && t.Active);
                if (!ok) throw new AbrilException("El tipo de proceso seleccionado no es válido.", 400);
            }
            if (dto.PrioridadId.HasValue)
            {
                var ok = await ctx.GthPrioridad
                    .AnyAsync(p => p.GthPrioridadId == dto.PrioridadId.Value && p.State && p.Active);
                if (!ok) throw new AbrilException("La prioridad seleccionada no es válida.", 400);
            }
            if (dto.ContributorId.HasValue)
            {
                var ok = await ctx.Contributor
                    .AnyAsync(c => c.ContributorId == dto.ContributorId.Value && c.State && c.Active && c.Operativo);
                if (!ok) throw new AbrilException("La razón social seleccionada no es válida.", 400);
            }

            req.GthResponsableProcesoId = dto.ResponsableId;
            req.GthTipoProcesoId        = dto.TipoProcesoId;
            req.GthPrioridadId          = dto.PrioridadId;
            req.ContributorId           = dto.ContributorId;
            req.UpdatedDateTime         = DateTimeOffset.UtcNow;
            req.UpdatedUserId           = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task<EstadoRequerimientoResultDto> ReplacePublicaciones(int requerimientoId, List<int> canalIds, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var req = await ctx.GthRequerimiento
                .FirstOrDefaultAsync(r => r.GthRequerimientoId == requerimientoId && r.State);
            if (req == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            var deseados = canalIds.Distinct().ToList();
            if (deseados.Count > 0)
            {
                var validos = await ctx.GthCanalPublicacion
                    .CountAsync(c => deseados.Contains(c.GthCanalPublicacionId) && c.State && c.Active);
                if (validos != deseados.Count)
                    throw new AbrilException("Uno o más canales seleccionados no son válidos.", 400);
            }

            var now = DateTimeOffset.UtcNow;

            // Reconciliación contra las publicaciones vigentes (mismo patrón que los
            // destinatarios de correo: alta de nuevos, baja de los quitados).
            var vigentes = await ctx.GthRequerimientoCanal
                .Where(rc => rc.GthRequerimientoId == requerimientoId && rc.State)
                .ToListAsync();

            foreach (var canalId in deseados)
            {
                var v = vigentes.FirstOrDefault(x => x.GthCanalPublicacionId == canalId);
                if (v != null)
                {
                    if (!v.Active)
                    {
                        v.Active          = true;
                        v.UpdatedDateTime = now;
                        v.UpdatedUserId   = userId;
                    }
                }
                else
                {
                    ctx.GthRequerimientoCanal.Add(new GthRequerimientoCanal
                    {
                        GthRequerimientoId   = requerimientoId,
                        GthCanalPublicacionId = canalId,
                        CreatedDateTime      = now,
                        CreatedUserId        = userId,
                        Active               = true,
                        State                = true,
                    });
                }
            }

            foreach (var v in vigentes)
            {
                if (!deseados.Contains(v.GthCanalPublicacionId))
                {
                    v.State           = false;
                    v.UpdatedDateTime = now;
                    v.UpdatedUserId   = userId;
                }
            }

            // Avance del pipeline: registrar la publicación deja el requerimiento en la fase
            // PUBLICACION (no hay integración real con los portales; el registro es manual).
            // Si ya está en esa fase o más adelante, no se retrocede.
            var estados = await ctx.GthEstadoRequerimiento
                .Where(e => e.State && (e.Codigo == EstadoReclutamiento.Publicacion
                                        || e.GthEstadoRequerimientoId == req.GthEstadoRequerimientoId))
                .ToListAsync();
            var publicacion = estados.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.Publicacion)
                ?? throw new AbrilException("No está configurado el estado PUBLICACION de reclutamiento.", 500);
            var actual = estados.FirstOrDefault(e => e.GthEstadoRequerimientoId == req.GthEstadoRequerimientoId);

            if (actual == null || actual.Orden < publicacion.Orden)
            {
                req.GthEstadoRequerimientoId = publicacion.GthEstadoRequerimientoId;
                req.UpdatedDateTime          = now;
                req.UpdatedUserId            = userId;
                actual = publicacion;
            }

            await ctx.SaveChangesAsync();

            return new EstadoRequerimientoResultDto
            {
                EstadoCodigo = actual.Codigo,
                EstadoNombre = actual.Nombre,
            };
        }

        public async Task<EstadoRequerimientoResultDto> IniciarRevisionCv(int requerimientoId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var req = await ctx.GthRequerimiento
                .FirstOrDefaultAsync(r => r.GthRequerimientoId == requerimientoId && r.State);
            if (req == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            var estados = await ctx.GthEstadoRequerimiento
                .Where(e => e.State && (e.Codigo == EstadoReclutamiento.LongList
                                        || e.GthEstadoRequerimientoId == req.GthEstadoRequerimientoId))
                .ToListAsync();
            var longList = estados.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.LongList)
                ?? throw new AbrilException("No está configurado el estado LONG_LIST de reclutamiento.", 500);
            var actual = estados.FirstOrDefault(e => e.GthEstadoRequerimientoId == req.GthEstadoRequerimientoId);

            // Idempotente: si ya está en Long list (o más adelante) no se retrocede ni se duplica.
            if (actual != null && actual.Orden >= longList.Orden)
                return new EstadoRequerimientoResultDto { EstadoCodigo = actual.Codigo, EstadoNombre = actual.Nombre };

            // Solo se inicia la revisión de CV desde la fase de publicación.
            if (actual == null || actual.Codigo != EstadoReclutamiento.Publicacion)
                throw new AbrilException("La vacante aún no está publicada en los canales de publicación.", 400);

            req.GthEstadoRequerimientoId = longList.GthEstadoRequerimientoId;
            req.UpdatedDateTime          = DateTimeOffset.UtcNow;
            req.UpdatedUserId            = userId;
            await ctx.SaveChangesAsync();

            return new EstadoRequerimientoResultDto
            {
                EstadoCodigo = longList.Codigo,
                EstadoNombre = longList.Nombre,
            };
        }

        public async Task<SeguimientoDto?> GetSeguimiento(int requerimientoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Cabecera del requerimiento (scope: solo del usuario dueño de la solicitud).
            var head = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId
                      && r.State && r.Solicitud!.State
                      && r.Solicitud.SolicitanteUserId == userId
                join p in ctx.GthPuesto on r.GthPuestoId equals p.GthPuestoId
                join t in ctx.GthTipoRequerimiento on r.GthTipoRequerimientoId equals t.GthTipoRequerimientoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                select new
                {
                    r.GthRequerimientoId,
                    r.Codigo,
                    Puesto            = p.Nombre,
                    Tipo              = t.Nombre,
                    Area              = r.Solicitud!.AreaNombre,
                    ProyectoObra      = pr.ProjectDescription,
                    r.Solicitud.Justificacion,
                    r.FechaRequeridaIngreso,
                    r.CreatedDateTime,
                    EstadoCodigo      = e.Codigo,
                    EstadoNombre      = e.Nombre,
                    EstadoOrden       = e.Orden,
                    r.Solicitud.SustentoNombre,
                    r.Solicitud.SustentoUrl,
                }).FirstOrDefaultAsync();

            if (head == null) return null;

            // Catálogo de fases del pipeline (todas las vigentes, en orden).
            var fases = await ctx.GthEstadoRequerimiento
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.Orden)
                .Select(e => new FaseSeguimientoDto
                {
                    Codigo      = e.Codigo,
                    Nombre      = e.Nombre,
                    Descripcion = e.Descripcion,
                    Orden       = e.Orden,
                })
                .ToListAsync();

            // Estado visual de cada fase respecto a la fase actual del requerimiento.
            foreach (var f in fases)
                f.Estado = f.Orden < head.EstadoOrden ? "done"
                         : f.Orden == head.EstadoOrden ? "current"
                         : "pending";

            // Reemplazo (tipo "Reemplazo") no requiere aprobación de Gerencia General; puesto nuevo sí.
            var aprobacionGg = !string.Equals(head.Tipo, "Reemplazo", StringComparison.OrdinalIgnoreCase);

            return new SeguimientoDto
            {
                RequerimientoId       = head.GthRequerimientoId,
                Codigo                = head.Codigo,
                Puesto                = head.Puesto,
                TipoRequerimiento     = head.Tipo,
                Area                  = head.Area,
                ProyectoObra          = head.ProyectoObra,
                Justificacion         = head.Justificacion,
                FechaRequeridaIngreso = head.FechaRequeridaIngreso,
                Enviado               = head.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                EstadoCodigo          = head.EstadoCodigo,
                EstadoNombre          = head.EstadoNombre,
                EstadoOrden           = head.EstadoOrden,
                AprobacionGgRequerida = aprobacionGg,
                SustentoNombre        = head.SustentoNombre,
                SustentoUrl           = head.SustentoUrl,
                Fases                 = fases,
                SiguientePaso         = fases.FirstOrDefault(f => f.Estado == "current")?.Descripcion,
            };
        }

        /// <summary>Proyecta requerimientos (+ puesto, proyecto y estado) a filas de la tabla, en 1 roundtrip.</summary>
        private static async Task<List<SolicitudVacanteListItemDto>> ProjectRequerimientos(
            AppDbContext ctx, IQueryable<GthRequerimiento> reqs)
        {
            var raw = await (
                from r in reqs
                join p in ctx.GthPuesto on r.GthPuestoId equals p.GthPuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                orderby r.CreatedDateTime descending, r.GthRequerimientoId descending
                select new
                {
                    r.GthRequerimientoId,
                    r.Codigo,
                    Puesto = p.Nombre,
                    r.Solicitud!.Justificacion,
                    Area = r.Solicitud.AreaNombre,
                    ProyectoObra = pr.ProjectDescription,
                    r.CreatedDateTime,
                    EstadoCodigo = e.Codigo,
                    EstadoNombre = e.Nombre,
                }).ToListAsync();

            // Conversión a hora Perú en memoria (evita traducir ToOffset en el join).
            return raw.Select(x => new SolicitudVacanteListItemDto
            {
                RequerimientoId = x.GthRequerimientoId,
                Codigo          = x.Codigo,
                Puesto          = x.Puesto,
                Justificacion   = x.Justificacion,
                Area            = x.Area,
                ProyectoObra    = x.ProyectoObra,
                Enviado         = x.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                EstadoCodigo    = x.EstadoCodigo,
                EstadoNombre    = x.EstadoNombre,
            }).ToList();
        }

        // ── Configuración de destinatarios del correo de nueva solicitud ──────────
        public async Task<CorreoDestinatariosDto> GetCorreoDestinatarios()
        {
            using var ctx = _factory.CreateDbContext();
            var rows = await ctx.GthCorreoDestinatario
                .Where(d => d.State && d.Active)
                .OrderBy(d => d.Email)
                .Select(d => new { d.Email, d.EsCopia })
                .ToListAsync();

            return new CorreoDestinatariosDto
            {
                Principales = rows.Where(r => !r.EsCopia).Select(r => r.Email).ToList(),
                Copias      = rows.Where(r =>  r.EsCopia).Select(r => r.Email).ToList(),
            };
        }

        public async Task ReplaceCorreoDestinatarios(List<string> principales, List<string> copias, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;

            // Estado deseado: email -> esCopia (principal gana si estuviera en ambas, ya resuelto en el servicio).
            var deseado = new Dictionary<string, bool>();
            foreach (var e in principales) deseado[e] = false;
            foreach (var e in copias)      deseado.TryAdd(e, true);

            // Vigentes (state = true) — email único entre ellos por el índice parcial.
            var vigentes = await ctx.GthCorreoDestinatario.Where(d => d.State).ToListAsync();
            var vigentesByEmail = vigentes.ToDictionary(v => v.Email);

            // Alta o actualización en sitio (si cambia el tipo se actualiza es_copia, no se borra+inserta:
            // así nunca hay dos filas vigentes con el mismo correo y no choca con el índice único).
            foreach (var (email, esCopia) in deseado)
            {
                if (vigentesByEmail.TryGetValue(email, out var v))
                {
                    if (v.EsCopia != esCopia || !v.Active)
                    {
                        v.EsCopia = esCopia;
                        v.Active = true;
                        v.UpdatedDateTime = now;
                        v.UpdatedUserId = userId;
                    }
                }
                else
                {
                    ctx.GthCorreoDestinatario.Add(new GthCorreoDestinatario
                    {
                        Email           = email,
                        EsCopia         = esCopia,
                        Active          = true,
                        State           = true,
                        CreatedDateTime = now,
                        CreatedUserId   = userId,
                    });
                }
            }

            // Baja (soft delete) de los vigentes que ya no están en el conjunto deseado.
            foreach (var v in vigentes)
            {
                if (!deseado.ContainsKey(v.Email))
                {
                    v.State = false;
                    v.UpdatedDateTime = now;
                    v.UpdatedUserId = userId;
                }
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task<(string? AreaNombre, int? AreaScopeId, int? WorkerId)> ResolveSolicitanteInternal(AppDbContext ctx, int userId)
        {
            var w = await ctx.Worker
                .Where(x => x.Person != null && x.Person.UserId == userId)
                .Select(x => new { x.Id, x.Area, x.AreaScopeId })
                .FirstOrDefaultAsync();
            return (w?.Area, w?.AreaScopeId, w?.Id);
        }

        public async Task<SolicitudPersonalCreateResultDto> Create(GthSolicitud solicitud, List<VacanteCreateDto> vacantes, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Estado inicial del pipeline.
            var estadoNuevoId = await ctx.GthEstadoRequerimiento
                .Where(e => e.Codigo == EstadoReclutamiento.Nuevo && e.State)
                .Select(e => e.GthEstadoRequerimientoId)
                .FirstOrDefaultAsync();
            if (estadoNuevoId == 0)
                throw new AbrilException("No está configurado el estado inicial de reclutamiento (NUEVO).", 500);

            // Prioridad por defecto al crear: Media (GTH la ajusta luego desde la bandeja). Puede ser
            // null si el catálogo aún no está sembrado; no es bloqueante.
            var prioridadMediaId = await ctx.GthPrioridad
                .Where(p => p.Codigo == PrioridadReclutamiento.Media && p.State && p.Active)
                .Select(p => (int?)p.GthPrioridadId)
                .FirstOrDefaultAsync();

            // Validar que los ids referenciados existan y estén vigentes.
            var puestoIds  = vacantes.Select(v => v.PuestoId).Distinct().ToList();
            var tipoIds    = vacantes.Select(v => v.TipoRequerimientoId).Distinct().ToList();
            var projectIds = vacantes.Select(v => v.ProjectId).Distinct().ToList();

            var puestosOk = await ctx.GthPuesto.CountAsync(p => puestoIds.Contains(p.GthPuestoId) && p.State && p.Active);
            if (puestosOk != puestoIds.Count)
                throw new AbrilException("Uno o más puestos seleccionados no son válidos.", 400);

            var tiposOk = await ctx.GthTipoRequerimiento.CountAsync(t => tipoIds.Contains(t.GthTipoRequerimientoId) && t.State && t.Active);
            if (tiposOk != tipoIds.Count)
                throw new AbrilException("Uno o más tipos de requerimiento no son válidos.", 400);

            var projectsOk = await ctx.Project.CountAsync(p => projectIds.Contains(p.ProjectId) && p.State && p.Active);
            if (projectsOk != projectIds.Count)
                throw new AbrilException("Uno o más proyectos/obras seleccionados no son válidos.", 400);

            // Correlativo anual del código REQ-AAAA-NNNN (año en hora Perú, UTC-5).
            var now = DateTimeOffset.UtcNow;
            var anio = now.ToOffset(TimeSpan.FromHours(-5)).Year;
            var maxNumero = await ctx.GthRequerimiento
                .Where(r => r.Anio == anio)
                .Select(r => (int?)r.Numero)
                .MaxAsync() ?? 0;

            solicitud.CreatedDateTime = now;
            solicitud.CreatedUserId   = userId;
            solicitud.Active          = true;
            solicitud.State           = true;

            var codigos = new List<string>(vacantes.Count);
            foreach (var v in vacantes)
            {
                maxNumero++;
                var codigo = $"REQ-{anio}-{maxNumero:D4}";
                codigos.Add(codigo);
                solicitud.Requerimientos.Add(new GthRequerimiento
                {
                    Codigo                   = codigo,
                    Anio                     = anio,
                    Numero                   = maxNumero,
                    GthPuestoId              = v.PuestoId,
                    GthTipoRequerimientoId   = v.TipoRequerimientoId,
                    ProjectId                = v.ProjectId,
                    FechaRequeridaIngreso    = v.FechaRequeridaIngreso,
                    GthEstadoRequerimientoId = estadoNuevoId,
                    GthPrioridadId           = prioridadMediaId,
                    CreatedDateTime          = now,
                    CreatedUserId            = userId,
                    Active                   = true,
                    State                    = true,
                });
            }

            ctx.GthSolicitud.Add(solicitud);
            await ctx.SaveChangesAsync();

            return new SolicitudPersonalCreateResultDto
            {
                SolicitudId = solicitud.GthSolicitudId,
                Codigos     = codigos,
            };
        }
    }

    /// <summary>Códigos estables de estados de reclutamiento (espejo de gth_estado_requerimiento.codigo).</summary>
    internal static class EstadoReclutamiento
    {
        public const string Nuevo       = "NUEVO";
        public const string Publicacion = "PUBLICACION";
        public const string LongList    = "LONG_LIST";
    }

    /// <summary>Códigos estables de prioridad de reclutamiento (espejo de gth_prioridad.codigo).</summary>
    internal static class PrioridadReclutamiento
    {
        public const string Media = "MEDIA";
    }
}
