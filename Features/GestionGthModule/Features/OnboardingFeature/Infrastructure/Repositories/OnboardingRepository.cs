using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Repositories
{
    public class OnboardingRepository : IOnboardingRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public OnboardingRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Los timestamps se guardan en UTC y se sirven al frontend en hora de Perú.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        /// <summary>
        /// Fila cruda de un onboarding con todo lo que se lee de reclutamiento. Es una clase con
        /// nombre (y no un tipo anónimo) porque la comparte la bandeja con el alta de un onboarding
        /// nuevo, que devuelve la misma fila para insertarla en la tabla sin recargar la pantalla.
        /// </summary>
        private sealed class OnboardingRawRow
        {
            public GthOnboarding Ob { get; set; } = null!;
            public string Codigo { get; set; } = string.Empty;
            public string CandidatoNombre { get; set; } = string.Empty;
            public string? PersonNombre { get; set; }
            public string? PersonEmail { get; set; }
            public string? Puesto { get; set; }
            public string? Area { get; set; }
            public string? Empresa { get; set; }
            public string? ProyectoObra { get; set; }
            public string? JefeDirecto { get; set; }

            /// <summary>Coordinador administrativo del proyecto destino: el que recibe el aviso de obra.</summary>
            public string? CoordAdminNombre { get; set; }
            public string? CoordAdminEmail { get; set; }

            public string FaseCodigo { get; set; } = string.Empty;
            public string FaseNombre { get; set; } = string.Empty;
            public int FaseOrden { get; set; }
            public string EstadoCodigo { get; set; } = string.Empty;
            public string EstadoNombre { get; set; } = string.Empty;

            // ── Formulario de bienvenida ──────────────────────────────────────
            /// <summary>Cuándo salió el correo de bienvenida (UTC). null = todavía no.</summary>
            public DateTimeOffset? BienvenidaEnviada { get; set; }
            /// <summary>Buzón al que salió, que es el de su ficha maestra al momento del envío.</summary>
            public string? BienvenidaEmail { get; set; }
            /// <summary>Cuándo el colaborador envió su formulario (UTC). null = pendiente.</summary>
            public DateTimeOffset? FormularioCompletado { get; set; }
            /// <summary>Fecha límite que se le comunicó para completarlo.</summary>
            public DateOnly? FormularioFechaLimite { get; set; }
        }

        /// <summary>
        /// Onboardings vigentes con su vacante, su ficha maestra y sus catálogos, en una sola
        /// consulta. <paramref name="onboardingId"/> la acota a uno solo (alta de un ingreso nuevo).
        /// </summary>
        private static IQueryable<OnboardingRawRow> QueryOnboardings(AppDbContext ctx, int? onboardingId = null) =>
            from ob in ctx.GthOnboarding
            where ob.State && (onboardingId == null || ob.GthOnboardingId == onboardingId)
            join c in ctx.GthCandidato on ob.GthCandidatoId equals c.GthCandidatoId
            join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
            join s in ctx.GthSolicitud on r.GthSolicitudId equals s.GthSolicitudId
            join p in ctx.Puesto on r.PuestoId equals p.PuestoId
            join pr in ctx.Project on r.ProjectId equals pr.ProjectId
            join fa in ctx.GthOnboardingFase on ob.GthOnboardingFaseId equals fa.GthOnboardingFaseId
            join es in ctx.GthOnboardingEstado on ob.GthOnboardingEstadoId equals es.GthOnboardingEstadoId
                // Razón social, ficha maestra y jefe directo son todos opcionales.
            join co in ctx.Contributor on r.ContributorId equals (int?)co.ContributorId into coJoin
            from co in coJoin.DefaultIfEmpty()
            join pe in ctx.Person on ob.PersonId equals (int?)pe.PersonId into peJoin
            from pe in peJoin.DefaultIfEmpty()
            join w in ctx.Worker on s.SolicitanteWorkerId equals (int?)w.Id into wJoin
            from w in wJoin.DefaultIfEmpty()
                // Formulario de bienvenida: existe recién cuando GTH manda ese correo.
            join fo in ctx.GthOnboardingFormulario.Where(x => x.State)
                on ob.GthOnboardingId equals fo.GthOnboardingId into foJoin
            from fo in foJoin.DefaultIfEmpty()
            select new OnboardingRawRow
            {
                Ob              = ob,
                Codigo          = r.Codigo,
                CandidatoNombre = c.Nombre,
                PersonNombre    = pe == null ? null : pe.FullName,
                PersonEmail     = pe == null ? null : pe.Email,
                Puesto          = p.Nombre,
                Area            = s.AreaNombre,
                Empresa         = co == null ? null : co.ContributorName,
                ProyectoObra    = pr.ProjectDescription,
                JefeDirecto     = w == null || w.Person == null ? null : w.Person.FullName,
                // El destinatario del aviso de obra sale del proyecto destino, no de la vacante: es
                // quien administra esa obra hoy. Se trae en la misma consulta porque la pantalla
                // tiene que poder decir, fila por fila, si ese aviso aplica y a quién le va.
                CoordAdminNombre = pr.CoordAdmin == null || pr.CoordAdmin.Person == null
                                    ? null : pr.CoordAdmin.Person.FullName,
                CoordAdminEmail  = pr.CoordAdmin == null ? null : pr.CoordAdmin.EmailCorporativo,
                FaseCodigo      = fa.Codigo,
                FaseNombre      = fa.Nombre,
                FaseOrden       = fa.Orden,
                EstadoCodigo    = es.Codigo,
                EstadoNombre    = es.Nombre,

                BienvenidaEnviada     = fo == null ? null : fo.EnviadoDateTime,
                BienvenidaEmail       = fo == null ? null : fo.CorreoEnvio,
                FormularioCompletado  = fo == null ? null : fo.CompletadoDateTime,
                FormularioFechaLimite = fo == null ? null : fo.FechaLimite,
            };

        /// <summary>
        /// Una actividad del catálogo con lo justo para resolver si un onboarding ya la cumplió.
        /// </summary>
        private sealed record ActividadCatalogo(int FaseOrden, string Codigo, bool Automatica);

        /// <summary>
        /// Qué actividades del checklist tiene cumplidas un onboarding. Hoy NO se guarda actividad
        /// por actividad: se deduce de lo que sí está persistido en su fila, que es la única fuente
        /// de verdad. Vive acá —y no en la pantalla— para que el % de la tabla y los checks del
        /// detalle no puedan discrepar:
        ///
        ///   • Actividades de fases anteriores a la actual → hechas (para pasar de fase hubo que
        ///     cumplirlas).
        ///   • Actividades automáticas → hechas (las cumple el sistema solo, sin acción de GTH).
        ///   • AVISO_OBRA → hecha cuando el correo ya salió, y también cuando NO aplica: a Oficina
        ///     Central no se le avisa de nada y un proyecto sin coordinador administrativo no tiene
        ///     a quién avisarle. Dejarla pendiente en esos casos congelaría el avance en un paso
        ///     que nadie puede dar.
        ///   • ENVIAR_BIENVENIDA → hecha cuando ese correo salió (o sea, cuando el colaborador ya
        ///     tiene su formulario abierto y su enlace).
        ///   • RECIBIR_FORMULARIO → hecha cuando el colaborador envió ese mismo formulario. Es de
        ///     la fase siguiente, pero se cumple sola y desde acá: el que la cumple es él, no GTH.
        ///
        /// A medida que cada fase se implemente, su regla entra acá.
        /// </summary>
        private static List<string> ActividadesHechas(
            IReadOnlyList<ActividadCatalogo> checklist, OnboardingRawRow x)
        {
            var avisoObraHecha = x.Ob.AvisoObraEnviadoDateTime != null || !AplicaAvisoObra(x);

            return checklist
                .Where(a => a.FaseOrden < x.FaseOrden
                         || a.Automatica
                         || (a.Codigo == ActividadOnboarding.AvisoObra && avisoObraHecha)
                         || (a.Codigo == ActividadOnboarding.EnviarBienvenida && x.BienvenidaEnviada != null)
                         || (a.Codigo == ActividadOnboarding.RecibirFormulario && x.FormularioCompletado != null))
                .Select(a => a.Codigo)
                .ToList();
        }

        /// <summary>
        /// ¿Le corresponde a este ingreso el aviso al responsable de obra? Solo a los que entran a
        /// una obra o sede con coordinador administrativo cargado.
        ///
        /// Oficina Central se descarta por nombre porque no hay nada más con qué distinguirla: la
        /// tabla <c>project</c> no tiene un tipo ni una bandera, es una fila más del catálogo. En la
        /// práctica tampoco tiene coordinador administrativo, así que la segunda condición ya la
        /// dejaría fuera; el nombre está para que la regla sea explícita y no dependa de que ese
        /// campo siga vacío.
        /// </summary>
        private static bool AplicaAvisoObra(OnboardingRawRow x) =>
            !EsOficinaCentral(x.ProyectoObra) && !string.IsNullOrWhiteSpace(x.CoordAdminEmail);

        /// <summary>Por qué no aplica, para explicarlo en la pantalla. null = sí aplica.</summary>
        private static string? MotivoNoAplicaAvisoObra(OnboardingRawRow x)
        {
            if (EsOficinaCentral(x.ProyectoObra))
                return "El ingreso es a Oficina Central: no hay obra a la que avisarle.";
            if (string.IsNullOrWhiteSpace(x.CoordAdminEmail))
                return "El proyecto no tiene coordinador administrativo con correo en Configuración → Proyectos.";
            return null;
        }

        private static bool EsOficinaCentral(string? proyecto) =>
            string.Equals(proyecto?.Trim(), "OFICINA CENTRAL", StringComparison.OrdinalIgnoreCase);

        /// <summary>Catálogo de actividades vigentes, ordenado por fase (para resolver el avance).</summary>
        private static Task<List<ActividadCatalogo>> LeerChecklist(AppDbContext ctx) =>
            (from a in ctx.GthOnboardingActividad
             where a.State && a.Active
             join f in ctx.GthOnboardingFase on a.GthOnboardingFaseId equals f.GthOnboardingFaseId
             where f.State && f.Active
             orderby f.Orden, a.Orden
             select new ActividadCatalogo(f.Orden, a.Codigo, a.Automatica))
            .ToListAsync();

        /// <summary>
        /// Proyecta la fila cruda al DTO de la tabla. <paramref name="checklist"/> es el catálogo de
        /// actividades vigentes: el % de avance son las actividades cumplidas sobre ese total.
        /// </summary>
        private static OnboardingListItemDto MapItem(OnboardingRawRow x, IReadOnlyList<ActividadCatalogo> checklist)
        {
            var hechas = ActividadesHechas(checklist, x);
            return MapItem(x, hechas, checklist.Count);
        }

        private static OnboardingListItemDto MapItem(
            OnboardingRawRow x, List<string> actividadesHechas, int totalActividades) => new()
        {
            OnboardingId = x.Ob.GthOnboardingId,
            CandidatoId  = x.Ob.GthCandidatoId,
            PersonId     = x.Ob.PersonId,
            Codigo       = x.Codigo,
            // El nombre de la base maestra manda sobre el que registró GTH al cargar el CV: es el
            // que el propio postulante declaró y GTH validó al aprobar su formulario.
            Nombre       = string.IsNullOrWhiteSpace(x.PersonNombre) ? x.CandidatoNombre : x.PersonNombre!,
            Puesto       = x.Puesto,
            Area         = x.Area,
            Empresa      = x.Empresa,
            ProyectoObra = x.ProyectoObra,
            FechaIngreso = x.Ob.FechaIngreso,
            JefeDirecto  = x.JefeDirecto,
            Correo       = x.PersonEmail,
            FaseCodigo   = x.FaseCodigo,
            FaseNombre   = x.FaseNombre,
            FaseOrden    = x.FaseOrden,
            EstadoCodigo = x.EstadoCodigo,
            EstadoNombre = x.EstadoNombre,
            ActividadesHechas = actividadesHechas,
            AvancePorcentaje = totalActividades <= 0
                ? 0
                : (int)Math.Round(actividadesHechas.Count * 100d / totalActividades, MidpointRounding.AwayFromZero),
            FileDigitalCarpeta = x.Ob.FileDigitalRuta,
            Observacion        = x.Ob.Observacion,
            IniciadoEn         = x.Ob.CreatedDateTime.ToOffset(PeruOffset).DateTime,

            AvisoObraAplica          = AplicaAvisoObra(x),
            AvisoObraMotivoNoAplica  = MotivoNoAplicaAvisoObra(x),
            AvisoObraDestinatario    = x.CoordAdminNombre,
            // El buzón al que salió manda sobre el de hoy: el coordinador administrativo del
            // proyecto puede haber cambiado desde entonces.
            AvisoObraEmail           = x.Ob.AvisoObraEmail ?? x.CoordAdminEmail,
            AvisoObraEnviadoEn       = x.Ob.AvisoObraEnviadoDateTime?.ToOffset(PeruOffset).DateTime,

            BienvenidaEnviadaEn    = x.BienvenidaEnviada?.ToOffset(PeruOffset).DateTime,
            // El buzón al que salió manda sobre el de hoy; si nunca salió, se muestra el actual
            // para que la pantalla pueda decir a quién le va a llegar.
            BienvenidaEmail        = x.BienvenidaEmail ?? x.PersonEmail,
            FormularioFechaLimite  = x.FormularioFechaLimite,
            FormularioCompletadoEn = x.FormularioCompletado?.ToOffset(PeruOffset).DateTime,
        };

        public async Task<BandejaOnboardingDto> GetBandeja()
        {
            using var ctx = _factory.CreateDbContext();

            // Antes de leer nada: los que terminaron reclutamiento entran solos a la lista. Ver
            // MaterializarPendientes.
            await MaterializarPendientes(ctx);

            var fases = await ctx.GthOnboardingFase
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.Orden)
                .Select(f => new FaseOnboardingDto
                {
                    FaseId      = f.GthOnboardingFaseId,
                    Codigo      = f.Codigo,
                    Nombre      = f.Nombre,
                    Descripcion = f.Descripcion,
                    Orden       = f.Orden,
                    // El checklist de cada fase va anidado acá para que el detalle no tenga que
                    // pedirlo por separado: es el mismo catálogo para todos los colaboradores.
                    Actividades = ctx.GthOnboardingActividad
                        .Where(a => a.State && a.Active && a.GthOnboardingFaseId == f.GthOnboardingFaseId)
                        .OrderBy(a => a.Orden)
                        .Select(a => new ActividadOnboardingDto
                        {
                            ActividadId = a.GthOnboardingActividadId,
                            Codigo      = a.Codigo,
                            Nombre      = a.Nombre,
                            Descripcion = a.Descripcion,
                            Orden       = a.Orden,
                            Automatica  = a.Automatica,
                        })
                        .ToList(),
                })
                .ToListAsync();

            var filas = await QueryOnboardings(ctx)
                .OrderByDescending(x => x.Ob.CreatedDateTime)
                .ThenByDescending(x => x.Ob.GthOnboardingId)
                .ToListAsync();

            // El catálogo de actividades se arma de las fases que ya se leyeron: es el mismo dato,
            // así que no vale un roundtrip más.
            var checklist = fases
                .SelectMany(f => f.Actividades.Select(a => new ActividadCatalogo(f.Orden, a.Codigo, a.Automatica)))
                .ToList();

            var colaboradores = filas.Select(x => MapItem(x, checklist)).ToList();

            // Conteo del embudo: cuántos colaboradores hay parados en cada fase.
            foreach (var f in fases)
                f.Total = colaboradores.Count(c => c.FaseCodigo == f.Codigo);

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5).Date);
            var desde7Dias = DateTimeOffset.UtcNow.AddDays(-7);

            return new BandejaOnboardingDto
            {
                Fases           = fases,
                Colaboradores   = colaboradores,
                Resumen = new ResumenOnboardingDto
                {
                    IngresosDelMes = colaboradores.Count(c =>
                        c.FechaIngreso.HasValue
                        && c.FechaIngreso.Value.Year == hoy.Year
                        && c.FechaIngreso.Value.Month == hoy.Month),
                    EnProceso  = colaboradores.Count(c => c.EstadoCodigo != EstadoOnboarding.Completo),
                    Completos  = colaboradores.Count(c => c.EstadoCodigo == EstadoOnboarding.Completo),
                    ColaboradoresNuevos = filas.Count(x => x.Ob.CreatedDateTime >= desde7Dias),
                },
            };
        }

        /// <summary>
        /// Lo mínimo para abrirle el onboarding a un candidato que ya terminó reclutamiento. Todo
        /// sale de su carta oferta: la ficha maestra, la fecha de ingreso pactada y el file digital
        /// (la carpeta que esa carta creó, que el onboarding sigue llenando).
        /// </summary>
        private sealed class CandidatoPorIngresar
        {
            public int CandidatoId { get; set; }
            public int? PersonId { get; set; }
            public DateOnly? FechaIngreso { get; set; }
            public string? FileDigitalDriveId { get; set; }
            public string? FileDigitalItemId { get; set; }
            public string? FileDigitalRuta { get; set; }
        }

        /// <summary>
        /// Le abre el onboarding a todo el que ya terminó reclutamiento y todavía no lo tiene:
        /// resultado SELECCIONADO sobre un requerimiento CERRADO —o sea, con su carta oferta firmada
        /// y aprobada—.
        ///
        /// Entrar a onboarding dejó de ser una decisión de GTH: si el proceso de reclutamiento
        /// cerró, esa persona entra, y el alta manual solo agregaba un formulario que no preguntaba
        /// nada que no estuviera ya decidido. Se materializa acá, al abrir la bandeja, y no en el
        /// acto que cierra el requerimiento, porque así también entran los que cerraron antes de
        /// este cambio: es autocorrectivo, no hace falta un backfill.
        ///
        /// El join con la carta oferta es interno a propósito: el requerimiento no se puede cerrar
        /// sin ella, así que un candidato sin carta no debería existir — y si existiera, significa
        /// que algo lo cerró por otra vía y no queremos abrirle un onboarding sin expediente.
        /// </summary>
        private static async Task MaterializarPendientes(AppDbContext ctx)
        {
            var pendientes = await (
                from c in ctx.GthCandidato
                where c.State
                    // Un candidato solo puede tener un onboarding abierto.
                    && !ctx.GthOnboarding.Any(o => o.GthCandidatoId == c.GthCandidatoId && o.State)
                join ev in ctx.GthCandidatoEvaluacion.Where(x => x.State)
                    on c.GthCandidatoId equals ev.GthCandidatoId
                join res in ctx.GthCandidatoResultado on ev.GthCandidatoResultadoId equals res.GthCandidatoResultadoId
                where res.Codigo == ResultadoCandidatoSeleccionado
                join r in ctx.GthRequerimiento.Where(x => x.State)
                    on c.GthRequerimientoId equals r.GthRequerimientoId
                join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
                where e.Codigo == EstadoRequerimientoCerrado
                join ca in ctx.GthCartaOferta.Where(x => x.State)
                    on c.GthCandidatoId equals ca.GthCandidatoId
                select new CandidatoPorIngresar
                {
                    CandidatoId        = c.GthCandidatoId,
                    PersonId           = ca.PersonId,
                    FechaIngreso       = ca.FechaIngreso,
                    FileDigitalDriveId = ca.FileDigitalDriveId,
                    FileDigitalItemId  = ca.FileDigitalItemId,
                    FileDigitalRuta    = ca.FileDigitalRuta,
                }).ToListAsync();

            if (pendientes.Count == 0) return;

            var fase = await ctx.GthOnboardingFase
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.Orden)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No hay ninguna fase de onboarding configurada.", 500);

            var estado = await ctx.GthOnboardingEstado
                .FirstOrDefaultAsync(e => e.Codigo == EstadoOnboarding.EnProceso && e.State)
                ?? throw new AbrilException(
                    $"No está configurado el estado {EstadoOnboarding.EnProceso} del onboarding.", 500);

            var now = DateTimeOffset.UtcNow;
            foreach (var p in pendientes)
            {
                ctx.GthOnboarding.Add(new GthOnboarding
                {
                    GthCandidatoId        = p.CandidatoId,
                    PersonId              = p.PersonId,
                    GthOnboardingFaseId   = fase.GthOnboardingFaseId,
                    GthOnboardingEstadoId = estado.GthOnboardingEstadoId,
                    // La pactada en la carta oferta: es la única fecha de ingreso que existe, y se
                    // decide al generarla. Acá no se vuelve a preguntar.
                    FechaIngreso          = p.FechaIngreso,
                    FileDigitalDriveId    = p.FileDigitalDriveId,
                    FileDigitalItemId     = p.FileDigitalItemId,
                    FileDigitalRuta       = p.FileDigitalRuta,
                    CreatedDateTime       = now,
                });
            }

            try
            {
                await ctx.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Dos usuarios abriendo la pantalla a la vez: el índice único por candidato
                // (ix_gth_onboarding_candidato) deja pasar uno solo. El otro no tiene nada que
                // hacer — las filas ya están— así que se sigue leyendo la bandeja como si nada.
                ctx.ChangeTracker.Clear();
            }
        }

        // ── Avance de fase ─────────────────────────────────────────────────────

        /// <summary>
        /// Mueve el onboarding a la fase siguiente del catálogo. Cada fase decide qué la habilita a
        /// avanzar; desde que la carta oferta se fue a Reclutamiento no queda ninguna implementada,
        /// así que se corta con un mensaje explícito en vez de avanzar en falso. A medida que cada
        /// fase se codifique, su condición entra acá.
        /// </summary>
        public async Task<OnboardingListItemDto> Avanzar(int onboardingId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var ob = await ctx.GthOnboarding.FirstOrDefaultAsync(o => o.GthOnboardingId == onboardingId && o.State)
                ?? throw new AbrilException("El onboarding indicado no existe o fue dado de baja.", 404);

            var actual = await ctx.GthOnboardingFase
                .FirstOrDefaultAsync(f => f.GthOnboardingFaseId == ob.GthOnboardingFaseId)
                ?? throw new AbrilException("La fase actual del onboarding ya no está vigente en el catálogo.", 409);

            throw new AbrilException(
                $"La fase «{actual.Nombre}» todavía no está habilitada para avanzar desde el sistema.", 409);
        }

        // ── Aviso al responsable de obra ───────────────────────────────────────

        public async Task<AvisoObraContextoDto> GetAvisoObraContexto(int onboardingId)
        {
            using var ctx = _factory.CreateDbContext();

            var x = await QueryOnboardings(ctx, onboardingId).FirstOrDefaultAsync()
                ?? throw new AbrilException("El onboarding indicado no existe o fue dado de baja.", 404);

            return new AvisoObraContextoDto
            {
                OnboardingId     = x.Ob.GthOnboardingId,
                Codigo           = x.Codigo,
                Nombre           = string.IsNullOrWhiteSpace(x.PersonNombre) ? x.CandidatoNombre : x.PersonNombre!,
                Puesto           = x.Puesto,
                Area             = x.Area,
                Empresa          = x.Empresa,
                ProyectoObra     = x.ProyectoObra,
                JefeDirecto      = x.JefeDirecto,
                FechaIngreso     = x.Ob.FechaIngreso,
                CoordAdminNombre = x.CoordAdminNombre,
                CoordAdminEmail  = x.CoordAdminEmail,
                Aplica           = AplicaAvisoObra(x),
                MotivoNoAplica   = MotivoNoAplicaAvisoObra(x),
                EnviadoEn        = x.Ob.AvisoObraEnviadoDateTime,
            };
        }

        public async Task<OnboardingListItemDto> MarcarAvisoObraEnviado(
            int onboardingId, string email, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var ob = await ctx.GthOnboarding
                .FirstOrDefaultAsync(o => o.GthOnboardingId == onboardingId && o.State)
                ?? throw new AbrilException("El onboarding indicado no existe o fue dado de baja.", 404);

            var now = DateTimeOffset.UtcNow;
            ob.AvisoObraEnviadoDateTime = now;
            ob.AvisoObraEmail           = email;
            ob.AvisoObraUserId          = userId;
            ob.UpdatedDateTime          = now;
            ob.UpdatedUserId            = userId;

            await ctx.SaveChangesAsync();

            return await LeerItem(ctx, onboardingId);
        }

        public async Task<OnboardingListItemDto> GetItem(int onboardingId)
        {
            using var ctx = _factory.CreateDbContext();
            return await LeerItem(ctx, onboardingId);
        }

        /// <summary>Relee la fila ya escrita para devolverla al frontend con sus catálogos resueltos.</summary>
        private static async Task<OnboardingListItemDto> LeerItem(AppDbContext ctx, int onboardingId)
        {
            var checklist = await LeerChecklist(ctx);
            var fila = await QueryOnboardings(ctx, onboardingId).FirstOrDefaultAsync()
                ?? throw new AbrilException("No se pudo releer el onboarding actualizado.", 500);
            return MapItem(fila, checklist);
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Espejo de <c>gth_candidato_resultado.codigo</c> para el seleccionado (dueño del puesto).</summary>
        private const string ResultadoCandidatoSeleccionado = "SELECCIONADO";

        /// <summary>Espejo de <c>gth_estado_requerimiento.codigo</c> para el proceso ya cerrado.</summary>
        private const string EstadoRequerimientoCerrado = "CERRADO";
    }

    /// <summary>
    /// Códigos estables de las fases del onboarding (espejo de <c>gth_onboarding_fase.codigo</c>).
    ///
    /// Se fueron dos: CARTA_OFERTA_FIRMADA, cuando la carta pasó a ser el último paso de
    /// Reclutamiento, y FILE_DIGITAL, cuyas tres actividades ya no tenían nada que hacer acá —
    /// guardar la carta en SharePoint y avisarle a TI ocurren antes, en Reclutamiento, y el aviso
    /// al responsable de obra se mudó a CORREO_BIENVENIDA, que es donde GTH lo manda.
    /// </summary>
    public static class FaseOnboarding
    {
        public const string CorreoBienvenida = "CORREO_BIENVENIDA";
        public const string FormularioWeb    = "FORMULARIO_WEB";
        public const string Preinicio        = "PREINICIO";
        public const string CierreOnboarding = "CIERRE_ONBOARDING";
        public const string BaseMaestra      = "BASE_MAESTRA";
    }

    /// <summary>
    /// Códigos estables de las actividades con lógica propia (espejo de
    /// <c>gth_onboarding_actividad.codigo</c>). El resto del checklist se dibuja del catálogo y no
    /// necesita constante.
    /// </summary>
    public static class ActividadOnboarding
    {
        /// <summary>El aviso al coordinador administrativo de la obra donde entra el colaborador.</summary>
        public const string AvisoObra = "AVISO_OBRA";

        /// <summary>
        /// El correo de bienvenida: le abre al colaborador su formulario «Nuevos Talentos» y le
        /// manda el enlace, la documentación que tiene que enviar y la fecha límite.
        /// </summary>
        public const string EnviarBienvenida = "ENVIAR_BIENVENIDA";

        /// <summary>
        /// La vuelta del anterior: el colaborador envió ese formulario. Vive en la fase
        /// «Formulario web», pero se cumple sola —el que la cumple es él— así que se resuelve en la
        /// misma pasada.
        /// </summary>
        public const string RecibirFormulario = "RECIBIR_FORMULARIO";
    }

    /// <summary>Códigos estables del estado del onboarding (espejo de <c>gth_onboarding_estado.codigo</c>).</summary>
    public static class EstadoOnboarding
    {
        public const string EnProceso = "EN_PROCESO";
        public const string Completo  = "COMPLETO";
    }
}
