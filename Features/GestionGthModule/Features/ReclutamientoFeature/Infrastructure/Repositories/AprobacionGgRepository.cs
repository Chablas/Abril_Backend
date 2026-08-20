using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    /// <summary>
    /// Aprobación de la solicitud de personal en sus dos niveles (gerente del área y Gerencia
    /// General). La pantalla «Aprobaciones» se sirve en dos roundtrips (cabeceras + vacantes de
    /// todas ellas) y el detalle de una aprobación en uno solo (cabecera + sus vacantes): el
    /// gerente entra desde el correo a decidir, no a navegar.
    ///
    /// Todo lo que sale de acá está filtrado por el <see cref="AprobacionScope"/> del usuario: el
    /// Gerente General ve todas las solicitudes, un gerente de área solo las de su
    /// <c>area_scope</c> hacia abajo, y cualquier otra categoría no ve ninguna.
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
                // Las dos casillas nacen pendientes: el correo les llega a ambos gerentes a la vez
                // y cualquiera puede decidir primero.
                var pendienteId = await ResolveEstadoId(ctx, AprobacionGgEstadoCodigo.Pendiente);
                aprobacion = new GthAprobacionGg
                {
                    GthSolicitudId         = solicitudId,
                    Token                  = nuevoToken,
                    EstadoGerenteGeneralId = pendienteId,
                    EstadoGerenteAreaId    = pendienteId,
                    CreatedDateTime        = now,
                    CreatedUserId          = userId,
                    Active                 = true,
                    State                  = true,
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
                        GthAprobacionGgId      = aprobacion.GthAprobacionGgId,
                        GthRequerimientoId     = reqId,
                        AprobadoGerenteGeneral = null,
                        AprobadoGerenteArea    = null,
                        CreatedDateTime        = now,
                        CreatedUserId          = userId,
                        Active                 = true,
                        State                  = true,
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

        /// <summary>Cabecera de la solicitud + vacantes con su decisión, para el correo a los gerentes.</summary>
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
                Area              = solicitud.AreaNombre,
                AreaScopeId       = solicitud.AreaScopeId,
                SolicitanteNombre = solicitanteNombre,
                Justificacion     = solicitud.Justificacion,
                SustentoNombre    = solicitud.SustentoNombre,
                SustentoUrl       = solicitud.SustentoUrl,
                // La fecha de decisión y el estado se escriben juntos, así que basta con una para
                // saber si el GG ya cerró la solicitud (evita un join extra solo para leer el
                // código del estado). El visto bueno del área no cuenta: no cierra nada.
                Decidida          = aprobacion.GerenteGeneralDecididoDateTime.HasValue,
                Vacantes          = await QueryVacantes(ctx, aprobacion.GthAprobacionGgId, aprobacion.GthSolicitudId),
            };
        }

        /// <summary>
        /// Vacantes vigentes de la solicitud con la decisión de cada nivel registrada en el detalle
        /// (left join: una vacante sin fila de detalle queda como "sin decidir" en ambos).
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
                // Trabajador reemplazado: left join porque solo lo tienen las vacantes de tipo
                // Reemplazo registradas desde que se pide ese dato.
                join wr in ctx.Worker on r.ReemplazaWorkerId equals (int?)wr.Id into reemplazaJoin
                from wr in reemplazaJoin.DefaultIfEmpty()
                orderby r.GthRequerimientoId
                select new AprobacionGgVacanteDto
                {
                    RequerimientoId        = r.GthRequerimientoId,
                    Codigo                 = r.Codigo,
                    Puesto                 = p.Nombre,
                    TipoRequerimiento      = t.Nombre,
                    TrabajadorReemplazado  = wr == null ? null
                        : (wr.Person != null ? wr.Person.FullName : wr.ApellidoNombre),
                    ProyectoObra           = pr.ProjectDescription,
                    SalarioBrutoMensual    = r.SalarioBrutoMensual,
                    AprobadoGerenteArea    = d != null ? d.AprobadoGerenteArea : null,
                    AprobadoGerenteGeneral = d != null ? d.AprobadoGerenteGeneral : null,
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

        public async Task<AprobacionGgBandejaDto> GetBandeja(AprobacionScope scope)
        {
            var bandeja = new AprobacionGgBandejaDto
            {
                Nivel       = scope.Nivel,
                AreaAlcance = scope.AreaNombre,
            };

            // Sin nivel no hay nada que mostrar: la pantalla se abre por rol, pero las solicitudes
            // se ven por categoría de trabajador. Ni siquiera se consulta la BD.
            if (!scope.PuedeDecidir) return bandeja;

            using var ctx = _factory.CreateDbContext();

            // EF traduce Contains sobre una lista; el HashSet del scope se materializa una vez.
            var areaIds = scope.AreaScopeIds.ToList();

            // 1) Cabeceras: una fila por solicitud dentro del alcance del usuario. Los nombres del
            //    solicitante y de quienes decidieron salen por left join a person (user_id es 1:1
            //    con app_user), no por consulta suelta: evita el N+1 de la lista completa.
            var cabeceras = await (
                from a in ctx.GthAprobacionGg.AsNoTracking()
                where a.State
                join s in ctx.GthSolicitud.AsNoTracking() on a.GthSolicitudId equals s.GthSolicitudId
                where s.State
                      // Alcance: el GG no filtra; el gerente de área solo ve su subárbol. Una
                      // solicitud sin area_scope (no se pudo resolver al registrarla) queda fuera
                      // del alcance de cualquier gerente de área: no hay a qué gerencia atribuirla.
                      && (scope.VeTodo || (s.AreaScopeId != null && areaIds.Contains(s.AreaScopeId.Value)))
                join egg in ctx.GthAprobacionGgEstado.AsNoTracking() on a.EstadoGerenteGeneralId equals egg.GthAprobacionGgEstadoId
                join ega in ctx.GthAprobacionGgEstado.AsNoTracking() on a.EstadoGerenteAreaId equals ega.GthAprobacionGgEstadoId
                join ps in ctx.Person.AsNoTracking() on s.SolicitanteUserId equals ps.UserId into solicitanteJoin
                from ps in solicitanteJoin.DefaultIfEmpty()
                join pgg in ctx.Person.AsNoTracking() on a.GerenteGeneralDecididoUserId equals pgg.UserId into ggJoin
                from pgg in ggJoin.DefaultIfEmpty()
                join pga in ctx.Person.AsNoTracking() on a.GerenteAreaDecididoUserId equals pga.UserId into gaJoin
                from pga in gaJoin.DefaultIfEmpty()
                orderby a.GthAprobacionGgId descending
                select new
                {
                    a.GthAprobacionGgId,
                    s.AreaNombre,
                    s.Justificacion,
                    s.CreatedDateTime,
                    SolicitanteNombre = ps != null ? ps.FullName : null,

                    GgEstadoCodigo = egg.Codigo,
                    GgEstadoNombre = egg.Nombre,
                    GgDecididoEn   = a.GerenteGeneralDecididoDateTime,
                    GgDecididoPor  = pgg != null ? pgg.FullName : null,
                    GgComentario   = a.GerenteGeneralComentario,

                    GaEstadoCodigo = ega.Codigo,
                    GaEstadoNombre = ega.Nombre,
                    GaDecididoEn   = a.GerenteAreaDecididoDateTime,
                    GaDecididoPor  = pga != null ? pga.FullName : null,
                    GaComentario   = a.GerenteAreaComentario,
                }).ToListAsync();

            if (cabeceras.Count == 0) return bandeja;

            // 2) Vacantes de todas esas solicitudes de una sola vez (código + las dos decisiones).
            //    Se parte de gth_requerimiento y el detalle entra por left join —igual que
            //    QueryVacantes, para que la lista y el modal no puedan contar distinto si a una
            //    vacante le falta su fila de detalle. La llave del join es el par (aprobación,
            //    requerimiento): filtrar solo por requerimiento traería el detalle de una
            //    aprobación dada de baja.
            var ids = cabeceras.Select(c => c.GthAprobacionGgId).ToList();
            var vacantes = await (
                from a in ctx.GthAprobacionGg.AsNoTracking()
                where ids.Contains(a.GthAprobacionGgId)
                join r in ctx.GthRequerimiento.AsNoTracking() on a.GthSolicitudId equals r.GthSolicitudId
                where r.State
                join d in ctx.GthAprobacionGgDetalle.AsNoTracking().Where(x => x.State)
                    on new { A = a.GthAprobacionGgId, R = r.GthRequerimientoId }
                    equals new { A = d.GthAprobacionGgId, R = d.GthRequerimientoId } into detalleJoin
                from d in detalleJoin.DefaultIfEmpty()
                orderby r.GthRequerimientoId
                select new
                {
                    a.GthAprobacionGgId,
                    r.Codigo,
                    AprobadoGg = d != null ? d.AprobadoGerenteGeneral : null,
                    AprobadoGa = d != null ? d.AprobadoGerenteArea : null,
                }).ToListAsync();

            var porAprobacion = vacantes.ToLookup(v => v.GthAprobacionGgId);
            var esGg = scope.Nivel == AprobacionNivel.GerenteGeneral;

            bandeja.Aprobaciones = cabeceras.Select(c =>
            {
                var vs = porAprobacion[c.GthAprobacionGgId].ToList();

                var gg = new AprobacionNivelResumenDto
                {
                    EstadoCodigo       = c.GgEstadoCodigo,
                    EstadoNombre       = c.GgEstadoNombre,
                    Decidida           = c.GgEstadoCodigo != AprobacionGgEstadoCodigo.Pendiente,
                    DecididoEn         = c.GgDecididoEn?.ToOffset(PeruOffset).DateTime,
                    DecididoPor        = c.GgDecididoPor,
                    Comentario         = c.GgComentario,
                    VacantesAprobadas  = vs.Count(v => v.AprobadoGg == true),
                    VacantesRechazadas = vs.Count(v => v.AprobadoGg == false),
                };

                var ga = new AprobacionNivelResumenDto
                {
                    EstadoCodigo       = c.GaEstadoCodigo,
                    EstadoNombre       = c.GaEstadoNombre,
                    Decidida           = c.GaEstadoCodigo != AprobacionGgEstadoCodigo.Pendiente,
                    DecididoEn         = c.GaDecididoEn?.ToOffset(PeruOffset).DateTime,
                    DecididoPor        = c.GaDecididoPor,
                    Comentario         = c.GaComentario,
                    VacantesAprobadas  = vs.Count(v => v.AprobadoGa == true),
                    VacantesRechazadas = vs.Count(v => v.AprobadoGa == false),
                };

                return new AprobacionGgBandejaItemDto
                {
                    AprobacionId      = c.GthAprobacionGgId,
                    Codigos           = string.Join(", ", vs.Select(v => v.Codigo)),
                    Area              = c.AreaNombre,
                    SolicitanteNombre = c.SolicitanteNombre,
                    Justificacion     = c.Justificacion,
                    Enviado           = c.CreatedDateTime.ToOffset(PeruOffset).DateTime,
                    TotalVacantes     = vs.Count,
                    GerenteGeneral    = gg,
                    GerenteArea       = ga,
                    EsperaMiDecision  = esGg ? !gg.Decidida : !ga.Decidida,
                };
            }).ToList();

            // 3) Tarjetas: siempre contra la casilla del usuario que consulta. "Por aprobar" es lo
            //    que espera SU firma, no la del otro nivel.
            var mias = bandeja.Aprobaciones
                .Select(i => new { Item = i, Mi = esGg ? i.GerenteGeneral : i.GerenteArea })
                .ToList();

            bandeja.Resumen = new AprobacionGgBandejaResumenDto
            {
                Pendientes         = mias.Count(x => !x.Mi.Decidida),
                VacantesPendientes = mias.Where(x => !x.Mi.Decidida).Sum(x => x.Item.TotalVacantes),
                // "Aprobadas" incluye las parciales: en ambas hay vacantes que sí tuvieron el visto bueno.
                Aprobadas          = mias.Count(x => x.Mi.Decidida && x.Mi.VacantesAprobadas > 0),
                Rechazadas         = mias.Count(x => x.Mi.Decidida && x.Mi.VacantesAprobadas == 0),
            };

            return bandeja;
        }

        public async Task<AprobacionGgDetalleDto?> GetDetalle(int aprobacionId, AprobacionScope scope)
        {
            using var ctx = _factory.CreateDbContext();

            var head = await (
                from a in ctx.GthAprobacionGg.AsNoTracking()
                where a.GthAprobacionGgId == aprobacionId && a.State
                join s in ctx.GthSolicitud.AsNoTracking() on a.GthSolicitudId equals s.GthSolicitudId
                where s.State
                join egg in ctx.GthAprobacionGgEstado.AsNoTracking() on a.EstadoGerenteGeneralId equals egg.GthAprobacionGgEstadoId
                join ega in ctx.GthAprobacionGgEstado.AsNoTracking() on a.EstadoGerenteAreaId equals ega.GthAprobacionGgEstadoId
                join ps in ctx.Person.AsNoTracking() on s.SolicitanteUserId equals ps.UserId into solicitanteJoin
                from ps in solicitanteJoin.DefaultIfEmpty()
                join pgg in ctx.Person.AsNoTracking() on a.GerenteGeneralDecididoUserId equals pgg.UserId into ggJoin
                from pgg in ggJoin.DefaultIfEmpty()
                join pga in ctx.Person.AsNoTracking() on a.GerenteAreaDecididoUserId equals pga.UserId into gaJoin
                from pga in gaJoin.DefaultIfEmpty()
                select new
                {
                    a.GthAprobacionGgId,
                    a.GthSolicitudId,
                    s.AreaScopeId,
                    s.AreaNombre,
                    s.Justificacion,
                    s.SustentoNombre,
                    s.SustentoUrl,
                    s.CreatedDateTime,
                    SolicitanteNombre = ps != null ? ps.FullName : null,

                    GgEstadoCodigo = egg.Codigo,
                    GgEstadoNombre = egg.Nombre,
                    GgDecididoEn   = a.GerenteGeneralDecididoDateTime,
                    GgDecididoPor  = pgg != null ? pgg.FullName : null,
                    GgComentario   = a.GerenteGeneralComentario,

                    GaEstadoCodigo = ega.Codigo,
                    GaEstadoNombre = ega.Nombre,
                    GaDecididoEn   = a.GerenteAreaDecididoDateTime,
                    GaDecididoPor  = pga != null ? pga.FullName : null,
                    GaComentario   = a.GerenteAreaComentario,
                }).FirstOrDefaultAsync();

            if (head == null) return null;

            // El enlace del correo lleva un id de aprobación: un gerente de área que reciba (o
            // reenvíen) uno de otra gerencia no puede abrirlo. Se distingue de "no existe" a
            // propósito: es un caso real y el mensaje tiene que explicarlo.
            EnsureAlcance(scope, head.AreaScopeId);

            var vacantes = await QueryVacantes(ctx, head.GthAprobacionGgId, head.GthSolicitudId);

            var gg = new AprobacionNivelResumenDto
            {
                EstadoCodigo       = head.GgEstadoCodigo,
                EstadoNombre       = head.GgEstadoNombre,
                Decidida           = head.GgEstadoCodigo != AprobacionGgEstadoCodigo.Pendiente,
                DecididoEn         = head.GgDecididoEn?.ToOffset(PeruOffset).DateTime,
                DecididoPor        = head.GgDecididoPor,
                Comentario         = head.GgComentario,
                VacantesAprobadas  = vacantes.Count(v => v.AprobadoGerenteGeneral == true),
                VacantesRechazadas = vacantes.Count(v => v.AprobadoGerenteGeneral == false),
            };

            var ga = new AprobacionNivelResumenDto
            {
                EstadoCodigo       = head.GaEstadoCodigo,
                EstadoNombre       = head.GaEstadoNombre,
                Decidida           = head.GaEstadoCodigo != AprobacionGgEstadoCodigo.Pendiente,
                DecididoEn         = head.GaDecididoEn?.ToOffset(PeruOffset).DateTime,
                DecididoPor        = head.GaDecididoPor,
                Comentario         = head.GaComentario,
                VacantesAprobadas  = vacantes.Count(v => v.AprobadoGerenteArea == true),
                VacantesRechazadas = vacantes.Count(v => v.AprobadoGerenteArea == false),
            };

            var miCasilla = scope.Nivel == AprobacionNivel.GerenteGeneral ? gg : ga;

            return new AprobacionGgDetalleDto
            {
                AprobacionId      = head.GthAprobacionGgId,
                Area              = head.AreaNombre,
                SolicitanteNombre = head.SolicitanteNombre,
                Justificacion     = head.Justificacion,
                SustentoNombre    = head.SustentoNombre,
                SustentoUrl       = head.SustentoUrl,
                Enviado           = head.CreatedDateTime.ToOffset(PeruOffset).DateTime,
                GerenteGeneral    = gg,
                GerenteArea       = ga,
                Nivel             = scope.Nivel,
                PuedeDecidir      = scope.PuedeDecidir && !miCasilla.Decidida,
                Vacantes          = vacantes,
            };
        }

        public async Task<AprobacionGgDecisionContextoDto> RegistrarDecision(
            int aprobacionId, AprobacionGgDecisionDto dto, int userId, AprobacionScope scope)
        {
            if (!scope.PuedeDecidir)
                throw new AbrilException(
                    "Tu ficha de trabajador no es de Gerencia General ni de gerente de área, " +
                    "así que no puedes aprobar ni rechazar solicitudes de personal.", 403);

            using var ctx = _factory.CreateDbContext();

            var aprobacion = await ctx.GthAprobacionGg
                .FirstOrDefaultAsync(a => a.GthAprobacionGgId == aprobacionId && a.State);
            if (aprobacion == null)
                throw new AbrilException("La solicitud por aprobar ya no está disponible.", 404);

            var solicitud = await ctx.GthSolicitud
                .FirstOrDefaultAsync(s => s.GthSolicitudId == aprobacion.GthSolicitudId && s.State)
                ?? throw new AbrilException("La solicitud de personal ya no está disponible.", 404);

            EnsureAlcance(scope, solicitud.AreaScopeId);

            var esGg = scope.Nivel == AprobacionNivel.GerenteGeneral;

            // Cada nivel decide UNA sola vez. Se lee el estado de SU casilla: que el otro nivel ya
            // haya decidido no bloquea (dos gerentes pueden tener el modal abierto a la vez).
            var estadoActualId = esGg ? aprobacion.EstadoGerenteGeneralId : aprobacion.EstadoGerenteAreaId;
            var estadoActual = await ctx.GthAprobacionGgEstado
                .Where(e => e.GthAprobacionGgEstadoId == estadoActualId)
                .Select(e => e.Codigo)
                .FirstOrDefaultAsync();
            if (estadoActual != AprobacionGgEstadoCodigo.Pendiente)
                throw new AbrilException(
                    esGg ? "Gerencia General ya decidió sobre esta solicitud."
                         : "El gerente del área ya registró su visto bueno en esta solicitud.", 409);

            // Vacantes vigentes de la solicitud (entidades: al decidir el GG se les cambia el estado).
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

            // Estados destino del requerimiento: solo los mueve Gerencia General. El visto bueno
            // del gerente del área es redundante por diseño y no toca el pipeline — si lo moviera,
            // una vacante entraría a GTH sin la aprobación obligatoria.
            GthEstadoRequerimiento? validacionGth = null, rechazadoGg = null;
            if (esGg)
            {
                var estadosReq = await ctx.GthEstadoRequerimiento
                    .Where(e => e.State && (e.Codigo == EstadoReclutamiento.ValidacionGth
                                            || e.Codigo == EstadoReclutamiento.RechazadoGg))
                    .ToListAsync();
                validacionGth = estadosReq.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.ValidacionGth)
                    ?? throw new AbrilException("No está configurado el estado VALIDACION_GTH de reclutamiento.", 500);
                rechazadoGg = estadosReq.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.RechazadoGg)
                    ?? throw new AbrilException("No está configurado el estado RECHAZADO_GG de reclutamiento.", 500);
            }

            var detalles = await ctx.GthAprobacionGgDetalle
                .Where(d => d.GthAprobacionGgId == aprobacion.GthAprobacionGgId && d.State)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            int aprobados = 0, rechazados = 0;

            foreach (var r in requerimientos)
            {
                var aprobado = decisionPorId[r.GthRequerimientoId];
                if (aprobado) aprobados++; else rechazados++;

                if (esGg)
                {
                    r.GthEstadoRequerimientoId = aprobado
                        ? validacionGth!.GthEstadoRequerimientoId
                        : rechazadoGg!.GthEstadoRequerimientoId;
                    r.UpdatedDateTime = now;
                    r.UpdatedUserId   = userId;
                }

                var detalle = detalles.FirstOrDefault(d => d.GthRequerimientoId == r.GthRequerimientoId);
                if (detalle == null)
                {
                    detalle = new GthAprobacionGgDetalle
                    {
                        GthAprobacionGgId  = aprobacion.GthAprobacionGgId,
                        GthRequerimientoId = r.GthRequerimientoId,
                        CreatedDateTime    = now,
                        CreatedUserId      = userId,
                        Active             = true,
                        State              = true,
                    };
                    ctx.GthAprobacionGgDetalle.Add(detalle);
                }
                else
                {
                    detalle.UpdatedDateTime = now;
                    detalle.UpdatedUserId   = userId;
                }

                // Solo se escribe la columna del nivel que está decidiendo: la del otro queda como
                // esté (puede tener ya una decisión distinta, y esa discrepancia es información).
                if (esGg)
                {
                    detalle.AprobadoGerenteGeneral         = aprobado;
                    detalle.GerenteGeneralDecididoDateTime = now;
                }
                else
                {
                    detalle.AprobadoGerenteArea         = aprobado;
                    detalle.GerenteAreaDecididoDateTime = now;
                }
            }

            var codigoEstado = aprobados == 0 ? AprobacionGgEstadoCodigo.Rechazada
                             : rechazados == 0 ? AprobacionGgEstadoCodigo.Aprobada
                             : AprobacionGgEstadoCodigo.AprobadaParcial;
            var estadoDestino = await ctx.GthAprobacionGgEstado
                .FirstOrDefaultAsync(e => e.Codigo == codigoEstado && e.State)
                ?? throw new AbrilException($"No está configurado el estado {codigoEstado} de la aprobación.", 500);

            var comentario = string.IsNullOrWhiteSpace(dto.Comentario) ? null : dto.Comentario.Trim();

            if (esGg)
            {
                aprobacion.EstadoGerenteGeneralId         = estadoDestino.GthAprobacionGgEstadoId;
                aprobacion.GerenteGeneralDecididoDateTime = now;
                // Traza de quién decidió, aparte de updated_user_id para que un update posterior
                // (p. ej. el visto bueno tardío del área) no la pise.
                aprobacion.GerenteGeneralDecididoUserId   = userId;
                aprobacion.GerenteGeneralComentario       = comentario;
            }
            else
            {
                aprobacion.EstadoGerenteAreaId         = estadoDestino.GthAprobacionGgEstadoId;
                aprobacion.GerenteAreaDecididoDateTime = now;
                aprobacion.GerenteAreaDecididoUserId   = userId;
                aprobacion.GerenteAreaComentario       = comentario;
            }

            aprobacion.UpdatedDateTime = now;
            aprobacion.UpdatedUserId   = userId;

            var solicitanteNombre = solicitud.SolicitanteUserId.HasValue
                ? await ctx.Worker
                    .Where(w => w.Person != null && w.Person.UserId == solicitud.SolicitanteUserId.Value)
                    .Select(w => w.Person!.FullName ?? w.ApellidoNombre)
                    .FirstOrDefaultAsync()
                : null;

            // Qué dejó dicho el gerente del área: va como contexto en el correo a GTH cuando decide
            // el GG. Se lee antes del SaveChanges porque la propia decisión del GG no lo cambia.
            var gerenteAreaResumen = await BuildGerenteAreaResumen(ctx, aprobacion);

            await ctx.SaveChangesAsync();

            // Vacantes con sus datos legibles para el correo a GTH (una sola consulta para ambas listas).
            var vacantes = await QueryVacantes(ctx, aprobacion.GthAprobacionGgId, aprobacion.GthSolicitudId);
            Func<AprobacionGgVacanteDto, bool?> decisionDeEsteNivel =
                esGg ? v => v.AprobadoGerenteGeneral : v => v.AprobadoGerenteArea;

            return new AprobacionGgDecisionContextoDto
            {
                Resultado = new AprobacionGgDecisionResultDto
                {
                    Nivel        = scope.Nivel,
                    EstadoCodigo = estadoDestino.Codigo,
                    EstadoNombre = estadoDestino.Nombre,
                    Aprobados    = aprobados,
                    Rechazados   = rechazados,
                },
                SolicitudId        = aprobacion.GthSolicitudId,
                Area               = solicitud.AreaNombre,
                SolicitanteNombre  = solicitanteNombre,
                Justificacion      = solicitud.Justificacion,
                SustentoNombre     = solicitud.SustentoNombre,
                SustentoUrl        = solicitud.SustentoUrl,
                Comentario         = comentario,
                GerenteAreaResumen = esGg ? gerenteAreaResumen : null,
                Aprobadas          = vacantes.Where(v => decisionDeEsteNivel(v) == true).ToList(),
                Rechazadas         = vacantes.Where(v => decisionDeEsteNivel(v) == false).ToList(),
            };
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Misma regla que <see cref="RegistrarDecision"/> —solo se escribe la casilla del nivel del
        /// usuario y solo el Gerente General mueve el pipeline— pero resuelta en lote: las
        /// cabeceras, las vacantes, los detalles y los catálogos se leen de una vez para todas las
        /// solicitudes seleccionadas y todo se guarda en un solo <c>SaveChanges</c>, en vez de
        /// repetir N veces la decisión de una.
        ///
        /// El lote no es todo-o-nada: cada solicitud que ya no admite la decisión de este usuario se
        /// aparta con su motivo y las demás se registran igual. Lo contrario obligaría al gerente a
        /// adivinar cuál de las diez que seleccionó se cerró mientras revisaba.
        /// </remarks>
        public async Task<AprobacionGgDecisionMasivaContextoDto> RegistrarDecisionMasiva(
            List<int> aprobacionIds, bool aprobado, string? comentario, int userId, AprobacionScope scope)
        {
            if (!scope.PuedeDecidir)
                throw new AbrilException(
                    "Tu ficha de trabajador no es de Gerencia General ni de gerente de área, " +
                    "así que no puedes aprobar ni rechazar solicitudes de personal.", 403);

            var resultado = new AprobacionGgDecisionMasivaContextoDto { Nivel = scope.Nivel };
            if (aprobacionIds.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();
            var esGg = scope.Nivel == AprobacionNivel.GerenteGeneral;

            // 1) Cabeceras: la aprobación y su solicitud. Entidades (no proyección) porque las dos
            //    se escriben más abajo.
            var cabeceras = await (
                from a in ctx.GthAprobacionGg
                where aprobacionIds.Contains(a.GthAprobacionGgId) && a.State
                join s in ctx.GthSolicitud on a.GthSolicitudId equals s.GthSolicitudId
                where s.State
                select new { Aprobacion = a, Solicitud = s }
            ).ToListAsync();

            // Las que ni siquiera existen (o se dieron de baja) se apartan acá: sin esto
            // desaparecerían del conteo sin explicación.
            foreach (var id in aprobacionIds.Where(id => cabeceras.All(c => c.Aprobacion.GthAprobacionGgId != id)))
                resultado.Omitidas.Add(new AprobacionGgDecisionOmitidaDto
                {
                    AprobacionId = id,
                    Motivo       = "La solicitud ya no está disponible.",
                });

            if (cabeceras.Count == 0) return resultado;

            // 2) Catálogos y filas hijas de TODAS las cabeceras, de una sola vez.
            //    Los estados de la aprobación son un catálogo de 4 filas: se trae completo (sin
            //    filtrar State) porque hace falta tanto para leer el estado actual de cada casilla
            //    como para resolver el destino.
            var estados = await ctx.GthAprobacionGgEstado.AsNoTracking().ToListAsync();

            var solicitudIds = cabeceras.Select(c => c.Solicitud.GthSolicitudId).ToList();
            var requerimientos = await ctx.GthRequerimiento
                .Where(r => solicitudIds.Contains(r.GthSolicitudId) && r.State)
                .OrderBy(r => r.GthRequerimientoId)
                .ToListAsync();
            var reqPorSolicitud = requerimientos
                .GroupBy(r => r.GthSolicitudId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var idsVivos = cabeceras.Select(c => c.Aprobacion.GthAprobacionGgId).ToList();
            var detalles = await ctx.GthAprobacionGgDetalle
                .Where(d => idsVivos.Contains(d.GthAprobacionGgId) && d.State)
                .ToListAsync();
            var detallePorAprobacion = detalles
                .GroupBy(d => d.GthAprobacionGgId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Estados destino del requerimiento: solo los mueve Gerencia General (ver RegistrarDecision).
            GthEstadoRequerimiento? validacionGth = null, rechazadoGg = null;
            if (esGg)
            {
                var estadosReq = await ctx.GthEstadoRequerimiento
                    .Where(e => e.State && (e.Codigo == EstadoReclutamiento.ValidacionGth
                                            || e.Codigo == EstadoReclutamiento.RechazadoGg))
                    .ToListAsync();
                validacionGth = estadosReq.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.ValidacionGth)
                    ?? throw new AbrilException("No está configurado el estado VALIDACION_GTH de reclutamiento.", 500);
                rechazadoGg = estadosReq.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.RechazadoGg)
                    ?? throw new AbrilException("No está configurado el estado RECHAZADO_GG de reclutamiento.", 500);
            }

            // Estado destino de la casilla: en bloque la decisión es la misma para todas las
            // vacantes de la solicitud, así que nunca queda una aprobación parcial.
            var codigoDestino = aprobado ? AprobacionGgEstadoCodigo.Aprobada : AprobacionGgEstadoCodigo.Rechazada;
            var estadoDestino = estados.FirstOrDefault(e => e.Codigo == codigoDestino && e.State)
                ?? throw new AbrilException($"No está configurado el estado {codigoDestino} de la aprobación.", 500);

            var comentarioLimpio = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
            var now = DateTimeOffset.UtcNow;

            // 3) La decisión, solicitud por solicitud pero sin volver a la BD.
            var decididas = new List<(GthAprobacionGg Aprobacion, GthSolicitud Solicitud, int Vacantes)>();

            foreach (var c in cabeceras)
            {
                var aprobacion = c.Aprobacion;
                var solicitud  = c.Solicitud;

                // Alcance: acá no corta con 403 —el lote sigue— pero tampoco deja pasar una
                // solicitud de otra gerencia. Es la misma regla de EnsureAlcance, como omisión.
                if (!scope.Alcanza(solicitud.AreaScopeId))
                {
                    resultado.Omitidas.Add(new AprobacionGgDecisionOmitidaDto
                    {
                        AprobacionId = aprobacion.GthAprobacionGgId,
                        Motivo       = "Pertenece a otra área y no está dentro de tu alcance.",
                    });
                    continue;
                }

                // Cada nivel decide UNA sola vez, y solo se mira SU casilla: que el otro nivel ya
                // haya decidido no impide registrar la propia.
                var estadoActualId = esGg ? aprobacion.EstadoGerenteGeneralId : aprobacion.EstadoGerenteAreaId;
                var codigoActual = estados.FirstOrDefault(e => e.GthAprobacionGgEstadoId == estadoActualId)?.Codigo;
                if (codigoActual != AprobacionGgEstadoCodigo.Pendiente)
                {
                    resultado.Omitidas.Add(new AprobacionGgDecisionOmitidaDto
                    {
                        AprobacionId = aprobacion.GthAprobacionGgId,
                        Motivo       = esGg ? "Gerencia General ya decidió sobre esta solicitud."
                                            : "El gerente del área ya registró su visto bueno.",
                    });
                    continue;
                }

                if (!reqPorSolicitud.TryGetValue(solicitud.GthSolicitudId, out var vacantes) || vacantes.Count == 0)
                {
                    resultado.Omitidas.Add(new AprobacionGgDecisionOmitidaDto
                    {
                        AprobacionId = aprobacion.GthAprobacionGgId,
                        Motivo       = "La solicitud no tiene vacantes por aprobar.",
                    });
                    continue;
                }

                detallePorAprobacion.TryGetValue(aprobacion.GthAprobacionGgId, out var detallesDeEsta);

                // Todas las vacantes de la fila reciben la misma decisión: es justamente lo que
                // significa aprobar o rechazar la fila completa desde la lista.
                foreach (var r in vacantes)
                {
                    if (esGg)
                    {
                        r.GthEstadoRequerimientoId = aprobado
                            ? validacionGth!.GthEstadoRequerimientoId
                            : rechazadoGg!.GthEstadoRequerimientoId;
                        r.UpdatedDateTime = now;
                        r.UpdatedUserId   = userId;
                    }

                    var detalle = detallesDeEsta?.FirstOrDefault(d => d.GthRequerimientoId == r.GthRequerimientoId);
                    if (detalle == null)
                    {
                        detalle = new GthAprobacionGgDetalle
                        {
                            GthAprobacionGgId  = aprobacion.GthAprobacionGgId,
                            GthRequerimientoId = r.GthRequerimientoId,
                            CreatedDateTime    = now,
                            CreatedUserId      = userId,
                            Active             = true,
                            State              = true,
                        };
                        ctx.GthAprobacionGgDetalle.Add(detalle);
                    }
                    else
                    {
                        detalle.UpdatedDateTime = now;
                        detalle.UpdatedUserId   = userId;
                    }

                    // Solo la columna del nivel que decide: la del otro queda como esté.
                    if (esGg)
                    {
                        detalle.AprobadoGerenteGeneral         = aprobado;
                        detalle.GerenteGeneralDecididoDateTime = now;
                    }
                    else
                    {
                        detalle.AprobadoGerenteArea         = aprobado;
                        detalle.GerenteAreaDecididoDateTime = now;
                    }
                }

                if (esGg)
                {
                    aprobacion.EstadoGerenteGeneralId         = estadoDestino.GthAprobacionGgEstadoId;
                    aprobacion.GerenteGeneralDecididoDateTime = now;
                    aprobacion.GerenteGeneralDecididoUserId   = userId;
                    aprobacion.GerenteGeneralComentario       = comentarioLimpio;
                }
                else
                {
                    aprobacion.EstadoGerenteAreaId         = estadoDestino.GthAprobacionGgEstadoId;
                    aprobacion.GerenteAreaDecididoDateTime = now;
                    aprobacion.GerenteAreaDecididoUserId   = userId;
                    aprobacion.GerenteAreaComentario       = comentarioLimpio;
                }

                aprobacion.UpdatedDateTime = now;
                aprobacion.UpdatedUserId   = userId;

                decididas.Add((aprobacion, solicitud, vacantes.Count));
            }

            if (decididas.Count == 0) return resultado;

            await ctx.SaveChangesAsync();

            // 4) Contexto de cada decisión registrada. Los datos legibles (vacantes, solicitante,
            //    visto bueno del área) solo los consume el correo del Gerente General, así que en el
            //    visto bueno del gerente del área no se consulta nada más: se devuelven las
            //    cabeceras con sus conteos y listo.
            var aprobacionIdsDecididas = decididas.Select(d => d.Aprobacion.GthAprobacionGgId).ToList();

            var vacantesPorAprobacion = esGg
                ? await QueryVacantesDeVarias(ctx, aprobacionIdsDecididas)
                : new Dictionary<int, List<AprobacionGgVacanteDto>>();

            var nombresPorUser = new Dictionary<int, string?>();
            var resumenAreaPorAprobacion = new Dictionary<int, string?>();
            if (esGg)
            {
                // Nombres del solicitante de cada solicitud y del gerente de área que ya dio su
                // visto bueno, en una sola consulta a person para todos los usuarios del lote.
                var userIds = decididas
                    .Select(d => d.Solicitud.SolicitanteUserId)
                    .Concat(decididas.Select(d => d.Aprobacion.GerenteAreaDecididoUserId))
                    .Where(id => id.HasValue)
                    .Distinct()
                    .ToList();

                if (userIds.Count > 0)
                {
                    var filas = await ctx.Person.AsNoTracking()
                        .Where(p => userIds.Contains(p.UserId))
                        .Select(p => new { p.UserId, p.FullName })
                        .ToListAsync();
                    foreach (var f in filas)
                        if (f.UserId.HasValue) nombresPorUser[f.UserId.Value] = f.FullName;
                }

                // "Aprobada parcialmente — Juan Pérez": lo mismo que BuildGerenteAreaResumen, pero
                // con el catálogo y los nombres ya en memoria. Null si el área nunca opinó.
                foreach (var d in decididas)
                {
                    if (!d.Aprobacion.GerenteAreaDecididoDateTime.HasValue) continue;

                    var estadoNombre = estados
                        .FirstOrDefault(e => e.GthAprobacionGgEstadoId == d.Aprobacion.EstadoGerenteAreaId)?.Nombre;
                    if (string.IsNullOrWhiteSpace(estadoNombre)) continue;

                    var quien = d.Aprobacion.GerenteAreaDecididoUserId.HasValue
                        ? nombresPorUser.GetValueOrDefault(d.Aprobacion.GerenteAreaDecididoUserId.Value)
                        : null;

                    resumenAreaPorAprobacion[d.Aprobacion.GthAprobacionGgId] =
                        string.IsNullOrWhiteSpace(quien) ? estadoNombre : $"{estadoNombre} — {quien}";
                }
            }

            foreach (var d in decididas)
            {
                var vacantes = vacantesPorAprobacion.GetValueOrDefault(d.Aprobacion.GthAprobacionGgId)
                               ?? new List<AprobacionGgVacanteDto>();

                resultado.Registradas.Add(new AprobacionGgDecisionContextoDto
                {
                    Resultado = new AprobacionGgDecisionResultDto
                    {
                        Nivel        = scope.Nivel,
                        EstadoCodigo = estadoDestino.Codigo,
                        EstadoNombre = estadoDestino.Nombre,
                        Aprobados    = aprobado ? d.Vacantes : 0,
                        Rechazados   = aprobado ? 0 : d.Vacantes,
                    },
                    SolicitudId        = d.Solicitud.GthSolicitudId,
                    Area               = d.Solicitud.AreaNombre,
                    SolicitanteNombre  = d.Solicitud.SolicitanteUserId.HasValue
                        ? nombresPorUser.GetValueOrDefault(d.Solicitud.SolicitanteUserId.Value)
                        : null,
                    Justificacion      = d.Solicitud.Justificacion,
                    SustentoNombre     = d.Solicitud.SustentoNombre,
                    SustentoUrl        = d.Solicitud.SustentoUrl,
                    Comentario         = comentarioLimpio,
                    GerenteAreaResumen = resumenAreaPorAprobacion.GetValueOrDefault(d.Aprobacion.GthAprobacionGgId),
                    Aprobadas          = aprobado ? vacantes : new List<AprobacionGgVacanteDto>(),
                    Rechazadas         = aprobado ? new List<AprobacionGgVacanteDto>() : vacantes,
                });
            }

            return resultado;
        }

        /// <summary>
        /// Igual que <see cref="QueryVacantes"/> pero para varias aprobaciones en un solo roundtrip,
        /// agrupadas por aprobación. La llave del join al detalle es el par (aprobación,
        /// requerimiento), como en <see cref="GetBandeja"/>: filtrar solo por requerimiento traería
        /// el detalle de otra aprobación de la misma solicitud.
        /// </summary>
        private static async Task<Dictionary<int, List<AprobacionGgVacanteDto>>> QueryVacantesDeVarias(
            AppDbContext ctx, List<int> aprobacionIds)
        {
            var filas = await (
                from a in ctx.GthAprobacionGg.AsNoTracking()
                where aprobacionIds.Contains(a.GthAprobacionGgId)
                join r in ctx.GthRequerimiento.AsNoTracking() on a.GthSolicitudId equals r.GthSolicitudId
                where r.State
                join p in ctx.Puesto.AsNoTracking() on r.PuestoId equals p.PuestoId
                join t in ctx.GthTipoRequerimiento.AsNoTracking() on r.GthTipoRequerimientoId equals t.GthTipoRequerimientoId
                join pr in ctx.Project.AsNoTracking() on r.ProjectId equals pr.ProjectId
                join d in ctx.GthAprobacionGgDetalle.AsNoTracking().Where(x => x.State)
                    on new { A = a.GthAprobacionGgId, R = r.GthRequerimientoId }
                    equals new { A = d.GthAprobacionGgId, R = d.GthRequerimientoId } into detalleJoin
                from d in detalleJoin.DefaultIfEmpty()
                join wr in ctx.Worker.AsNoTracking() on r.ReemplazaWorkerId equals (int?)wr.Id into reemplazaJoin
                from wr in reemplazaJoin.DefaultIfEmpty()
                orderby r.GthRequerimientoId
                select new
                {
                    a.GthAprobacionGgId,
                    Vacante = new AprobacionGgVacanteDto
                    {
                        RequerimientoId        = r.GthRequerimientoId,
                        Codigo                 = r.Codigo,
                        Puesto                 = p.Nombre,
                        TipoRequerimiento      = t.Nombre,
                        TrabajadorReemplazado  = wr == null ? null
                            : (wr.Person != null ? wr.Person.FullName : wr.ApellidoNombre),
                        ProyectoObra           = pr.ProjectDescription,
                        SalarioBrutoMensual    = r.SalarioBrutoMensual,
                        AprobadoGerenteArea    = d != null ? d.AprobadoGerenteArea : null,
                        AprobadoGerenteGeneral = d != null ? d.AprobadoGerenteGeneral : null,
                    },
                }).ToListAsync();

            return filas
                .GroupBy(f => f.GthAprobacionGgId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Vacante).ToList());
        }

        /// <summary>
        /// Frase legible con lo que el gerente del área dejó registrado ("Aprobada parcialmente —
        /// Juan Pérez"). Null si nunca opinó: en ese caso el correo a GTH no dice nada del área en
        /// vez de afirmar que está pendiente, que no aporta.
        /// </summary>
        private static async Task<string?> BuildGerenteAreaResumen(AppDbContext ctx, GthAprobacionGg aprobacion)
        {
            if (!aprobacion.GerenteAreaDecididoDateTime.HasValue) return null;

            var estado = await ctx.GthAprobacionGgEstado.AsNoTracking()
                .Where(e => e.GthAprobacionGgEstadoId == aprobacion.EstadoGerenteAreaId)
                .Select(e => e.Nombre)
                .FirstOrDefaultAsync();

            var quien = aprobacion.GerenteAreaDecididoUserId.HasValue
                ? await ctx.Person.AsNoTracking()
                    .Where(p => p.UserId == aprobacion.GerenteAreaDecididoUserId.Value)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync()
                : null;

            if (string.IsNullOrWhiteSpace(estado)) return null;
            return string.IsNullOrWhiteSpace(quien) ? estado : $"{estado} — {quien}";
        }

        public async Task<AprobacionGgResumenDto?> GetResumenByRequerimiento(int requerimientoId)
        {
            using var ctx = _factory.CreateDbContext();

            // Left join al detalle: la aprobación es de la solicitud completa, pero la tarjeta
            // muestra además la decisión de ESTA vacante en cada nivel.
            var raw = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId && r.State
                join a in ctx.GthAprobacionGg on r.GthSolicitudId equals a.GthSolicitudId
                where a.State
                join egg in ctx.GthAprobacionGgEstado on a.EstadoGerenteGeneralId equals egg.GthAprobacionGgEstadoId
                join ega in ctx.GthAprobacionGgEstado on a.EstadoGerenteAreaId equals ega.GthAprobacionGgEstadoId
                join d in ctx.GthAprobacionGgDetalle.Where(x => x.State)
                    on new { A = a.GthAprobacionGgId, R = r.GthRequerimientoId }
                    equals new { A = d.GthAprobacionGgId, R = d.GthRequerimientoId } into detalleJoin
                from d in detalleJoin.DefaultIfEmpty()
                select new
                {
                    GgEstadoCodigo = egg.Codigo,
                    GgEstadoNombre = egg.Nombre,
                    GaEstadoCodigo = ega.Codigo,
                    GaEstadoNombre = ega.Nombre,
                    AprobadoGg     = d != null ? d.AprobadoGerenteGeneral : null,
                    AprobadoGa     = d != null ? d.AprobadoGerenteArea : null,
                    a.GerenteGeneralComentario,
                    a.GerenteAreaComentario,
                    a.EnviadoDateTime,
                    a.GerenteGeneralDecididoDateTime,
                    a.GerenteAreaDecididoDateTime,
                }).FirstOrDefaultAsync();

            if (raw == null) return null;

            // Conversión a hora Perú en memoria (ToOffset no se traduce a SQL).
            return new AprobacionGgResumenDto
            {
                EstadoCodigo            = raw.GgEstadoCodigo,
                EstadoNombre            = raw.GgEstadoNombre,
                Aprobado                = raw.AprobadoGg,
                GerenteAreaEstadoCodigo = raw.GaEstadoCodigo,
                GerenteAreaEstadoNombre = raw.GaEstadoNombre,
                AprobadoGerenteArea     = raw.AprobadoGa,
                Comentario              = raw.GerenteGeneralComentario,
                GerenteAreaComentario   = raw.GerenteAreaComentario,
                EnviadoEn               = raw.EnviadoDateTime?.ToOffset(PeruOffset).DateTime,
                DecididoEn              = raw.GerenteGeneralDecididoDateTime?.ToOffset(PeruOffset).DateTime,
                GerenteAreaDecididoEn   = raw.GerenteAreaDecididoDateTime?.ToOffset(PeruOffset).DateTime,
            };
        }

        /// <summary>
        /// Corta con 403 si la solicitud no cae dentro del alcance del usuario. Mensaje explícito a
        /// propósito: el gerente tiene que entender que no es un error, es que no es su área.
        /// </summary>
        private static void EnsureAlcance(AprobacionScope scope, int? areaScopeId)
        {
            if (scope.Alcanza(areaScopeId)) return;

            throw new AbrilException(
                scope.PuedeDecidir
                    ? "Esta solicitud de personal pertenece a otra área y no está dentro de tu alcance."
                    : "Tu ficha de trabajador no te da acceso a las solicitudes de personal por aprobar.",
                403);
        }

        /// <summary>Resuelve el id de un estado de la aprobación por su código estable; 500 si no está sembrado.</summary>
        private static async Task<int> ResolveEstadoId(AppDbContext ctx, string codigo)
        {
            var id = await ctx.GthAprobacionGgEstado
                .Where(e => e.Codigo == codigo && e.State)
                .Select(e => (int?)e.GthAprobacionGgEstadoId)
                .FirstOrDefaultAsync();
            if (id == null)
                throw new AbrilException($"No está configurado el estado {codigo} de la aprobación.", 500);
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
