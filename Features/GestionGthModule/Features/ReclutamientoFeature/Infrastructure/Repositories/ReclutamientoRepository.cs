using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    public class ReclutamientoRepository : IReclutamientoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ReclutamientoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Los timestamps se guardan en UTC y se sirven al frontend en hora de Perú.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        /// <summary>
        /// Fila cruda del formulario del postulante en el detalle de GTH. Es una clase con nombre
        /// (y no un tipo anónimo) porque la consulta se saltea cuando no hay candidatos y ambas
        /// ramas del condicional necesitan el mismo tipo de lista.
        /// </summary>
        private sealed class FormularioRawRow
        {
            public int GthCandidatoId { get; set; }
            public string EstadoCodigo { get; set; } = string.Empty;
            public string EstadoNombre { get; set; } = string.Empty;
            public string CorreoEnvio { get; set; } = string.Empty;
            public string? CorreoElectronico { get; set; }
            public DateTimeOffset? EnviadoDateTime { get; set; }
            public DateTimeOffset? CompletadoDateTime { get; set; }
            public string? RevisadoNombre { get; set; }
            public DateTimeOffset? RevisadoDateTime { get; set; }
        }

        /// <summary>
        /// Fila cruda de la evaluación de la entrevista de un candidato (mismo motivo que
        /// <see cref="FormularioRawRow"/>: la consulta se saltea cuando no hay candidatos y ambas
        /// ramas necesitan el mismo tipo de lista).
        /// </summary>
        private sealed class EvaluacionRawRow
        {
            public int GthCandidatoId { get; set; }
            public int? PuntajeEntrevista { get; set; }
            public int? PuntajePsicotecnico { get; set; }
            public int? PuntajeTecnica { get; set; }
            public int? PuntajeResultado { get; set; }
            public string? ComentarioEntrevista { get; set; }
            public string? ComentarioPsicotecnico { get; set; }
            public string? ComentarioRecomendacion { get; set; }
            public string ResultadoCodigo { get; set; } = string.Empty;
            public string ResultadoNombre { get; set; } = string.Empty;
            public string? AgradecimientoCorreo { get; set; }
            public DateTimeOffset? AgradecimientoDateTime { get; set; }
            public DateTimeOffset? DecisionDateTime { get; set; }
        }

        /// <summary>Evaluaciones vigentes de los candidatos indicados (vacío si no hay candidatos).</summary>
        private static async Task<List<EvaluacionRawRow>> QueryEvaluaciones(AppDbContext ctx, List<int> candidatoIds)
        {
            if (candidatoIds.Count == 0) return new List<EvaluacionRawRow>();

            return await (
                from ev in ctx.GthCandidatoEvaluacion
                where ev.State && candidatoIds.Contains(ev.GthCandidatoId)
                join res in ctx.GthCandidatoResultado on ev.GthCandidatoResultadoId equals res.GthCandidatoResultadoId
                select new EvaluacionRawRow
                {
                    GthCandidatoId          = ev.GthCandidatoId,
                    PuntajeEntrevista       = ev.PuntajeEntrevista,
                    PuntajePsicotecnico     = ev.PuntajePsicotecnico,
                    PuntajeTecnica          = ev.PuntajeTecnica,
                    PuntajeResultado        = ev.PuntajeResultado,
                    ComentarioEntrevista    = ev.ComentarioEntrevista,
                    ComentarioPsicotecnico  = ev.ComentarioPsicotecnico,
                    ComentarioRecomendacion = ev.ComentarioRecomendacion,
                    ResultadoCodigo         = res.Codigo,
                    ResultadoNombre         = res.Nombre,
                    AgradecimientoCorreo    = ev.AgradecimientoCorreo,
                    AgradecimientoDateTime  = ev.AgradecimientoDateTime,
                    DecisionDateTime        = ev.DecisionDateTime,
                }).ToListAsync();
        }

        /// <summary>Proyecta la fila cruda de la evaluación al DTO (fechas ya en hora de Perú).</summary>
        private static EvaluacionResumenDto MapEvaluacion(EvaluacionRawRow x) => new()
        {
            PuntajeEntrevista       = x.PuntajeEntrevista,
            PuntajePsicotecnico     = x.PuntajePsicotecnico,
            PuntajeTecnica          = x.PuntajeTecnica,
            PuntajeResultado        = x.PuntajeResultado,
            ComentarioEntrevista    = x.ComentarioEntrevista,
            ComentarioPsicotecnico  = x.ComentarioPsicotecnico,
            ComentarioRecomendacion = x.ComentarioRecomendacion,
            ResultadoCodigo         = x.ResultadoCodigo,
            ResultadoNombre         = x.ResultadoNombre,
            AgradecimientoCorreo    = x.AgradecimientoCorreo,
            AgradecimientoEnviadoEn = x.AgradecimientoDateTime?.ToOffset(PeruOffset).DateTime,
            DecididoEn              = x.DecisionDateTime?.ToOffset(PeruOffset).DateTime,
        };

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

            dto.Puestos = await ctx.Puesto
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
                .Select(p => new OpcionDto { Id = p.PuestoId, Nombre = p.Nombre })
                .ToListAsync();

            // Para el modo "Puesto personalizado": el solicitante escribe el puesto y le asigna una
            // de estas categorías (la real del trabajador, no la que trae el puesto del catálogo).
            dto.Categorias = await ctx.Categoria
                .Where(c => c.State && c.Active)
                .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
                .Select(c => new OpcionDto { Id = c.CategoriaId, Nombre = c.Nombre })
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

        public async Task<SolicitantePanelDto> GetSolicitantePanel(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Tabla "Mis solicitudes de vacante" del usuario (mismo proyectado de siempre).
            var misSolicitudes = await ProjectRequerimientos(
                ctx,
                ctx.GthRequerimiento.Where(r => r.State && r.Solicitud!.State && r.Solicitud.SolicitanteUserId == userId));

            // Tarjetas "Gestión de candidatos": requerimientos del usuario cuya long list ya fue
            // enviada por GTH (estado LONG_LIST_ENVIADA), con el conteo de candidatos vigentes.
            var cards = await (
                from r in ctx.GthRequerimiento
                where r.State && r.Solicitud!.State && r.Solicitud.SolicitanteUserId == userId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                where e.Codigo == EstadoReclutamiento.LongListEnviada
                orderby r.UpdatedDateTime descending, r.GthRequerimientoId descending
                select new GestionCandidatoCardDto
                {
                    RequerimientoId = r.GthRequerimientoId,
                    Codigo          = r.Codigo,
                    Puesto          = p.Nombre,
                    Area            = r.Solicitud!.AreaNombre,
                    ProyectoObra    = pr.ProjectDescription,
                    TotalCandidatos = ctx.GthCandidato.Count(c => c.GthRequerimientoId == r.GthRequerimientoId && c.State),
                    EstadoCodigo    = e.Codigo,
                    EstadoNombre    = e.Nombre,
                    Tipo            = TipoGestionCandidato.LongList,
                }).ToListAsync();

            // Tarjetas "Finalistas enviados por GTH": requerimientos del usuario que siguen en la
            // fase de entrevistas y ya tienen al menos un candidato evaluado que sigue en carrera.
            // El filtro por ENTREVISTAS es el que hace desaparecer la tarjeta cuando el proceso se
            // cierra (aprobó a un finalista) o vuelve a LONG_LIST (rechazó a todos).
            //
            // El id de NO_PASO se resuelve aparte (consulta mínima al catálogo) para que el conteo
            // por requerimiento sea una subconsulta simple sobre ids en vez de un join anidado.
            var noPasoId = await ctx.GthCandidatoResultado
                .Where(x => x.Codigo == ResultadoCandidato.NoPaso && x.State)
                .Select(x => (int?)x.GthCandidatoResultadoId)
                .FirstOrDefaultAsync() ?? 0; // 0 = catálogo sin sembrar: no descarta a nadie

            var finalistas = await (
                from r in ctx.GthRequerimiento
                where r.State && r.Solicitud!.State && r.Solicitud.SolicitanteUserId == userId
                      && ctx.GthCandidato.Any(c => c.GthRequerimientoId == r.GthRequerimientoId && c.State
                            && ctx.GthCandidatoEvaluacion.Any(ev => ev.GthCandidatoId == c.GthCandidatoId
                                  && ev.State && ev.GthCandidatoResultadoId != noPasoId))
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                where e.Codigo == EstadoReclutamiento.Entrevistas
                orderby r.UpdatedDateTime descending, r.GthRequerimientoId descending
                select new GestionCandidatoCardDto
                {
                    RequerimientoId = r.GthRequerimientoId,
                    Codigo          = r.Codigo,
                    Puesto          = p.Nombre,
                    Area            = r.Solicitud!.AreaNombre,
                    ProyectoObra    = pr.ProjectDescription,
                    TotalCandidatos = ctx.GthCandidato.Count(c => c.GthRequerimientoId == r.GthRequerimientoId && c.State
                            && ctx.GthCandidatoEvaluacion.Any(ev => ev.GthCandidatoId == c.GthCandidatoId
                                  && ev.State && ev.GthCandidatoResultadoId != noPasoId)),
                    EstadoCodigo    = e.Codigo,
                    EstadoNombre    = e.Nombre,
                    Tipo            = TipoGestionCandidato.Finalistas,
                }).ToListAsync();

            cards.AddRange(finalistas);

            // Tarjetas resumen. Se calculan sobre lo ya traído (sin roundtrips extra).
            //
            // "En revisión · GTH evaluando" = procesos cuyo siguiente paso le toca a GTH, sin contar
            // el primero (NUEVO, que es justamente "Pendientes · Sin respuesta"). Además se descartan
            // los que están esperando una acción del solicitante, que son exactamente los que ya
            // tienen tarjeta en "Gestión de candidatos": el caso real es ENTREVISTAS con finalistas
            // ya enviados — GTH terminó su parte y la decisión es del solicitante, así que sería
            // contradictorio mostrarlo a la vez como "GTH evaluando".
            var esperandoAlSolicitante = cards.Select(c => c.RequerimientoId).ToHashSet();

            var anioActual = DateTimeOffset.UtcNow.ToOffset(PeruOffset).Year;

            var resumen = new ResumenSolicitantePanelDto
            {
                TotalRegistradas = misSolicitudes.Count,
                // "Pendientes · Sin respuesta" = nadie movió todavía el requerimiento. Con el nuevo
                // flujo eso es APROBACION_GG (esperando a Gerencia General); se sigue contando NUEVO
                // por los requerimientos anteriores a ese cambio.
                Pendientes       = misSolicitudes.Count(s => s.EstadoCodigo == EstadoReclutamiento.Nuevo
                                                             || s.EstadoCodigo == EstadoReclutamiento.AprobacionGg),
                EnRevisionGth    = misSolicitudes.Count(s =>
                                       EstadoReclutamiento.FasesGth.Contains(s.EstadoCodigo)
                                       && !esperandoAlSolicitante.Contains(s.RequerimientoId)),
                // "Este período" = año en curso (hora Perú), el mismo que rotula la cabecera de la
                // página. Se toma la fecha de envío porque es la que define a qué año pertenece el
                // requerimiento en la tabla.
                Aprobadas        = misSolicitudes.Count(s =>
                                       s.EstadoCodigo == EstadoReclutamiento.Cerrado
                                       && s.Enviado.Year == anioActual),
            };

            return new SolicitantePanelDto
            {
                Resumen           = resumen,
                GestionCandidatos = cards,
                MisSolicitudes    = misSolicitudes,
            };
        }

        public async Task<RevisionLongListDto?> GetRevisionLongList(int requerimientoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Cabecera del requerimiento (scope: solo del usuario dueño de la solicitud) que ya
            // tenga la long list enviada (LONG_LIST_ENVIADA o posterior).
            var head = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId
                      && r.State && r.Solicitud!.State
                      && r.Solicitud.SolicitanteUserId == userId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                select new
                {
                    r.GthRequerimientoId,
                    r.Codigo,
                    Puesto       = p.Nombre,
                    Area         = r.Solicitud!.AreaNombre,
                    ProyectoObra = pr.ProjectDescription,
                    EstadoCodigo = e.Codigo,
                    EstadoNombre = e.Nombre,
                    EstadoOrden  = e.Orden,
                }).FirstOrDefaultAsync();

            if (head == null) return null;

            // La revisión solo está disponible una vez que GTH envió la long list.
            var longListEnviadaOrden = await ctx.GthEstadoRequerimiento
                .Where(e => e.Codigo == EstadoReclutamiento.LongListEnviada && e.State)
                .Select(e => (int?)e.Orden)
                .FirstOrDefaultAsync();
            if (longListEnviadaOrden == null || head.EstadoOrden < longListEnviadaOrden)
                return null;

            var candidatos = await (
                from c in ctx.GthCandidato
                where c.GthRequerimientoId == requerimientoId && c.State
                join est in ctx.GthCandidatoEstado on c.GthCandidatoEstadoId equals est.GthCandidatoEstadoId
                orderby c.Orden, c.GthCandidatoId
                select new CandidatoRevisionDto
                {
                    CandidatoId   = c.GthCandidatoId,
                    Nombre        = c.Nombre,
                    Puesto        = c.Puesto,
                    Comentario    = c.Comentario,
                    CvNombre      = c.CvNombre,
                    CvUrl         = c.CvUrl,
                    InformeNombre = c.InformeNombre,
                    InformeUrl    = c.InformeUrl,
                    EstadoCodigo  = est.Codigo,
                    EstadoNombre  = est.Nombre,
                }).ToListAsync();

            return new RevisionLongListDto
            {
                RequerimientoId = head.GthRequerimientoId,
                Codigo          = head.Codigo,
                Puesto          = head.Puesto,
                Area            = head.Area,
                ProyectoObra    = head.ProyectoObra,
                EstadoCodigo    = head.EstadoCodigo,
                EstadoNombre    = head.EstadoNombre,
                Candidatos      = candidatos,
            };
        }

        public async Task<LongListDecisionContextoDto> RegistrarDecisionLongList(
            int requerimientoId, List<CandidatoDecisionDto> decisiones, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Cabecera + estado actual, con scope al solicitante dueño de la solicitud. Se trae la
            // entidad del requerimiento (r) para mutarla; EF la rastrea aunque venga en un anónimo.
            var head = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId
                      && r.State && r.Solicitud!.State
                      && r.Solicitud.SolicitanteUserId == userId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                select new
                {
                    Req          = r,
                    r.Codigo,
                    Puesto       = p.Nombre,
                    Area         = r.Solicitud!.AreaNombre,
                    ProyectoObra = pr.ProjectDescription,
                    EstadoCodigo = e.Codigo,
                }).FirstOrDefaultAsync();

            if (head == null)
                throw new AbrilException("No se encontró la long list del requerimiento.", 404);

            // La decisión solo se registra una vez, cuando GTH ya envió la long list (LONG_LIST_ENVIADA).
            if (head.EstadoCodigo != EstadoReclutamiento.LongListEnviada)
                throw new AbrilException("Esta long list ya fue revisada o aún no está disponible para revisión.", 409);

            var candidatos = await ctx.GthCandidato
                .Where(c => c.GthRequerimientoId == requerimientoId && c.State)
                .OrderBy(c => c.Orden).ThenBy(c => c.GthCandidatoId)
                .ToListAsync();
            if (candidatos.Count == 0)
                throw new AbrilException("La long list no tiene candidatos para revisar.", 400);

            // Decisión recibida por candidato; deben cubrir exactamente a los candidatos vigentes.
            var decisionPorId = new Dictionary<int, bool>();
            foreach (var d in decisiones) decisionPorId[d.CandidatoId] = d.Aprobado;
            if (candidatos.Any(c => !decisionPorId.ContainsKey(c.GthCandidatoId)))
                throw new AbrilException("Debes aprobar o rechazar a todos los candidatos antes de enviar la decisión.", 400);

            var estadosCand = await ctx.GthCandidatoEstado
                .Where(e => e.State && (e.Codigo == EstadoCandidato.Aprobado || e.Codigo == EstadoCandidato.Rechazado))
                .ToListAsync();
            var aprobadoId = estadosCand.FirstOrDefault(e => e.Codigo == EstadoCandidato.Aprobado)?.GthCandidatoEstadoId
                ?? throw new AbrilException("No está configurado el estado APROBADO de candidatos.", 500);
            var rechazadoId = estadosCand.FirstOrDefault(e => e.Codigo == EstadoCandidato.Rechazado)?.GthCandidatoEstadoId
                ?? throw new AbrilException("No está configurado el estado RECHAZADO de candidatos.", 500);

            var now = DateTimeOffset.UtcNow;
            int aprobados = 0, rechazados = 0;
            foreach (var c in candidatos)
            {
                var aprobado = decisionPorId[c.GthCandidatoId];
                c.GthCandidatoEstadoId = aprobado ? aprobadoId : rechazadoId;
                c.UpdatedDateTime      = now;
                c.UpdatedUserId        = userId;
                if (aprobado) aprobados++; else rechazados++;
            }

            // ≥1 aprobado → LONG_LIST_APROBADA. 0 aprobados → de vuelta a LONG_LIST para que GTH
            // vuelva a enviar una long list (los rechazados quedan grabados como data histórica).
            var todosRechazados = aprobados == 0;
            var codigoDestino   = todosRechazados ? EstadoReclutamiento.LongList : EstadoReclutamiento.LongListAprobada;
            var estadoDestino   = await ctx.GthEstadoRequerimiento
                .FirstOrDefaultAsync(e => e.Codigo == codigoDestino && e.State)
                ?? throw new AbrilException($"No está configurado el estado {codigoDestino} de reclutamiento.", 500);

            head.Req.GthEstadoRequerimientoId = estadoDestino.GthEstadoRequerimientoId;
            head.Req.UpdatedDateTime          = now;
            head.Req.UpdatedUserId            = userId;

            // Nombre del solicitante para el cuerpo del correo (best-effort; no bloquea).
            var solicitanteNombre = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => w.Person!.FullName ?? w.ApellidoNombre)
                .FirstOrDefaultAsync();

            await ctx.SaveChangesAsync();

            return new LongListDecisionContextoDto
            {
                Resultado = new LongListDecisionResultDto
                {
                    EstadoCodigo    = estadoDestino.Codigo,
                    EstadoNombre    = estadoDestino.Nombre,
                    Aprobados       = aprobados,
                    Rechazados      = rechazados,
                    TodosRechazados = todosRechazados,
                },
                Codigo            = head.Codigo,
                Puesto            = head.Puesto,
                Area              = head.Area,
                ProyectoObra      = head.ProyectoObra,
                SolicitanteNombre = solicitanteNombre,
                Candidatos        = candidatos.Select(c => new CandidatoDecididoDto
                {
                    Nombre   = c.Nombre,
                    Puesto   = c.Puesto,
                    Aprobado = decisionPorId[c.GthCandidatoId],
                }).ToList(),
            };
        }

        public async Task<BandejaReclutamientoDto> GetBandeja()
        {
            using var ctx = _factory.CreateDbContext();

            // Todos los requerimientos vigentes (de cualquier área), más recientes primero, EXCEPTO
            // los que todavía no le pertenecen a GTH: los que esperan la aprobación de Gerencia
            // General y los que el GG rechazó (EstadoReclutamiento.FueraDeGth). GTH solo ve lo
            // aprobado. Como el embudo del pipeline se calcula sobre esta misma lista, la suma de
            // las etapas sigue siendo el total de la tabla.
            // Left join a gth_prioridad porque la prioridad es opcional (nullable).
            var raw = await (
                from r in ctx.GthRequerimiento
                where r.State && r.Solicitud!.State
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                where !EstadoReclutamiento.FueraDeGth.Contains(e.Codigo)
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

            // "Evaluaciones · Programadas": entrevistas ya agendadas cuyo resultado GTH todavía no
            // cierra (sin evaluación registrada, o registrada pero aún en PENDIENTE). Una vez que el
            // candidato queda en PASO / NO_PASO / SELECCIONADO / RECHAZADO deja de estar programada.
            var evaluacionesProgramadas = await (
                from ent in ctx.GthEntrevista
                join c in ctx.GthCandidato on ent.GthCandidatoId equals c.GthCandidatoId
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                where ent.State && c.State && r.State
                      && !(from ev in ctx.GthCandidatoEvaluacion
                           join res in ctx.GthCandidatoResultado
                               on ev.GthCandidatoResultadoId equals res.GthCandidatoResultadoId
                           where ev.GthCandidatoId == c.GthCandidatoId && ev.State
                                 && res.Codigo != ResultadoCandidato.Pendiente
                           select ev.GthCandidatoEvaluacionId).Any()
                select ent.GthEntrevistaId).CountAsync();

            // ── Embudo del pipeline y tarjetas de resumen ─────────────────────────────────
            // Se calculan sobre `solicitudes`, que ya está materializado (sin roundtrips extra).
            var totalPorFase = solicitudes
                .GroupBy(s => s.EstadoCodigo)
                .ToDictionary(g => g.Key, g => g.Count());

            var pipeline = EtapaPipeline.Todas
                .Select(etapa => new PipelineEtapaDto
                {
                    Codigo = etapa.Codigo,
                    Nombre = etapa.Nombre,
                    Total  = etapa.Fases.Sum(f => totalPorFase.GetValueOrDefault(f)),
                })
                .ToList();

            var cerrados = pipeline.First(e => e.Codigo == EtapaPipeline.CodigoCierre).Total;

            // "Vacantes abiertas" = ya publicadas y todavía en curso: las etapas desde Publicado
            // hasta la anterior a Cierre. Se recorta por posición para no repetir la lista de fases.
            var desdePublicado = Array.FindIndex(EtapaPipeline.Todas, e => e.Codigo == EtapaPipeline.CodigoPublicado);
            var hastaCierre    = Array.FindIndex(EtapaPipeline.Todas, e => e.Codigo == EtapaPipeline.CodigoCierre);
            var vacantesAbiertas = pipeline
                .Take(hastaCierre)
                .Skip(desdePublicado)
                .Sum(e => e.Total);

            // "Este período" = año en curso (hora Perú), el mismo que rotula la cabecera de la página.
            var anioActual = DateTimeOffset.UtcNow.ToOffset(PeruOffset).Year;

            var resumen = new ResumenReclutamientoDto
            {
                // Lo cerrado ya no está en curso, así que sale de "En proceso".
                EnProceso               = solicitudes.Count - cerrados,
                VacantesAbiertas        = vacantesAbiertas,
                EvaluacionesProgramadas = evaluacionesProgramadas,
                ProcesosCerrados        = solicitudes.Count(s => s.EstadoCodigo == EstadoReclutamiento.Cerrado
                                                                 && s.FechaLlegada.Year == anioActual),
                // "Solicitudes nuevas" = las que acaban de llegar a GTH. Con el paso de Gerencia
                // General eso es VALIDACION_GTH; se sigue contando NUEVO por los requerimientos
                // anteriores a ese cambio, que se quedaron en esa fase.
                SolicitudesNuevas       = totalPorFase.GetValueOrDefault(EstadoReclutamiento.Nuevo)
                                          + totalPorFase.GetValueOrDefault(EstadoReclutamiento.ValidacionGth),
            };

            return new BandejaReclutamientoDto
            {
                Resumen     = resumen,
                Pipeline    = pipeline,
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

        /// <summary>
        /// Tope de trabajadores por razón social para el cálculo de cupos. Cuentan los de Staff,
        /// Oficina Central y Personal Externo
        /// (<see cref="ObraOficinaStaffIds.ConsumenCupoRazonSocial"/>); el personal de Obra y los
        /// practicantes no consumen cupo.
        /// </summary>
        private const int TopeCuposRazonSocial = 20;

        public async Task<DetalleRequerimientoGthDto?> GetDetalleGth(int requerimientoId)
        {
            using var ctx = _factory.CreateDbContext();

            // Cabecera + asignación interna actual (sin scope por usuario: es la vista de GTH).
            var head = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId && r.State && r.Solicitud!.State
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
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

            // Ocupación por razón social desde la base maestra: trabajadores no retirados de
            // Staff, Oficina Central o Personal Externo. El personal de Obra NO consume el tope
            // (el tope de 20 es de planilla de escritorio, y contando obreros toda razón social
            // con un proyecto en curso quedaba en 0 cupos). Los practicantes tampoco consumen.
            //
            // El practicante se detecta por `categoria_maestra_id`, no por el texto libre
            // `workers.categoria`: ese campo guarda el nivel del puesto (Operario, Arquitecto…)
            // y se desincroniza — había practicantes con "Arquitecto" contando cupo y empleados
            // que habían sido practicantes y seguían con el texto viejo sin contar. Los que no
            // tienen categoría maestra sí consumen (no son practicantes).
            var ocupados = await ctx.Worker
                .Where(w => w.ContributorId != null
                            && w.Estado != "RETIRADO"
                            && ObraOficinaStaffIds.ConsumenCupoRazonSocial.Contains(w.ObraOficinaStaffId ?? 0)
                            && w.CategoriaMaestraId != CategoriaMaestraIds.PracticantePrePro)
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
                    Id     = c.GthCanalPublicacionId,
                    Nombre = c.Nombre,
                })
                .ToListAsync();

            var publicados = await ctx.GthRequerimientoCanal
                .Where(rc => rc.GthRequerimientoId == requerimientoId && rc.State && rc.Active)
                .Select(rc => rc.GthCanalPublicacionId)
                .ToListAsync();
            foreach (var c in canales)
                c.Publicado = publicados.Contains(c.Id);

            // Candidatos aprobados por el solicitante (para la fase "Long list aprobada", imágenes 4/5).
            // Vacío en fases anteriores; los rechazados no se incluyen.
            var candidatosAprobadosRaw = await (
                from c in ctx.GthCandidato
                where c.GthRequerimientoId == requerimientoId && c.State
                join est in ctx.GthCandidatoEstado on c.GthCandidatoEstadoId equals est.GthCandidatoEstadoId
                where est.Codigo == EstadoCandidato.Aprobado
                orderby c.Orden, c.GthCandidatoId
                select new
                {
                    c.GthCandidatoId,
                    c.Nombre,
                    c.Puesto,
                    c.MultitestRealizado,
                }).ToListAsync();

            // Formulario del postulante de cada candidato aprobado (0..1 por candidato) para saber, por
            // candidato, si GTH ya lo envió y en qué fase está. Se trae en un segundo roundtrip pequeño
            // (acotado a los candidatos aprobados) en vez de un left join a subconsulta: EF Core no
            // materializa bien ese patrón —proyección de la subconsulta con value types nullable +
            // "fr != null ? fr.Campo : null"— y lanza "Nullable object must have a value" al iterar
            // cuando el candidato aún no tiene formulario. Se une en memoria por GthCandidatoId.
            var candidatoIds = candidatosAprobadosRaw.Select(x => x.GthCandidatoId).ToList();
            var formulariosRaw = candidatoIds.Count == 0
                ? new List<FormularioRawRow>()
                : await (
                    from f in ctx.GthPostulanteFormulario
                    where f.State && candidatoIds.Contains(f.GthCandidatoId)
                    join fe in ctx.GthPostulanteFormularioEstado on f.GthPostulanteFormularioEstadoId equals fe.GthPostulanteFormularioEstadoId
                    select new FormularioRawRow
                    {
                        GthCandidatoId     = f.GthCandidatoId,
                        EstadoCodigo       = fe.Codigo,
                        EstadoNombre       = fe.Nombre,
                        CorreoEnvio        = f.CorreoEnvio,
                        CorreoElectronico  = f.CorreoElectronico,
                        EnviadoDateTime    = f.EnviadoDateTime,
                        CompletadoDateTime = f.CompletadoDateTime,
                        RevisadoNombre     = f.RevisadoNombre,
                        RevisadoDateTime   = f.RevisadoDateTime,
                    }).ToListAsync();

            var formulariosPorCandidato = formulariosRaw.ToDictionary(x => x.GthCandidatoId, x => new CandidatoFormularioResumenDto
            {
                EstadoCodigo   = x.EstadoCodigo,
                EstadoNombre   = x.EstadoNombre,
                CorreoEnvio    = x.CorreoEnvio,
                EnviadoEn      = x.EnviadoDateTime?.ToOffset(PeruOffset).DateTime,
                CompletadoEn   = x.CompletadoDateTime?.ToOffset(PeruOffset).DateTime,
                RevisadoNombre = x.RevisadoNombre,
                RevisadoEn     = x.RevisadoDateTime?.ToOffset(PeruOffset).DateTime,
            });

            // Correo de contacto para la invitación a la entrevista: el que el postulante declaró
            // en el formulario y, si no lo declaró, aquel al que GTH le envió el enlace.
            var correosPorCandidato = formulariosRaw.ToDictionary(
                x => x.GthCandidatoId,
                x => string.IsNullOrWhiteSpace(x.CorreoElectronico) ? x.CorreoEnvio : x.CorreoElectronico!);

            // Entrevista programada de cada candidato (0..1 vigente por candidato).
            var entrevistasPorCandidato = candidatoIds.Count == 0
                ? new Dictionary<int, EntrevistaResumenDto>()
                : (await (
                        from en in ctx.GthEntrevista
                        where en.State && candidatoIds.Contains(en.GthCandidatoId)
                        join l in ctx.GthLugarEntrevista on en.GthLugarEntrevistaId equals l.GthLugarEntrevistaId
                        select new
                        {
                            en.GthCandidatoId,
                            en.Fecha,
                            en.Hora,
                            en.GthLugarEntrevistaId,
                            LugarNombre = l.Nombre,
                            en.CorreoEnvio,
                            en.EnviadoDateTime,
                        }).ToListAsync())
                    .ToDictionary(x => x.GthCandidatoId, x => new EntrevistaResumenDto
                    {
                        Fecha       = x.Fecha,
                        Hora        = x.Hora.ToString("HH\\:mm"),
                        LugarId     = x.GthLugarEntrevistaId,
                        LugarNombre = x.LugarNombre,
                        CorreoEnvio = x.CorreoEnvio,
                        EnviadoEn   = x.EnviadoDateTime?.ToOffset(PeruOffset).DateTime,
                    });

            // Evaluación de la entrevista de cada candidato (0..1 vigente por candidato): puntajes,
            // comentarios del informe y resultado (incluido el correo de agradecimiento si no continúa).
            var evaluacionesPorCandidato = (await QueryEvaluaciones(ctx, candidatoIds))
                .ToDictionary(x => x.GthCandidatoId, MapEvaluacion);

            var candidatosAprobados = candidatosAprobadosRaw.Select(x => new CandidatoAprobadoDto
            {
                CandidatoId        = x.GthCandidatoId,
                Nombre             = x.Nombre,
                Puesto             = x.Puesto,
                Formulario         = formulariosPorCandidato.GetValueOrDefault(x.GthCandidatoId),
                MultitestRealizado = x.MultitestRealizado,
                CorreoContacto     = correosPorCandidato.GetValueOrDefault(x.GthCandidatoId),
                Entrevista         = entrevistasPorCandidato.GetValueOrDefault(x.GthCandidatoId),
                Evaluacion         = evaluacionesPorCandidato.GetValueOrDefault(x.GthCandidatoId),
            }).ToList();

            // Catálogo de lugares para el desplegable de programación de entrevistas.
            var lugaresEntrevista = await ctx.GthLugarEntrevista
                .Where(l => l.State && l.Active)
                .OrderBy(l => l.Orden).ThenBy(l => l.Nombre)
                .Select(l => new OpcionDto { Id = l.GthLugarEntrevistaId, Nombre = l.Nombre })
                .ToListAsync();

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
                RazonesSociales     = razonesSociales,
                Canales             = canales,
                LugaresEntrevista   = lugaresEntrevista,
                CandidatosAprobados = candidatosAprobados,
            };
        }

        public async Task SetMultitest(int candidatoId, bool realizado, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var cand = await ctx.GthCandidato
                .FirstOrDefaultAsync(c => c.GthCandidatoId == candidatoId && c.State);
            if (cand == null)
                throw new AbrilException("Candidato no encontrado.", 404);

            var now = DateTimeOffset.UtcNow;
            cand.MultitestRealizado = realizado;
            cand.MultitestDateTime  = now;
            cand.MultitestUserId    = userId;
            cand.UpdatedDateTime    = now;
            cand.UpdatedUserId      = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task<EstadoRequerimientoResultDto> ContinuarAEntrevistas(int requerimientoId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var req = await ctx.GthRequerimiento
                .FirstOrDefaultAsync(r => r.GthRequerimientoId == requerimientoId && r.State);
            if (req == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            var estados = await ctx.GthEstadoRequerimiento
                .Where(e => e.State && (e.Codigo == EstadoReclutamiento.LongListAprobada
                                        || e.Codigo == EstadoReclutamiento.Entrevistas
                                        || e.GthEstadoRequerimientoId == req.GthEstadoRequerimientoId))
                .ToListAsync();
            var longListAprobada = estados.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.LongListAprobada)
                ?? throw new AbrilException("No está configurado el estado LONG_LIST_APROBADA de reclutamiento.", 500);
            var entrevistas = estados.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.Entrevistas)
                ?? throw new AbrilException("No está configurado el estado ENTREVISTAS de reclutamiento.", 500);
            var actual = estados.FirstOrDefault(e => e.GthEstadoRequerimientoId == req.GthEstadoRequerimientoId);

            // Idempotente: si ya está en Entrevistas (o más adelante) no se retrocede ni se revalida.
            if (actual != null && actual.Orden >= entrevistas.Orden)
                return new EstadoRequerimientoResultDto { EstadoCodigo = actual.Codigo, EstadoNombre = actual.Nombre };

            if (actual == null || actual.Orden < longListAprobada.Orden)
                throw new AbrilException("El solicitante aún no aprobó la long list de este requerimiento.", 400);

            // Requisitos para pasar a entrevistas (mismos que habilitan el botón en la vista de GTH):
            // Multitest marcado en todos los candidatos aprobados, todos los formularios ya revisados
            // (aprobados o rechazados) y al menos uno aprobado.
            var candidatos = await (
                from c in ctx.GthCandidato
                where c.GthRequerimientoId == requerimientoId && c.State
                join est in ctx.GthCandidatoEstado on c.GthCandidatoEstadoId equals est.GthCandidatoEstadoId
                where est.Codigo == EstadoCandidato.Aprobado
                select new { c.GthCandidatoId, c.MultitestRealizado }).ToListAsync();

            if (candidatos.Count == 0)
                throw new AbrilException("No hay candidatos aprobados por el solicitante en este requerimiento.", 400);
            if (candidatos.Any(c => !c.MultitestRealizado))
                throw new AbrilException("Marca el Multitest de todos los candidatos antes de continuar.", 400);

            // Estado del formulario de cada candidato (segundo roundtrip acotado: el left join
            // encadenado formulario→estado no lo materializa bien EF Core, ver GetDetalleGth).
            var idsCandidatos = candidatos.Select(c => c.GthCandidatoId).ToList();
            var estadoFormPorCandidato = (await (
                    from f in ctx.GthPostulanteFormulario
                    where f.State && idsCandidatos.Contains(f.GthCandidatoId)
                    join fe in ctx.GthPostulanteFormularioEstado on f.GthPostulanteFormularioEstadoId equals fe.GthPostulanteFormularioEstadoId
                    select new { f.GthCandidatoId, fe.Codigo }).ToListAsync())
                .ToDictionary(x => x.GthCandidatoId, x => x.Codigo);

            var estadosForm = idsCandidatos.Select(id => estadoFormPorCandidato.GetValueOrDefault(id)).ToList();
            if (estadosForm.Any(e => e != EstadoFormularioPostulante.Aprobado
                                     && e != EstadoFormularioPostulante.Rechazado))
                throw new AbrilException("Todos los formularios del postulante deben estar aprobados o rechazados antes de continuar.", 400);
            if (!estadosForm.Any(e => e == EstadoFormularioPostulante.Aprobado))
                throw new AbrilException("Se necesita al menos un formulario del postulante aprobado para programar entrevistas.", 400);

            req.GthEstadoRequerimientoId = entrevistas.GthEstadoRequerimientoId;
            req.UpdatedDateTime          = DateTimeOffset.UtcNow;
            req.UpdatedUserId            = userId;
            await ctx.SaveChangesAsync();

            return new EstadoRequerimientoResultDto
            {
                EstadoCodigo = entrevistas.Codigo,
                EstadoNombre = entrevistas.Nombre,
            };
        }

        public async Task<EntrevistaEnvioContextoDto> GuardarEntrevista(
            int candidatoId, DateOnly fecha, TimeOnly hora, int lugarId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Candidato + puesto/código del requerimiento.
            var cand = await (
                from c in ctx.GthCandidato
                where c.GthCandidatoId == candidatoId && c.State
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                select new { c.Nombre, Puesto = p.Nombre, r.Codigo }).FirstOrDefaultAsync();
            if (cand == null)
                throw new AbrilException("Candidato no encontrado.", 404);

            // Formulario del postulante (debe estar aprobado) y correo al que se cita.
            var form = await (
                from f in ctx.GthPostulanteFormulario
                where f.GthCandidatoId == candidatoId && f.State
                join fe in ctx.GthPostulanteFormularioEstado on f.GthPostulanteFormularioEstadoId equals fe.GthPostulanteFormularioEstadoId
                select new { EstadoCodigo = fe.Codigo, f.CorreoElectronico, f.CorreoEnvio }).FirstOrDefaultAsync();

            if (form?.EstadoCodigo != EstadoFormularioPostulante.Aprobado)
                throw new AbrilException("Solo se programa entrevista a los candidatos con el formulario del postulante aprobado.", 400);

            var correo = string.IsNullOrWhiteSpace(form.CorreoElectronico) ? form.CorreoEnvio : form.CorreoElectronico;
            if (string.IsNullOrWhiteSpace(correo))
                throw new AbrilException("El candidato no tiene un correo registrado al cual enviar la invitación.", 400);

            var lugar = await ctx.GthLugarEntrevista
                .FirstOrDefaultAsync(l => l.GthLugarEntrevistaId == lugarId && l.State && l.Active);
            if (lugar == null)
                throw new AbrilException("El lugar de entrevista seleccionado no es válido.", 400);

            var now = DateTimeOffset.UtcNow;

            // Una sola entrevista vigente por candidato: reprogramar actualiza esta misma fila.
            var entrevista = await ctx.GthEntrevista
                .FirstOrDefaultAsync(e => e.GthCandidatoId == candidatoId && e.State);
            if (entrevista == null)
            {
                entrevista = new GthEntrevista
                {
                    GthCandidatoId       = candidatoId,
                    Fecha                = fecha,
                    Hora                 = hora,
                    GthLugarEntrevistaId = lugarId,
                    CorreoEnvio          = correo,
                    EnviadoDateTime      = now,
                    EnviadoUserId        = userId,
                    CreatedDateTime      = now,
                    CreatedUserId        = userId,
                    Active               = true,
                    State                = true,
                };
                ctx.GthEntrevista.Add(entrevista);
            }
            else
            {
                entrevista.Fecha                = fecha;
                entrevista.Hora                 = hora;
                entrevista.GthLugarEntrevistaId = lugarId;
                entrevista.CorreoEnvio          = correo;
                entrevista.EnviadoDateTime      = now;
                entrevista.EnviadoUserId        = userId;
                entrevista.UpdatedDateTime      = now;
                entrevista.UpdatedUserId        = userId;
            }

            await ctx.SaveChangesAsync();

            return new EntrevistaEnvioContextoDto
            {
                CandidatoNombre = cand.Nombre,
                Puesto          = cand.Puesto,
                Codigo          = cand.Codigo,
                Resumen = new EntrevistaResumenDto
                {
                    Fecha       = fecha,
                    Hora        = hora.ToString("HH\\:mm"),
                    LugarId     = lugarId,
                    LugarNombre = lugar.Nombre,
                    CorreoEnvio = correo,
                    EnviadoEn   = now.ToOffset(PeruOffset).DateTime,
                },
            };
        }

        // ── Evaluación de la entrevista (puntajes, informe y no continuidad) ──
        /// <summary>Texto opcional normalizado: sin espacios sobrantes y null si queda vacío.</summary>
        private static string? Limpiar(string? texto) =>
            string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        /// <summary>
        /// Cabecera del candidato para evaluarlo: nombre, puesto/código del requerimiento y correo
        /// de la entrevista programada. Lanza 404 si no existe y 400 si aún no se le envió la
        /// invitación (solo se evalúa a quien ya fue citado).
        /// </summary>
        private static async Task<(string Nombre, string Puesto, string Codigo, string Correo)> GetCandidatoEntrevistado(
            AppDbContext ctx, int candidatoId)
        {
            var cand = await (
                from c in ctx.GthCandidato
                where c.GthCandidatoId == candidatoId && c.State
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                select new { c.Nombre, Puesto = p.Nombre, r.Codigo }).FirstOrDefaultAsync();
            if (cand == null)
                throw new AbrilException("Candidato no encontrado.", 404);

            var correo = await ctx.GthEntrevista
                .Where(e => e.GthCandidatoId == candidatoId && e.State)
                .Select(e => e.CorreoEnvio)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(correo))
                throw new AbrilException("Primero envía la invitación a la entrevista de este candidato.", 400);

            return (cand.Nombre, cand.Puesto, cand.Codigo, correo);
        }

        /// <summary>
        /// Evaluación vigente del candidato, creándola en PENDIENTE si aún no existe. No guarda:
        /// el llamador ajusta los campos y hace el <c>SaveChangesAsync</c>.
        /// </summary>
        private static async Task<GthCandidatoEvaluacion> GetOrCreateEvaluacion(
            AppDbContext ctx, int candidatoId, int? userId, DateTimeOffset now)
        {
            var evaluacion = await ctx.GthCandidatoEvaluacion
                .FirstOrDefaultAsync(e => e.GthCandidatoId == candidatoId && e.State);
            if (evaluacion != null)
            {
                evaluacion.UpdatedDateTime = now;
                evaluacion.UpdatedUserId   = userId;
                return evaluacion;
            }

            var pendienteId = await ctx.GthCandidatoResultado
                .Where(r => r.Codigo == ResultadoCandidato.Pendiente && r.State)
                .Select(r => (int?)r.GthCandidatoResultadoId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No está configurado el resultado PENDIENTE de la entrevista.", 500);

            evaluacion = new GthCandidatoEvaluacion
            {
                GthCandidatoId          = candidatoId,
                GthCandidatoResultadoId = pendienteId,
                CreatedDateTime         = now,
                CreatedUserId           = userId,
                Active                  = true,
                State                   = true,
            };
            ctx.GthCandidatoEvaluacion.Add(evaluacion);
            return evaluacion;
        }

        /// <summary>Resumen de la evaluación ya persistida (relee el catálogo para el nombre del resultado).</summary>
        private static async Task<EvaluacionResumenDto> BuildEvaluacionResumen(AppDbContext ctx, GthCandidatoEvaluacion e)
        {
            var resultado = await ctx.GthCandidatoResultado
                .Where(r => r.GthCandidatoResultadoId == e.GthCandidatoResultadoId)
                .Select(r => new { r.Codigo, r.Nombre })
                .FirstAsync();

            return new EvaluacionResumenDto
            {
                PuntajeEntrevista       = e.PuntajeEntrevista,
                PuntajePsicotecnico     = e.PuntajePsicotecnico,
                PuntajeTecnica          = e.PuntajeTecnica,
                PuntajeResultado        = e.PuntajeResultado,
                ComentarioEntrevista    = e.ComentarioEntrevista,
                ComentarioPsicotecnico  = e.ComentarioPsicotecnico,
                ComentarioRecomendacion = e.ComentarioRecomendacion,
                ResultadoCodigo         = resultado.Codigo,
                ResultadoNombre         = resultado.Nombre,
                AgradecimientoCorreo    = e.AgradecimientoCorreo,
                AgradecimientoEnviadoEn = e.AgradecimientoDateTime?.ToOffset(PeruOffset).DateTime,
            };
        }

        public async Task<EvaluacionResumenDto> GuardarEvaluacion(int candidatoId, EvaluacionGuardarDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            await GetCandidatoEntrevistado(ctx, candidatoId);

            var now = DateTimeOffset.UtcNow;
            var evaluacion = await GetOrCreateEvaluacion(ctx, candidatoId, userId, now);

            // Un candidato descartado por GTH o ya decidido por el solicitante tiene su resultado
            // cerrado: el informe no se sigue editando.
            var resultadoActual = await ctx.GthCandidatoResultado
                .Where(r => r.GthCandidatoResultadoId == evaluacion.GthCandidatoResultadoId)
                .Select(r => r.Codigo)
                .FirstOrDefaultAsync();
            if (resultadoActual == ResultadoCandidato.NoPaso)
                throw new AbrilException("El candidato ya no continúa en el proceso: su evaluación quedó cerrada.", 400);
            if (resultadoActual is ResultadoCandidato.Seleccionado or ResultadoCandidato.Rechazado)
                throw new AbrilException("El área solicitante ya tomó una decisión sobre este finalista: su informe quedó cerrado.", 400);

            // Guardar el informe es, a la vez, enviarlo como finalista: el candidato pasa a PASO y
            // desde ahí lo ve el área solicitante.
            if (resultadoActual != ResultadoCandidato.Paso)
            {
                evaluacion.GthCandidatoResultadoId = await ctx.GthCandidatoResultado
                    .Where(r => r.Codigo == ResultadoCandidato.Paso && r.State)
                    .Select(r => (int?)r.GthCandidatoResultadoId)
                    .FirstOrDefaultAsync()
                    ?? throw new AbrilException("No está configurado el resultado PASO de la entrevista.", 500);
            }

            evaluacion.PuntajeEntrevista       = dto.PuntajeEntrevista;
            evaluacion.PuntajePsicotecnico     = dto.PuntajePsicotecnico;
            evaluacion.PuntajeTecnica          = dto.PuntajeTecnica;
            evaluacion.PuntajeResultado        = dto.PuntajeResultado;
            evaluacion.ComentarioEntrevista    = Limpiar(dto.ComentarioEntrevista);
            evaluacion.ComentarioPsicotecnico  = Limpiar(dto.ComentarioPsicotecnico);
            evaluacion.ComentarioRecomendacion = Limpiar(dto.ComentarioRecomendacion);

            await ctx.SaveChangesAsync();
            return await BuildEvaluacionResumen(ctx, evaluacion);
        }

        public async Task<AgradecimientoEnvioContextoDto> RegistrarAgradecimiento(int candidatoId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var cand = await GetCandidatoEntrevistado(ctx, candidatoId);

            var noPasoId = await ctx.GthCandidatoResultado
                .Where(r => r.Codigo == ResultadoCandidato.NoPaso && r.State)
                .Select(r => (int?)r.GthCandidatoResultadoId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No está configurado el resultado NO_PASO de la entrevista.", 500);

            var now = DateTimeOffset.UtcNow;
            var evaluacion = await GetOrCreateEvaluacion(ctx, candidatoId, userId, now);

            evaluacion.GthCandidatoResultadoId = noPasoId;
            evaluacion.AgradecimientoCorreo    = cand.Correo;
            evaluacion.AgradecimientoDateTime  = now;
            evaluacion.AgradecimientoUserId    = userId;

            await ctx.SaveChangesAsync();

            return new AgradecimientoEnvioContextoDto
            {
                CandidatoNombre = cand.Nombre,
                Puesto          = cand.Puesto,
                Codigo          = cand.Codigo,
                Correo          = cand.Correo,
                Resumen         = await BuildEvaluacionResumen(ctx, evaluacion),
            };
        }

        public async Task<RevisionFinalistasDto?> GetRevisionFinalistas(int requerimientoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Cabecera del requerimiento (scope: solo del usuario dueño de la solicitud).
            var head = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId
                      && r.State && r.Solicitud!.State
                      && r.Solicitud.SolicitanteUserId == userId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                select new
                {
                    r.GthRequerimientoId,
                    r.Codigo,
                    Puesto       = p.Nombre,
                    Area         = r.Solicitud!.AreaNombre,
                    ProyectoObra = pr.ProjectDescription,
                    EstadoCodigo = e.Codigo,
                    EstadoNombre = e.Nombre,
                }).FirstOrDefaultAsync();

            if (head == null) return null;

            // Finalistas: candidatos aprobados en la long list con evaluación registrada por GTH.
            // Los que no continúan (resultado NO_PASO, con su correo de agradecimiento ya enviado)
            // no se muestran al solicitante.
            var candidatos = await (
                from c in ctx.GthCandidato
                where c.GthRequerimientoId == requerimientoId && c.State
                join est in ctx.GthCandidatoEstado on c.GthCandidatoEstadoId equals est.GthCandidatoEstadoId
                where est.Codigo == EstadoCandidato.Aprobado
                join ev in ctx.GthCandidatoEvaluacion on c.GthCandidatoId equals ev.GthCandidatoId
                where ev.State
                join res in ctx.GthCandidatoResultado on ev.GthCandidatoResultadoId equals res.GthCandidatoResultadoId
                where res.Codigo != ResultadoCandidato.NoPaso
                select new
                {
                    c.GthCandidatoId,
                    c.Nombre,
                    c.Puesto,
                    c.CvNombre,
                    c.CvUrl,
                    Evaluacion = new EvaluacionRawRow
                    {
                        GthCandidatoId          = c.GthCandidatoId,
                        PuntajeEntrevista       = ev.PuntajeEntrevista,
                        PuntajePsicotecnico     = ev.PuntajePsicotecnico,
                        PuntajeTecnica          = ev.PuntajeTecnica,
                        PuntajeResultado        = ev.PuntajeResultado,
                        ComentarioEntrevista    = ev.ComentarioEntrevista,
                        ComentarioPsicotecnico  = ev.ComentarioPsicotecnico,
                        ComentarioRecomendacion = ev.ComentarioRecomendacion,
                        ResultadoCodigo         = res.Codigo,
                        ResultadoNombre         = res.Nombre,
                        AgradecimientoCorreo    = ev.AgradecimientoCorreo,
                        AgradecimientoDateTime  = ev.AgradecimientoDateTime,
                    },
                }).ToListAsync();

            return new RevisionFinalistasDto
            {
                RequerimientoId = head.GthRequerimientoId,
                Codigo          = head.Codigo,
                Puesto          = head.Puesto,
                Area            = head.Area,
                ProyectoObra    = head.ProyectoObra,
                EstadoCodigo    = head.EstadoCodigo,
                EstadoNombre    = head.EstadoNombre,
                // Mejor puntaje de resultado primero; los que aún no lo tienen van al final.
                Finalistas      = candidatos
                    .OrderByDescending(x => x.Evaluacion.PuntajeResultado ?? -1)
                    .ThenBy(x => x.Nombre)
                    .Select(x => new FinalistaDto
                    {
                        CandidatoId = x.GthCandidatoId,
                        Nombre      = x.Nombre,
                        Puesto      = x.Puesto,
                        CvNombre    = x.CvNombre,
                        CvUrl       = x.CvUrl,
                        Evaluacion  = MapEvaluacion(x.Evaluacion),
                    }).ToList(),
            };
        }

        public async Task<FinalistaDecisionContextoDto> RegistrarDecisionFinalista(
            int requerimientoId, int candidatoId, bool aprobado, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Cabecera + estado actual, con scope al solicitante dueño de la solicitud. Se trae la
            // entidad del requerimiento (r) para mutarla; EF la rastrea aunque venga en un anónimo.
            var head = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId
                      && r.State && r.Solicitud!.State
                      && r.Solicitud.SolicitanteUserId == userId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                select new
                {
                    Req          = r,
                    r.Codigo,
                    Puesto       = p.Nombre,
                    Area         = r.Solicitud!.AreaNombre,
                    ProyectoObra = pr.ProjectDescription,
                    EstadoCodigo = e.Codigo,
                }).FirstOrDefaultAsync();

            if (head == null)
                throw new AbrilException("No se encontró el informe de finalistas del requerimiento.", 404);

            // La decisión final solo se toma mientras el requerimiento está en la fase de entrevistas
            // (después ya quedó cerrado o volvió a long list).
            if (head.EstadoCodigo != EstadoReclutamiento.Entrevistas)
                throw new AbrilException("Este proceso ya no está en la fase de decisión de finalistas.", 409);

            // Finalistas del requerimiento: candidatos con evaluación vigente que GTH no descartó.
            var finalistas = await (
                from c in ctx.GthCandidato
                where c.GthRequerimientoId == requerimientoId && c.State
                join ev in ctx.GthCandidatoEvaluacion on c.GthCandidatoId equals ev.GthCandidatoId
                where ev.State
                join res in ctx.GthCandidatoResultado on ev.GthCandidatoResultadoId equals res.GthCandidatoResultadoId
                where res.Codigo != ResultadoCandidato.NoPaso
                select new { c.GthCandidatoId, c.Nombre, Evaluacion = ev, ResultadoCodigo = res.Codigo })
                .ToListAsync();

            var elegido = finalistas.FirstOrDefault(f => f.GthCandidatoId == candidatoId)
                ?? throw new AbrilException("El candidato no forma parte de los finalistas de este requerimiento.", 404);
            if (elegido.ResultadoCodigo is ResultadoCandidato.Seleccionado or ResultadoCandidato.Rechazado)
                throw new AbrilException("Ya registraste una decisión sobre este finalista.", 409);

            var codigoResultado = aprobado ? ResultadoCandidato.Seleccionado : ResultadoCandidato.Rechazado;
            var resultadoDestino = await ctx.GthCandidatoResultado
                .Where(r => r.Codigo == codigoResultado && r.State)
                .Select(r => (int?)r.GthCandidatoResultadoId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException($"No está configurado el resultado {codigoResultado} de candidatos.", 500);

            var now = DateTimeOffset.UtcNow;
            elegido.Evaluacion.GthCandidatoResultadoId = resultadoDestino;
            elegido.Evaluacion.DecisionDateTime        = now;
            elegido.Evaluacion.DecisionUserId          = userId;
            elegido.Evaluacion.UpdatedDateTime         = now;
            elegido.Evaluacion.UpdatedUserId           = userId;

            // Al rechazar también se le manda el correo de agradecimiento (el mismo que envía GTH),
            // así que se registra su envío igual que en RegistrarAgradecimiento.
            var correoCandidato = string.Empty;
            if (!aprobado)
            {
                correoCandidato = await ctx.GthEntrevista
                    .Where(e => e.GthCandidatoId == candidatoId && e.State)
                    .Select(e => e.CorreoEnvio)
                    .FirstOrDefaultAsync() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(correoCandidato))
                {
                    elegido.Evaluacion.AgradecimientoCorreo   = correoCandidato;
                    elegido.Evaluacion.AgradecimientoDateTime = now;
                    elegido.Evaluacion.AgradecimientoUserId   = userId;
                }
            }

            // Aprobar cierra el proceso de reclutamiento (el seleccionado pasa a onboarding).
            // Rechazar al último finalista en carrera devuelve el requerimiento a LONG_LIST para que
            // GTH prepare y envíe una nueva long list; los rechazados quedan grabados como historial.
            var quedanEnCarrera = finalistas.Any(f =>
                f.GthCandidatoId != candidatoId
                && f.ResultadoCodigo is not (ResultadoCandidato.Seleccionado or ResultadoCandidato.Rechazado));
            var todosRechazados = !aprobado && !quedanEnCarrera;

            var codigoEstado = aprobado ? EstadoReclutamiento.Cerrado
                             : todosRechazados ? EstadoReclutamiento.LongList
                             : EstadoReclutamiento.Entrevistas;
            var estadoDestino = await ctx.GthEstadoRequerimiento
                .FirstOrDefaultAsync(e => e.Codigo == codigoEstado && e.State)
                ?? throw new AbrilException($"No está configurado el estado {codigoEstado} de reclutamiento.", 500);

            head.Req.GthEstadoRequerimientoId = estadoDestino.GthEstadoRequerimientoId;
            head.Req.UpdatedDateTime          = now;
            head.Req.UpdatedUserId            = userId;

            // Nombre del solicitante para el cuerpo del correo a GTH (best-effort; no bloquea).
            var solicitanteNombre = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => w.Person!.FullName ?? w.ApellidoNombre)
                .FirstOrDefaultAsync();

            await ctx.SaveChangesAsync();

            return new FinalistaDecisionContextoDto
            {
                Resultado = new FinalistaDecisionResultDto
                {
                    EstadoCodigo    = estadoDestino.Codigo,
                    EstadoNombre    = estadoDestino.Nombre,
                    Aprobado        = aprobado,
                    TodosRechazados = todosRechazados,
                    CandidatoNombre = elegido.Nombre,
                },
                Codigo            = head.Codigo,
                Puesto            = head.Puesto,
                Area              = head.Area,
                ProyectoObra      = head.ProyectoObra,
                SolicitanteNombre = solicitanteNombre,
                CandidatoCorreo   = correoCandidato,
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

        public async Task<LongListEnvioContextoDto> GetLongListEnvioContexto(int requerimientoId)
        {
            using var ctx = _factory.CreateDbContext();

            // Cabecera + estado actual + SLA del tipo de proceso, en 1 roundtrip.
            var info = await (
                from r in ctx.GthRequerimiento
                where r.GthRequerimientoId == requerimientoId && r.State && r.Solicitud!.State
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                join tp in ctx.GthTipoProceso on r.GthTipoProcesoId equals tp.GthTipoProcesoId into tpJoin
                from tp in tpJoin.DefaultIfEmpty()
                join u in ctx.User on r.Solicitud!.SolicitanteUserId equals (int?)u.UserId into uJoin
                from u in uJoin.DefaultIfEmpty()
                select new
                {
                    r.Codigo,
                    Puesto           = p.Nombre,
                    Area             = r.Solicitud!.AreaNombre,
                    ProyectoObra     = pr.ProjectDescription,
                    r.FechaRequeridaIngreso,
                    EstadoCodigo     = e.Codigo,
                    EstadoNombre     = e.Nombre,
                    EstadoOrden      = e.Orden,
                    SlaDias          = tp != null ? (int?)tp.SlaDias : null,
                    SolicitanteEmail = u != null ? u.Email : null,
                }).FirstOrDefaultAsync();

            if (info == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            // Solo se puede enviar la long list si la revisión de CV ya inició (fase LONG_LIST o
            // posterior). Reenviar cuando ya está en LONG_LIST_ENVIADA está permitido.
            var longListOrden = await ctx.GthEstadoRequerimiento
                .Where(e => e.Codigo == EstadoReclutamiento.LongList && e.State)
                .Select(e => (int?)e.Orden)
                .FirstOrDefaultAsync();
            if (longListOrden == null || info.EstadoOrden < longListOrden)
                throw new AbrilException("La revisión de CV aún no ha iniciado; no hay long list para enviar.", 400);

            return new LongListEnvioContextoDto
            {
                EstadoCodigo          = info.EstadoCodigo,
                EstadoNombre          = info.EstadoNombre,
                Codigo                = info.Codigo,
                Puesto                = info.Puesto,
                Area                  = info.Area,
                ProyectoObra          = info.ProyectoObra,
                FechaRequeridaIngreso = info.FechaRequeridaIngreso,
                SlaDias               = info.SlaDias,
                SolicitanteEmail      = info.SolicitanteEmail,
            };
        }

        public async Task<EstadoRequerimientoResultDto> GuardarLongListCandidatos(
            int requerimientoId, List<LongListCandidatoPersistDto> candidatos, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var req = await ctx.GthRequerimiento
                .FirstOrDefaultAsync(r => r.GthRequerimientoId == requerimientoId && r.State);
            if (req == null)
                throw new AbrilException("Requerimiento no encontrado.", 404);

            var estados = await ctx.GthEstadoRequerimiento
                .Where(e => e.State && (e.Codigo == EstadoReclutamiento.LongList
                                        || e.Codigo == EstadoReclutamiento.LongListEnviada
                                        || e.GthEstadoRequerimientoId == req.GthEstadoRequerimientoId))
                .ToListAsync();
            var longList        = estados.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.LongList);
            var longListEnviada = estados.FirstOrDefault(e => e.Codigo == EstadoReclutamiento.LongListEnviada)
                ?? throw new AbrilException("No está configurado el estado LONG_LIST_ENVIADA de reclutamiento.", 500);
            var actual = estados.FirstOrDefault(e => e.GthEstadoRequerimientoId == req.GthEstadoRequerimientoId);

            if (longList == null || actual == null || actual.Orden < longList.Orden)
                throw new AbrilException("La revisión de CV aún no ha iniciado; no hay long list para enviar.", 400);

            // Estado inicial de cada candidato: PENDIENTE (aún sin decisión del solicitante).
            var estadoPendienteId = await ctx.GthCandidatoEstado
                .Where(e => e.Codigo == EstadoCandidato.Pendiente && e.State)
                .Select(e => (int?)e.GthCandidatoEstadoId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No está configurado el estado PENDIENTE de candidatos.", 500);

            // El puesto del candidato es siempre el del requerimiento (el que registró el
            // solicitante): se guarda como snapshot para que la revisión no dependa del catálogo.
            var puestoRequerimiento = await ctx.Puesto
                .Where(p => p.PuestoId == req.PuestoId)
                .Select(p => p.Nombre)
                .FirstOrDefaultAsync();

            var now = DateTimeOffset.UtcNow;

            // Reemplazo: se da de baja (soft delete) la long list vigente y se inserta la nueva.
            // Permite reenviar la long list corrigiendo/actualizando los candidatos.
            var vigentes = await ctx.GthCandidato
                .Where(c => c.GthRequerimientoId == requerimientoId && c.State)
                .ToListAsync();
            foreach (var v in vigentes)
            {
                v.State           = false;
                v.UpdatedDateTime = now;
                v.UpdatedUserId   = userId;
            }

            var orden = 1;
            foreach (var c in candidatos)
            {
                ctx.GthCandidato.Add(new GthCandidato
                {
                    GthRequerimientoId   = requerimientoId,
                    Nombre               = string.IsNullOrWhiteSpace(c.Nombre) ? $"Candidato {orden}" : c.Nombre.Trim(),
                    Puesto               = puestoRequerimiento,
                    Comentario           = string.IsNullOrWhiteSpace(c.Comentario) ? null : c.Comentario.Trim(),
                    CvNombre             = c.CvNombre,
                    CvUrl                = c.CvUrl,
                    CvItemId             = c.CvItemId,
                    CvDriveId            = c.CvDriveId,
                    InformeNombre        = c.InformeNombre,
                    InformeUrl           = c.InformeUrl,
                    InformeItemId        = c.InformeItemId,
                    InformeDriveId       = c.InformeDriveId,
                    GthCandidatoEstadoId = estadoPendienteId,
                    Orden                = orden,
                    CreatedDateTime      = now,
                    CreatedUserId        = userId,
                    Active               = true,
                    State                = true,
                });
                orden++;
            }

            // Avance del pipeline a LONG_LIST_ENVIADA (idempotente: no se retrocede).
            if (actual.Orden < longListEnviada.Orden)
            {
                req.GthEstadoRequerimientoId = longListEnviada.GthEstadoRequerimientoId;
                req.UpdatedDateTime          = now;
                req.UpdatedUserId            = userId;
                actual = longListEnviada;
            }
            else
            {
                // Reenvío estando ya en LONG_LIST_ENVIADA: refresca la fecha para ordenar las tarjetas.
                req.UpdatedDateTime = now;
                req.UpdatedUserId   = userId;
            }

            await ctx.SaveChangesAsync();

            return new EstadoRequerimientoResultDto
            {
                EstadoCodigo = actual.Codigo,
                EstadoNombre = actual.Nombre,
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
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
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

            // Catálogo de fases del pipeline (las vigentes y activas, en orden). RECHAZADO_GG está
            // sembrado con active = false justamente para que no aparezca acá: es un estado del
            // requerimiento, no un paso del pipeline.
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

            // Un requerimiento rechazado por Gerencia General se quedó en esa fase: su orden (13,
            // fuera del pipeline) marcaría todas las fases como cumplidas, que es justo lo contrario.
            var rechazadoGg = head.EstadoCodigo == EstadoReclutamiento.RechazadoGg;
            var ordenEfectivo = rechazadoGg
                ? fases.FirstOrDefault(f => f.Codigo == EstadoReclutamiento.AprobacionGg)?.Orden ?? head.EstadoOrden
                : head.EstadoOrden;

            // Estado visual de cada fase respecto a la fase actual del requerimiento.
            foreach (var f in fases)
                f.Estado = f.Orden < ordenEfectivo ? "done"
                         : f.Orden == ordenEfectivo ? "current"
                         : "pending";

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
                SustentoNombre        = head.SustentoNombre,
                SustentoUrl           = head.SustentoUrl,
                Fases                 = fases,
                // Rechazado por el GG no tiene "siguiente paso": el proceso terminó ahí.
                SiguientePaso         = rechazadoGg
                    ? "Gerencia General no aprobó esta vacante. Para volver a pedirla hay que registrar una nueva solicitud."
                    : fases.FirstOrDefault(f => f.Estado == "current")?.Descripcion,
            };
        }

        /// <summary>Proyecta requerimientos (+ puesto, proyecto y estado) a filas de la tabla, en 1 roundtrip.</summary>
        private static async Task<List<SolicitudVacanteListItemDto>> ProjectRequerimientos(
            AppDbContext ctx, IQueryable<GthRequerimiento> reqs)
        {
            var raw = await (
                from r in reqs
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
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

        // ── Configuración de destinatarios del correo (por tipo: SOLICITUD / LONG_LIST) ─
        public async Task<CorreoDestinatariosDto> GetCorreoDestinatarios(string tipoCodigo)
        {
            using var ctx = _factory.CreateDbContext();
            var tipoId = await ResolveCorreoTipoId(ctx, tipoCodigo);

            // Solo los correos escritos a mano: los destinatarios dinámicos (codigo no nulo) no
            // tienen correo guardado y se resuelven al enviar, así que no le corresponden a este
            // modal — se administran desde la pantalla de Configuración.
            var rows = await ctx.GthCorreoDestinatario
                .Where(d => d.State && d.Active && d.GthCorreoTipoId == tipoId
                            && d.Codigo == null && d.Email != null)
                .OrderBy(d => d.Email)
                .Select(d => new { Email = d.Email!, d.EsCopia })
                .ToListAsync();

            return new CorreoDestinatariosDto
            {
                Principales = rows.Where(r => !r.EsCopia).Select(r => r.Email).ToList(),
                Copias      = rows.Where(r =>  r.EsCopia).Select(r => r.Email).ToList(),
            };
        }

        public async Task ReplaceCorreoDestinatarios(string tipoCodigo, List<string> principales, List<string> copias, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var tipoId = await ResolveCorreoTipoId(ctx, tipoCodigo);
            var now = DateTimeOffset.UtcNow;

            // Estado deseado: email -> esCopia (principal gana si estuviera en ambas, ya resuelto en el servicio).
            var deseado = new Dictionary<string, bool>();
            foreach (var e in principales) deseado[e] = false;
            foreach (var e in copias)      deseado.TryAdd(e, true);

            // Vigentes (state = true) del tipo — email único entre ellos por el índice parcial
            // (tipo, email). Los dinámicos quedan fuera: no tienen correo y este reemplazo masivo
            // los daría de baja como si el usuario los hubiera quitado.
            var vigentes = await ctx.GthCorreoDestinatario
                .Where(d => d.State && d.GthCorreoTipoId == tipoId
                            && d.Codigo == null && d.Email != null)
                .ToListAsync();
            var vigentesByEmail = vigentes.ToDictionary(v => v.Email!);

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
                        GthCorreoTipoId = tipoId,
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
                if (!deseado.ContainsKey(v.Email!))
                {
                    v.State = false;
                    v.UpdatedDateTime = now;
                    v.UpdatedUserId = userId;
                }
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<GerenteAreaDto?> GetGerenteDeArea(int? areaScopeId)
        {
            if (areaScopeId is not > 0) return null;

            using var ctx = _factory.CreateDbContext();

            // 1) Cadena área del solicitante → raíz. Se sube por el árbol porque el gerente casi
            //    nunca cuelga del área estándar del solicitante ("Tecnología de la Información")
            //    sino del nodo "Área de Gerencia" del que esa depende ("Gerencia de
            //    Administración"). El árbol es una tabla chica: se arma en memoria, igual que en
            //    JefeRevisorResolver.
            var scopes = await ctx.AreaScope.AsNoTracking()
                .Where(s => s.State)
                .Select(s => new { s.AreaScopeId, s.AreaScopeParentId })
                .ToListAsync();
            var parentById = scopes.ToDictionary(s => s.AreaScopeId, s => s.AreaScopeParentId);

            var cadena    = new List<int>();
            var visitados = new HashSet<int>();
            int? actual   = areaScopeId;
            while (actual != null && visitados.Add(actual.Value)) // el HashSet corta ciclos por si el árbol quedó mal
            {
                cadena.Add(actual.Value);
                parentById.TryGetValue(actual.Value, out actual);
            }
            if (cadena.Count == 0) return null;

            // 2) Gerentes vigentes de cualquier nodo de la cadena, con el nombre del área para la
            //    etiqueta del aviso. La categoría se resuelve como subconsulta para no gastar un
            //    roundtrip extra solo en leer su id.
            var candidatos = await (
                from w in ctx.Worker.AsNoTracking()
                where w.AreaScopeId != null && cadena.Contains(w.AreaScopeId.Value)
                      && w.Estado == "ACTIVO"
                      && w.EmailCorporativo != null && w.EmailCorporativo.Contains("@")
                      && w.CategoriaId == CategoriaIds.Gerente
                join s in ctx.AreaScope.AsNoTracking() on w.AreaScopeId equals s.AreaScopeId
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                select new
                {
                    w.Id,
                    AreaScopeId = w.AreaScopeId!.Value,
                    Email       = w.EmailCorporativo!,
                    Nombre      = w.Person != null ? w.Person.FullName : w.ApellidoNombre,
                    AreaNombre  = ai.AreaItemName,
                }).ToListAsync();

            // Gana el nodo más cercano al solicitante y, dentro de él, el primer trabajador por id.
            var elegido = candidatos
                .OrderBy(c => cadena.IndexOf(c.AreaScopeId))
                .ThenBy(c => c.Id)
                .FirstOrDefault();

            return elegido == null ? null : new GerenteAreaDto
            {
                WorkerId   = elegido.Id,
                Email      = elegido.Email.Trim(),
                Nombre     = elegido.Nombre,
                AreaNombre = elegido.AreaNombre,
            };
        }

        /// <summary>Resuelve el id del tipo de correo por su código estable (SOLICITUD/LONG_LIST); 500 si no está sembrado.</summary>
        private static async Task<int> ResolveCorreoTipoId(AppDbContext ctx, string tipoCodigo)
        {
            var tipoId = await ctx.GthCorreoTipo
                .Where(t => t.Codigo == tipoCodigo && t.State)
                .Select(t => (int?)t.GthCorreoTipoId)
                .FirstOrDefaultAsync();
            if (tipoId == null)
                throw new AbrilException("No está configurado el tipo de correo de reclutamiento solicitado.", 500);
            return tipoId.Value;
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

            // Estado inicial del pipeline: la solicitud nace esperando la aprobación de Gerencia
            // General (la fase NUEVO — "solicitud registrada" — queda como paso ya cumplido).
            var estadoInicialId = await ctx.GthEstadoRequerimiento
                .Where(e => e.Codigo == EstadoReclutamiento.AprobacionGg && e.State)
                .Select(e => e.GthEstadoRequerimientoId)
                .FirstOrDefaultAsync();
            if (estadoInicialId == 0)
                throw new AbrilException("No está configurado el estado inicial de reclutamiento (APROBACION_GG).", 500);

            // Prioridad por defecto al crear: Media (GTH la ajusta luego desde la bandeja). Puede ser
            // null si el catálogo aún no está sembrado; no es bloqueante.
            var prioridadMediaId = await ctx.GthPrioridad
                .Where(p => p.Codigo == PrioridadReclutamiento.Media && p.State && p.Active)
                .Select(p => (int?)p.GthPrioridadId)
                .FirstOrDefaultAsync();

            // Validar que los ids referenciados existan y estén vigentes. Los puestos solo se
            // validan contra el catálogo cuando la vacante lo eligió del desplegable: las de
            // "Puesto personalizado" traen nombre + categoría y su puesto se resuelve más abajo.
            var puestoIds  = vacantes.Where(v => !v.PuestoPersonalizado)
                                     .Select(v => v.PuestoId!.Value).Distinct().ToList();
            var categoriaIds = vacantes.Where(v => v.PuestoPersonalizado)
                                       .Select(v => v.CategoriaId!.Value).Distinct().ToList();
            var tipoIds    = vacantes.Select(v => v.TipoRequerimientoId).Distinct().ToList();
            var projectIds = vacantes.Select(v => v.ProjectId).Distinct().ToList();

            if (puestoIds.Count > 0)
            {
                var puestosOk = await ctx.Puesto.CountAsync(p => puestoIds.Contains(p.PuestoId) && p.State && p.Active);
                if (puestosOk != puestoIds.Count)
                    throw new AbrilException("Uno o más puestos seleccionados no son válidos.", 400);
            }

            if (categoriaIds.Count > 0)
            {
                var categoriasOk = await ctx.Categoria.CountAsync(c => categoriaIds.Contains(c.CategoriaId) && c.State && c.Active);
                if (categoriasOk != categoriaIds.Count)
                    throw new AbrilException("Una o más categorías seleccionadas no son válidas.", 400);
            }

            var tiposOk = await ctx.GthTipoRequerimiento.CountAsync(t => tipoIds.Contains(t.GthTipoRequerimientoId) && t.State && t.Active);
            if (tiposOk != tipoIds.Count)
                throw new AbrilException("Uno o más tipos de requerimiento no son válidos.", 400);

            var projectsOk = await ctx.Project.CountAsync(p => projectIds.Contains(p.ProjectId) && p.State && p.Active);
            if (projectsOk != projectIds.Count)
                throw new AbrilException("Uno o más proyectos/obras seleccionados no son válidos.", 400);

            // El alta de los puestos personalizados y la de la solicitud van juntas: si la solicitud
            // falla, el catálogo no se queda con puestos que nadie pidió.
            await using var tx = await ctx.Database.BeginTransactionAsync();

            var puestosPersonalizados = await ResolverPuestosPersonalizados(ctx, vacantes, userId);

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
                    PuestoId                 = v.PuestoPersonalizado
                                                   ? puestosPersonalizados[NormalizarPuestoNombre(v.PuestoNombre!)]
                                                   : v.PuestoId!.Value,
                    // El par (puesto, categoría) de la vacante: solo se guarda la categoría cuando el
                    // solicitante la declaró. Con puesto del desplegable queda null y quien contrate
                    // al seleccionado cae a puesto.categoria_id.
                    CategoriaId              = v.PuestoPersonalizado ? v.CategoriaId : null,
                    GthTipoRequerimientoId   = v.TipoRequerimientoId,
                    ProjectId                = v.ProjectId,
                    FechaRequeridaIngreso    = v.FechaRequeridaIngreso,
                    GthEstadoRequerimientoId = estadoInicialId,
                    GthPrioridadId           = prioridadMediaId,
                    CreatedDateTime          = now,
                    CreatedUserId            = userId,
                    Active                   = true,
                    State                    = true,
                });
            }

            ctx.GthSolicitud.Add(solicitud);
            await ctx.SaveChangesAsync();
            await tx.CommitAsync();

            return new SolicitudPersonalCreateResultDto
            {
                SolicitudId = solicitud.GthSolicitudId,
                Codigos     = codigos,
            };
        }

        /// <summary>
        /// Resuelve el <c>puesto_id</c> de las vacantes marcadas como "Puesto personalizado",
        /// devolviendo un mapa {nombre normalizado → puesto_id}.
        ///
        /// Si el nombre ya existe en el catálogo se reutiliza esa fila tal cual (el índice
        /// <c>ix_puesto_nombre_vivo</c> no admite dos puestos vivos con el mismo nombre, y
        /// administrar el catálogo es tarea de Habilitación → catálogos, no de este formulario):
        /// la categoría que eligió el solicitante no se le sobrescribe, porque la del puesto es solo
        /// una guía y la que manda queda en <c>gth_requerimiento.categoria_id</c>.
        /// Si no existe, se da de alta con la categoría elegida.
        /// </summary>
        private static async Task<Dictionary<string, int>> ResolverPuestosPersonalizados(
            AppDbContext ctx, List<VacanteCreateDto> vacantes, int? userId)
        {
            var resultado = new Dictionary<string, int>();

            // Dos vacantes de la misma solicitud pueden pedir el mismo puesto nuevo: se da de alta
            // una sola vez.
            var nombres = vacantes
                .Where(v => v.PuestoPersonalizado)
                .Select(v => NormalizarPuestoNombre(v.PuestoNombre!))
                .Distinct()
                .ToList();
            if (nombres.Count == 0) return resultado;

            var existentes = await ctx.Puesto
                .Where(p => p.State && nombres.Contains(p.Nombre))
                .Select(p => new { p.PuestoId, p.Nombre })
                .ToListAsync();
            foreach (var p in existentes) resultado[p.Nombre] = p.PuestoId;

            var nuevos = nombres.Where(n => !resultado.ContainsKey(n)).ToList();
            if (nuevos.Count == 0) return resultado;

            var maxOrden = await ctx.Puesto.MaxAsync(p => (int?)p.Orden) ?? 0;
            var creados = new List<Puesto>(nuevos.Count);
            foreach (var nombre in nuevos)
            {
                var categoriaId = vacantes
                    .First(v => v.PuestoPersonalizado && NormalizarPuestoNombre(v.PuestoNombre!) == nombre)
                    .CategoriaId;
                var puesto = new Puesto
                {
                    Nombre          = nombre,
                    CategoriaId     = categoriaId,
                    Orden           = ++maxOrden,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId   = userId,
                };
                creados.Add(puesto);
                ctx.Puesto.Add(puesto);
            }
            await ctx.SaveChangesAsync();

            foreach (var p in creados) resultado[p.Nombre] = p.PuestoId;
            return resultado;
        }

        /// <summary>
        /// Normaliza el nombre de un puesto escrito a mano: MAYÚSCULAS (como todo el catálogo) y
        /// espacios internos colapsados, para que "jefe  de  obra" no entre como una fila distinta
        /// de "JEFE DE OBRA" — el índice único no distingue esos casos por sí solo.
        /// </summary>
        private static string NormalizarPuestoNombre(string nombre) =>
            Regex.Replace(nombre.Trim(), @"\s+", " ").ToUpperInvariant();
    }

    /// <summary>Códigos estables de estados de reclutamiento (espejo de gth_estado_requerimiento.codigo).</summary>
    internal static class EstadoReclutamiento
    {
        public const string Nuevo             = "NUEVO";
        public const string AprobacionGg      = "APROBACION_GG";
        public const string ValidacionGth     = "VALIDACION_GTH";
        public const string Publicacion       = "PUBLICACION";
        public const string LongList          = "LONG_LIST";
        public const string LongListEnviada   = "LONG_LIST_ENVIADA";
        public const string LongListAprobada  = "LONG_LIST_APROBADA";
        public const string Entrevistas       = "ENTREVISTAS";
        public const string Evaluacion        = "EVALUACION";
        public const string SeleccionJefatura = "SELECCION_JEFATURA";
        public const string OfertaCierre      = "OFERTA_CIERRE";

        /// <summary>
        /// Estado final: el solicitante aprobó al finalista, el proceso de reclutamiento termina y
        /// el seleccionado pasa al proceso de onboarding (funcionalidad aparte).
        /// </summary>
        public const string Cerrado           = "CERRADO";

        /// <summary>
        /// Estado final: Gerencia General no aprobó la vacante, así que nunca llega a GTH. Está
        /// sembrado con <c>active = false</c> a propósito: es un estado del requerimiento pero no
        /// una fase del pipeline, y la línea de tiempo del seguimiento solo lista las fases activas.
        /// </summary>
        public const string RechazadoGg       = "RECHAZADO_GG";

        /// <summary>
        /// Estados en los que el requerimiento todavía NO le pertenece a GTH: sigue en el paso de
        /// Gerencia General, o el GG lo rechazó. La bandeja de GTH los excluye (solo ve lo aprobado).
        /// </summary>
        public static readonly HashSet<string> FueraDeGth = new()
        {
            AprobacionGg,
            RechazadoGg,
        };

        /// <summary>
        /// Fases en las que el siguiente paso le toca a GTH (tarjeta "En revisión · GTH evaluando"
        /// del panel del solicitante). Deja fuera a propósito:
        /// <list type="bullet">
        ///   <item><description><see cref="Nuevo"/>: es el primer paso de GTH y ya se cuenta en
        ///   "Pendientes · Sin respuesta"; contarlo en ambas tarjetas duplicaría el mismo proceso.</description></item>
        ///   <item><description><see cref="AprobacionGg"/>: el paso es de Gerencia General, no de GTH.</description></item>
        ///   <item><description><see cref="LongListEnviada"/> y <see cref="SeleccionJefatura"/>:
        ///   la pelota está del lado del solicitante.</description></item>
        ///   <item><description><see cref="Cerrado"/>: el proceso ya terminó.</description></item>
        /// </list>
        /// </summary>
        public static readonly HashSet<string> FasesGth = new()
        {
            ValidacionGth,
            Publicacion,
            LongList,
            LongListAprobada,
            Entrevistas,
            Evaluacion,
            OfertaCierre,
        };
    }

    /// <summary>
    /// Etapas del embudo "Pipeline de reclutamiento" (vista de GTH), en el orden en que se muestran.
    /// El catálogo tiene 12 fases y el embudo las agrupa en 7 etapas legibles; cada fase pertenece a
    /// exactamente una etapa, de modo que la suma de las etapas es siempre el total de requerimientos
    /// vigentes y ninguno se pierde del embudo.
    ///
    /// El orden sigue el <c>orden</c> del catálogo (por eso Entrevistas va antes que Evaluación),
    /// que es el mismo que ve el solicitante en el seguimiento vertical del requerimiento.
    /// <c>SELECCION_JEFATURA</c> es una fase heredada que el flujo implementado no usa (su función
    /// la cubren LONG_LIST_ENVIADA/LONG_LIST_APROBADA); se agrupa en Revisión por lo que describe.
    /// </summary>
    internal sealed record EtapaPipeline(string Codigo, string Nombre, string[] Fases)
    {
        public static readonly EtapaPipeline[] Todas =
        {
            new("SOLICITUD",   "Solicitud",   new[] { EstadoReclutamiento.Nuevo,
                                                      EstadoReclutamiento.AprobacionGg,
                                                      EstadoReclutamiento.ValidacionGth }),
            new("PUBLICADO",   "Publicado",   new[] { EstadoReclutamiento.Publicacion }),
            new("REVISION",    "Revisión",    new[] { EstadoReclutamiento.LongList,
                                                      EstadoReclutamiento.LongListEnviada,
                                                      EstadoReclutamiento.LongListAprobada,
                                                      EstadoReclutamiento.SeleccionJefatura }),
            new("ENTREVISTAS", "Entrevistas", new[] { EstadoReclutamiento.Entrevistas }),
            new("EVALUACION",  "Evaluación",  new[] { EstadoReclutamiento.Evaluacion }),
            new("OFERTA",      "Oferta",      new[] { EstadoReclutamiento.OfertaCierre }),
            new("CIERRE",      "Cierre",      new[] { EstadoReclutamiento.Cerrado }),
        };

        /// <summary>Código de la etapa terminal: lo ya cerrado no cuenta como proceso activo.</summary>
        public const string CodigoCierre = "CIERRE";

        /// <summary>Código de la etapa en la que la vacante recién se publica (inicio de "vacantes abiertas").</summary>
        public const string CodigoPublicado = "PUBLICADO";
    }

    /// <summary>Códigos estables de prioridad de reclutamiento (espejo de gth_prioridad.codigo).</summary>
    internal static class PrioridadReclutamiento
    {
        public const string Media = "MEDIA";
    }

    /// <summary>Códigos estables del estado de revisión de un candidato (espejo de gth_candidato_estado.codigo).</summary>
    internal static class EstadoCandidato
    {
        public const string Pendiente = "PENDIENTE";
        public const string Aprobado  = "APROBADO";
        public const string Rechazado = "RECHAZADO";
    }

    /// <summary>
    /// Códigos estables del resultado del candidato en el proceso (espejo de
    /// gth_candidato_resultado.codigo). Es una sola línea de tiempo: PENDIENTE → PASO (finalista
    /// enviado al solicitante) → SELECCIONADO / RECHAZADO por el solicitante; NO_PASO es la salida
    /// que decide GTH tras la entrevista.
    /// </summary>
    internal static class ResultadoCandidato
    {
        public const string Pendiente    = "PENDIENTE";
        public const string Paso         = "PASO";
        public const string NoPaso       = "NO_PASO";
        public const string Seleccionado = "SELECCIONADO";
        public const string Rechazado    = "RECHAZADO";
    }
}
