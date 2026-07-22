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
        public const string Nuevo = "NUEVO";
    }
}
