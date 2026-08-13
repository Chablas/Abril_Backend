using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    /// <summary>
    /// Aprobación de Gerencia General de la solicitud de personal. Todas las lecturas de la página
    /// pública se sirven en un solo roundtrip (cabecera + vacantes) porque el GG entra desde el
    /// correo y no navega por la app.
    /// </summary>
    public class AprobacionGgRepository : IAprobacionGgRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public AprobacionGgRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Los timestamps se guardan en UTC y se sirven al frontend en hora de Perú.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        public async Task<AprobacionGgEnvioContextoDto> PrepararEnvio(int solicitudId, string nuevoToken, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var solicitud = await ctx.GthSolicitud
                .FirstOrDefaultAsync(s => s.GthSolicitudId == solicitudId && s.State);
            if (solicitud == null)
                throw new AbrilException("Solicitud de personal no encontrada.", 404);

            // Idempotente: si ya hay una aprobación vigente se reutiliza (mismo token, para que el
            // enlace que ya viajó por correo siga funcionando).
            var aprobacion = await ctx.GthAprobacionGg
                .FirstOrDefaultAsync(a => a.GthSolicitudId == solicitudId && a.State);

            var now = DateTimeOffset.UtcNow;
            var esNueva = aprobacion == null;

            if (aprobacion == null)
            {
                var pendienteId = await ResolveEstadoId(ctx, AprobacionGgEstadoCodigo.Pendiente);
                aprobacion = new GthAprobacionGg
                {
                    GthSolicitudId          = solicitudId,
                    Token                   = nuevoToken,
                    GthAprobacionGgEstadoId = pendienteId,
                    CreatedDateTime         = now,
                    CreatedUserId           = userId,
                    Active                  = true,
                    State                   = true,
                };
                ctx.GthAprobacionGg.Add(aprobacion);
                await ctx.SaveChangesAsync();
            }

            // Detalle: una fila por vacante vigente de la solicitud (las que falten se agregan).
            var requerimientoIds = await ctx.GthRequerimiento
                .Where(r => r.GthSolicitudId == solicitudId && r.State)
                .Select(r => r.GthRequerimientoId)
                .ToListAsync();

            // En una aprobación recién creada no hay detalle que consultar (el caso normal al
            // registrar la solicitud); solo se revisa cuando se reutiliza una ya existente.
            var yaEnDetalle = esNueva
                ? new List<int>()
                : await ctx.GthAprobacionGgDetalle
                    .Where(d => d.GthAprobacionGgId == aprobacion.GthAprobacionGgId && d.State)
                    .Select(d => d.GthRequerimientoId)
                    .ToListAsync();

            var faltantes = requerimientoIds.Except(yaEnDetalle).ToList();
            if (faltantes.Count > 0)
            {
                foreach (var reqId in faltantes)
                {
                    ctx.GthAprobacionGgDetalle.Add(new GthAprobacionGgDetalle
                    {
                        GthAprobacionGgId  = aprobacion.GthAprobacionGgId,
                        GthRequerimientoId = reqId,
                        Aprobado           = null,
                        CreatedDateTime    = now,
                        CreatedUserId      = userId,
                        Active             = true,
                        State              = true,
                    });
                }
                await ctx.SaveChangesAsync();
            }

            return await BuildEnvioContexto(ctx, aprobacion, solicitud);
        }

        public async Task<AprobacionGgEnvioContextoDto?> GetEnvioContextoByRequerimiento(int requerimientoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Scope: solo el solicitante dueño de la solicitud puede reenviar su propio correo. Se
            // traen las dos entidades (aprobación y solicitud) en un roundtrip; EF las rastrea
            // aunque vengan dentro de un anónimo.
            var par = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId
                      && r.State && r.Solicitud!.State
                      && r.Solicitud.SolicitanteUserId == userId
                join a in ctx.GthAprobacionGg on r.GthSolicitudId equals a.GthSolicitudId
                where a.State
                select new { Aprobacion = a, Solicitud = r.Solicitud! }).FirstOrDefaultAsync();

            if (par == null) return null;

            return await BuildEnvioContexto(ctx, par.Aprobacion, par.Solicitud);
        }

        /// <summary>Cabecera de la solicitud + vacantes con su decisión, para el correo del GG.</summary>
        private static async Task<AprobacionGgEnvioContextoDto> BuildEnvioContexto(
            AppDbContext ctx, GthAprobacionGg aprobacion, GthSolicitud solicitud)
        {
            var solicitanteNombre = solicitud.SolicitanteUserId.HasValue
                ? await ctx.Worker
                    .Where(w => w.Person != null && w.Person.UserId == solicitud.SolicitanteUserId.Value)
                    .Select(w => w.Person!.FullName ?? w.ApellidoNombre)
                    .FirstOrDefaultAsync()
                : null;

            return new AprobacionGgEnvioContextoDto
            {
                SolicitudId       = aprobacion.GthSolicitudId,
                AprobacionId      = aprobacion.GthAprobacionGgId,
                Token             = aprobacion.Token,
                Area              = solicitud.AreaNombre,
                AreaScopeId       = solicitud.AreaScopeId,
                SolicitanteNombre = solicitanteNombre,
                Justificacion     = solicitud.Justificacion,
                SustentoNombre    = solicitud.SustentoNombre,
                SustentoUrl       = solicitud.SustentoUrl,
                // La fecha de decisión y el estado se escriben juntos, así que basta con una para
                // saber si ya se decidió (evita un join extra solo para leer el código del estado).
                Decidida          = aprobacion.DecididoDateTime.HasValue,
                Vacantes          = await QueryVacantes(ctx, aprobacion.GthAprobacionGgId, aprobacion.GthSolicitudId),
            };
        }

        /// <summary>
        /// Vacantes vigentes de la solicitud con la decisión que el GG registró en el detalle
        /// (left join: una vacante sin fila de detalle queda como "sin decidir").
        /// </summary>
        private static async Task<List<AprobacionGgVacanteDto>> QueryVacantes(
            AppDbContext ctx, int aprobacionId, int solicitudId)
        {
            return await (
                from r in ctx.GthRequerimiento
                where r.GthSolicitudId == solicitudId && r.State
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join t in ctx.GthTipoRequerimiento on r.GthTipoRequerimientoId equals t.GthTipoRequerimientoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join d in ctx.GthAprobacionGgDetalle.Where(x => x.GthAprobacionGgId == aprobacionId && x.State)
                    on r.GthRequerimientoId equals d.GthRequerimientoId into detalleJoin
                from d in detalleJoin.DefaultIfEmpty()
                orderby r.GthRequerimientoId
                select new AprobacionGgVacanteDto
                {
                    RequerimientoId       = r.GthRequerimientoId,
                    Codigo                = r.Codigo,
                    Puesto                = p.Nombre,
                    TipoRequerimiento     = t.Nombre,
                    ProyectoObra          = pr.ProjectDescription,
                    FechaRequeridaIngreso = r.FechaRequeridaIngreso,
                    Aprobado              = d != null ? d.Aprobado : null,
                }).ToListAsync();
        }

        public async Task RegistrarEnvio(int aprobacionId, List<string> principales, List<string> copias, bool esReenvio, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var aprobacion = await ctx.GthAprobacionGg
                .FirstOrDefaultAsync(a => a.GthAprobacionGgId == aprobacionId && a.State);
            if (aprobacion == null) return; // la aprobación se dio de baja mientras se enviaba el correo

            var now = DateTimeOffset.UtcNow;
            aprobacion.CorreoEnvio = principales.Count > 0 ? string.Join("; ", principales) : null;
            aprobacion.CorreoCopia = copias.Count > 0 ? string.Join("; ", copias) : null;

            if (esReenvio) aprobacion.ReenviadoDateTime = now;
            // El primer envío exitoso marca enviado_date_time; los reenvíos no lo mueven.
            aprobacion.EnviadoDateTime ??= now;

            aprobacion.UpdatedDateTime = now;
            aprobacion.UpdatedUserId   = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task<AprobacionGgPublicoDto?> GetPublicoByToken(string token)
        {
            using var ctx = _factory.CreateDbContext();

            var head = await (
                from a in ctx.GthAprobacionGg
                where a.Token == token && a.State
                join s in ctx.GthSolicitud on a.GthSolicitudId equals s.GthSolicitudId
                where s.State
                join e in ctx.GthAprobacionGgEstado on a.GthAprobacionGgEstadoId equals e.GthAprobacionGgEstadoId
                select new
                {
                    a.GthAprobacionGgId,
                    a.GthSolicitudId,
                    a.DecididoDateTime,
                    a.Comentario,
                    EstadoCodigo = e.Codigo,
                    EstadoNombre = e.Nombre,
                    s.AreaNombre,
                    s.Justificacion,
                    s.SustentoNombre,
                    s.SustentoUrl,
                    s.SolicitanteUserId,
                    s.CreatedDateTime,
                }).FirstOrDefaultAsync();

            if (head == null) return null;

            var solicitanteNombre = head.SolicitanteUserId.HasValue
                ? await ctx.Worker
                    .Where(w => w.Person != null && w.Person.UserId == head.SolicitanteUserId.Value)
                    .Select(w => w.Person!.FullName ?? w.ApellidoNombre)
                    .FirstOrDefaultAsync()
                : null;

            return new AprobacionGgPublicoDto
            {
                Area              = head.AreaNombre,
                SolicitanteNombre = solicitanteNombre,
                Justificacion     = head.Justificacion,
                SustentoNombre    = head.SustentoNombre,
                SustentoUrl       = head.SustentoUrl,
                Enviado           = head.CreatedDateTime.ToOffset(PeruOffset).DateTime,
                EstadoCodigo      = head.EstadoCodigo,
                EstadoNombre      = head.EstadoNombre,
                Decidida          = head.EstadoCodigo != AprobacionGgEstadoCodigo.Pendiente,
                DecididoEn        = head.DecididoDateTime?.ToOffset(PeruOffset).DateTime,
                Comentario        = head.Comentario,
                Vacantes          = await QueryVacantes(ctx, head.GthAprobacionGgId, head.GthSolicitudId),
            };
        }

        public async Task<AprobacionGgDecisionContextoDto> RegistrarDecision(string token, AprobacionGgDecisionDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var aprobacion = await ctx.GthAprobacionGg
                .FirstOrDefaultAsync(a => a.Token == token && a.State);
            if (aprobacion == null)
                throw new AbrilException("El enlace de aprobación no es válido o ya no está disponible.", 404);

            var estadoActual = await ctx.GthAprobacionGgEstado
                .Where(e => e.GthAprobacionGgEstadoId == aprobacion.GthAprobacionGgEstadoId)
                .Select(e => e.Codigo)
                .FirstOrDefaultAsync();
            // Se decide una sola vez: el enlace puede reenviarse por correo, así que no debe poder
            // cambiar una decisión ya tomada.
            if (estadoActual != AprobacionGgEstadoCodigo.Pendiente)
                throw new AbrilException("Esta solicitud ya fue decidida por Gerencia General.", 409);

            var solicitud = await ctx.GthSolicitud
                .FirstOrDefaultAsync(s => s.GthSolicitudId == aprobacion.GthSolicitudId && s.State)
                ?? throw new AbrilException("La solicitud de personal ya no está disponible.", 404);

            // Vacantes vigentes de la solicitud (entidades: se les cambia el estado).
            var requerimientos = await ctx.GthRequerimiento
                .Where(r => r.GthSolicitudId == aprobacion.GthSolicitudId && r.State)
                .OrderBy(r => r.GthRequerimientoId)
                .ToListAsync();
            if (requerimientos.Count == 0)
                throw new AbrilException("La solicitud no tiene vacantes por aprobar.", 400);

            // La decisión debe cubrir exactamente a las vacantes vigentes.
            var decisionPorId = new Dictionary<int, bool>();
            foreach (var d in dto.Decisiones) decisionPorId[d.RequerimientoId] = d.Aprobado;
            if (requerimientos.Any(r => !decisionPorId.ContainsKey(r.GthRequerimientoId)))
                throw new AbrilException("Debes aprobar o rechazar todas las vacantes de la solicitud.", 400);

            // Estados destino del requerimiento: aprobada → VALIDACION_GTH (pasa a GTH);
            // rechazada → RECHAZADO_GG (terminal).
            var estadosReq = await ctx.GthEstadoRequerimiento
                .Where(e => e.State && (e.Codigo == EstadoReclutamiento.ValidacionGth
                                        || e.Codigo == EstadoReclutamiento.RechazadoGg))
                .ToListAsync();
            var validacionGth = estadosReq.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.ValidacionGth)
                ?? throw new AbrilException("No está configurado el estado VALIDACION_GTH de reclutamiento.", 500);
            var rechazadoGg = estadosReq.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.RechazadoGg)
                ?? throw new AbrilException("No está configurado el estado RECHAZADO_GG de reclutamiento.", 500);

            var detalles = await ctx.GthAprobacionGgDetalle
                .Where(d => d.GthAprobacionGgId == aprobacion.GthAprobacionGgId && d.State)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            int aprobados = 0, rechazados = 0;

            foreach (var r in requerimientos)
            {
                var aprobado = decisionPorId[r.GthRequerimientoId];
                if (aprobado) aprobados++; else rechazados++;

                r.GthEstadoRequerimientoId = aprobado
                    ? validacionGth.GthEstadoRequerimientoId
                    : rechazadoGg.GthEstadoRequerimientoId;
                // Sin UpdatedUserId a propósito: quien decide es el GG desde el enlace del correo,
                // sin sesión. La traza de quién pudo decidir es gth_aprobacion_gg.correo_envio.
                r.UpdatedDateTime = now;

                var detalle = detalles.FirstOrDefault(d => d.GthRequerimientoId == r.GthRequerimientoId);
                if (detalle == null)
                {
                    ctx.GthAprobacionGgDetalle.Add(new GthAprobacionGgDetalle
                    {
                        GthAprobacionGgId  = aprobacion.GthAprobacionGgId,
                        GthRequerimientoId = r.GthRequerimientoId,
                        Aprobado           = aprobado,
                        DecididoDateTime   = now,
                        CreatedDateTime    = now,
                        Active             = true,
                        State              = true,
                    });
                }
                else
                {
                    detalle.Aprobado         = aprobado;
                    detalle.DecididoDateTime = now;
                    detalle.UpdatedDateTime  = now;
                }
            }

            var codigoEstado = aprobados == 0 ? AprobacionGgEstadoCodigo.Rechazada
                             : rechazados == 0 ? AprobacionGgEstadoCodigo.Aprobada
                             : AprobacionGgEstadoCodigo.AprobadaParcial;
            var estadoDestino = await ctx.GthAprobacionGgEstado
                .FirstOrDefaultAsync(e => e.Codigo == codigoEstado && e.State)
                ?? throw new AbrilException($"No está configurado el estado {codigoEstado} de la aprobación de Gerencia General.", 500);

            aprobacion.GthAprobacionGgEstadoId = estadoDestino.GthAprobacionGgEstadoId;
            aprobacion.DecididoDateTime        = now;
            aprobacion.Comentario              = string.IsNullOrWhiteSpace(dto.Comentario) ? null : dto.Comentario.Trim();
            aprobacion.UpdatedDateTime         = now;

            var solicitanteNombre = solicitud.SolicitanteUserId.HasValue
                ? await ctx.Worker
                    .Where(w => w.Person != null && w.Person.UserId == solicitud.SolicitanteUserId.Value)
                    .Select(w => w.Person!.FullName ?? w.ApellidoNombre)
                    .FirstOrDefaultAsync()
                : null;

            await ctx.SaveChangesAsync();

            // Vacantes con sus datos legibles para el correo a GTH (una sola consulta para ambas listas).
            var vacantes = await QueryVacantes(ctx, aprobacion.GthAprobacionGgId, aprobacion.GthSolicitudId);

            return new AprobacionGgDecisionContextoDto
            {
                Resultado = new AprobacionGgDecisionResultDto
                {
                    EstadoCodigo = estadoDestino.Codigo,
                    EstadoNombre = estadoDestino.Nombre,
                    Aprobados    = aprobados,
                    Rechazados   = rechazados,
                },
                SolicitudId       = aprobacion.GthSolicitudId,
                Area              = solicitud.AreaNombre,
                SolicitanteNombre = solicitanteNombre,
                Justificacion     = solicitud.Justificacion,
                SustentoNombre    = solicitud.SustentoNombre,
                SustentoUrl       = solicitud.SustentoUrl,
                Comentario        = aprobacion.Comentario,
                Aprobadas         = vacantes.Where(v => v.Aprobado == true).ToList(),
                Rechazadas        = vacantes.Where(v => v.Aprobado == false).ToList(),
            };
        }

        public async Task<AprobacionGgResumenDto?> GetResumenByRequerimiento(int requerimientoId)
        {
            using var ctx = _factory.CreateDbContext();

            // Left join al detalle: la aprobación es de la solicitud completa, pero la tarjeta
            // muestra además la decisión de ESTA vacante.
            var raw = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId && r.State
                join a in ctx.GthAprobacionGg on r.GthSolicitudId equals a.GthSolicitudId
                where a.State
                join e in ctx.GthAprobacionGgEstado on a.GthAprobacionGgEstadoId equals e.GthAprobacionGgEstadoId
                join d in ctx.GthAprobacionGgDetalle.Where(x => x.State)
                    on new { A = a.GthAprobacionGgId, R = r.GthRequerimientoId }
                    equals new { A = d.GthAprobacionGgId, R = d.GthRequerimientoId } into detalleJoin
                from d in detalleJoin.DefaultIfEmpty()
                select new
                {
                    EstadoCodigo = e.Codigo,
                    EstadoNombre = e.Nombre,
                    Aprobado     = d != null ? d.Aprobado : null,
                    a.Comentario,
                    a.EnviadoDateTime,
                    a.DecididoDateTime,
                }).FirstOrDefaultAsync();

            if (raw == null) return null;

            // Conversión a hora Perú en memoria (ToOffset no se traduce a SQL).
            return new AprobacionGgResumenDto
            {
                EstadoCodigo = raw.EstadoCodigo,
                EstadoNombre = raw.EstadoNombre,
                Aprobado     = raw.Aprobado,
                Comentario   = raw.Comentario,
                EnviadoEn    = raw.EnviadoDateTime?.ToOffset(PeruOffset).DateTime,
                DecididoEn   = raw.DecididoDateTime?.ToOffset(PeruOffset).DateTime,
            };
        }

        /// <summary>Resuelve el id de un estado de la aprobación por su código estable; 500 si no está sembrado.</summary>
        private static async Task<int> ResolveEstadoId(AppDbContext ctx, string codigo)
        {
            var id = await ctx.GthAprobacionGgEstado
                .Where(e => e.Codigo == codigo && e.State)
                .Select(e => (int?)e.GthAprobacionGgEstadoId)
                .FirstOrDefaultAsync();
            if (id == null)
                throw new AbrilException($"No está configurado el estado {codigo} de la aprobación de Gerencia General.", 500);
            return id.Value;
        }
    }

    /// <summary>Códigos estables del estado de la aprobación (espejo de gth_aprobacion_gg_estado.codigo).</summary>
    internal static class AprobacionGgEstadoCodigo
    {
        public const string Pendiente       = "PENDIENTE";
        public const string Aprobada        = "APROBADA";
        public const string AprobadaParcial = "APROBADA_PARCIAL";
        public const string Rechazada       = "RECHAZADA";
    }
}
