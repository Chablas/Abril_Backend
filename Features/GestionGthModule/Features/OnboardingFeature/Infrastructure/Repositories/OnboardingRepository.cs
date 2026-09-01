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
            public string FaseCodigo { get; set; } = string.Empty;
            public string FaseNombre { get; set; } = string.Empty;
            public int FaseOrden { get; set; }
            public string EstadoCodigo { get; set; } = string.Empty;
            public string EstadoNombre { get; set; } = string.Empty;
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
                JefeDirecto     = w == null ? null : (w.Person != null ? w.Person.FullName : w.ApellidoNombre),
                FaseCodigo      = fa.Codigo,
                FaseNombre      = fa.Nombre,
                FaseOrden       = fa.Orden,
                EstadoCodigo    = es.Codigo,
                EstadoNombre    = es.Nombre,
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
        ///   • Actividades automáticas → hechas (los avisos preventivos a TI y al responsable de
        ///     obra salen al registrar la solicitud, antes de que el colaborador llegue a onboarding).
        ///
        /// A medida que cada fase se implemente, su regla entra acá.
        /// </summary>
        private static List<string> ActividadesHechas(
            IReadOnlyList<ActividadCatalogo> checklist, int faseOrden) =>
            checklist
                .Where(a => a.FaseOrden < faseOrden || a.Automatica)
                .Select(a => a.Codigo)
                .ToList();

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
            var hechas = ActividadesHechas(checklist, x.FaseOrden);
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
        };

        public async Task<BandejaOnboardingDto> GetBandeja()
        {
            using var ctx = _factory.CreateDbContext();

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

            var candidatos = await QueryCandidatosAptos(ctx);

            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5).Date);
            var desde7Dias = DateTimeOffset.UtcNow.AddDays(-7);

            return new BandejaOnboardingDto
            {
                Fases           = fases,
                Colaboradores   = colaboradores,
                CandidatosAptos = candidatos,
                Resumen = new ResumenOnboardingDto
                {
                    IngresosDelMes = colaboradores.Count(c =>
                        c.FechaIngreso.HasValue
                        && c.FechaIngreso.Value.Year == hoy.Year
                        && c.FechaIngreso.Value.Month == hoy.Month),
                    EnProceso  = colaboradores.Count(c => c.EstadoCodigo != EstadoOnboarding.Completo),
                    Completos  = colaboradores.Count(c => c.EstadoCodigo == EstadoOnboarding.Completo),
                    ColaboradoresNuevos = filas.Count(x => x.Ob.CreatedDateTime >= desde7Dias),
                    CandidatosPorIngresar = candidatos.Count,
                },
            };
        }

        /// <summary>
        /// Candidatos que ya terminaron reclutamiento y pueden pasar a onboarding: resultado
        /// SELECCIONADO sobre un requerimiento que quedó CERRADO —o sea, con su carta oferta firmada
        /// y aprobada— y que todavía no tienen un onboarding abierto.
        ///
        /// La ficha maestra, el correo, la fecha de ingreso y el file digital salen todos de esa
        /// carta oferta: no se vuelven a resolver acá. El requerimiento no se puede cerrar sin ella,
        /// así que el join es interno a propósito — un candidato sin carta no es apto, y si apareciera
        /// significaría que algo cerró el proceso por otra vía.
        /// </summary>
        private static async Task<List<CandidatoAptoDto>> QueryCandidatosAptos(AppDbContext ctx) =>
            await (
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
                join s in ctx.GthSolicitud on r.GthSolicitudId equals s.GthSolicitudId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                    // Razón social, ficha maestra y jefe directo son todos opcionales.
                join co in ctx.Contributor on r.ContributorId equals (int?)co.ContributorId into coJoin
                from co in coJoin.DefaultIfEmpty()
                join pe in ctx.Person on ca.PersonId equals pe.PersonId into peJoin
                from pe in peJoin.DefaultIfEmpty()
                join w in ctx.Worker on s.SolicitanteWorkerId equals (int?)w.Id into wJoin
                from w in wJoin.DefaultIfEmpty()
                orderby r.Codigo descending
                select new CandidatoAptoDto
                {
                    CandidatoId     = c.GthCandidatoId,
                    RequerimientoId = r.GthRequerimientoId,
                    PersonId        = ca.PersonId,
                    // El nombre de la base maestra manda sobre el que registró GTH al cargar el CV.
                    Nombre          = pe != null && pe.FullName != null ? pe.FullName : c.Nombre,
                    Codigo          = r.Codigo,
                    Puesto          = p.Nombre,
                    Area            = s.AreaNombre,
                    Empresa         = co == null ? null : co.ContributorName,
                    ProyectoObra    = pr.ProjectDescription,
                    Correo          = pe == null ? null : pe.Email,
                    JefeDirecto     = w == null ? null : (w.Person != null ? w.Person.FullName : w.ApellidoNombre),
                    FechaIngreso       = ca.FechaIngreso,
                    FileDigitalCarpeta = ca.FileDigitalRuta,
                }).ToListAsync();

        public async Task<OnboardingListItemDto> Crear(OnboardingCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Se reusa la misma consulta de aptos: así "quién puede entrar a onboarding" está escrito
            // una sola vez y el desplegable y esta validación no pueden discrepar.
            var apto = (await QueryCandidatosAptos(ctx)).FirstOrDefault(x => x.CandidatoId == dto.CandidatoId)
                ?? throw new AbrilException(
                    "Este candidato no puede pasar a onboarding: su proceso de reclutamiento tiene que estar cerrado (carta oferta firmada y aprobada) y no puede tener otro onboarding abierto.",
                    409);

            // El file digital lo abrió su carta oferta; el onboarding sigue llenando esa misma
            // carpeta, así que se copian sus identificadores en vez de volver a resolverlos.
            var fileDigital = await (
                from ca in ctx.GthCartaOferta
                where ca.State && ca.GthCandidatoId == dto.CandidatoId
                select new { ca.FileDigitalDriveId, ca.FileDigitalItemId, ca.FileDigitalRuta })
                .FirstOrDefaultAsync();

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
            var ob = new GthOnboarding
            {
                GthCandidatoId        = apto.CandidatoId,
                PersonId              = apto.PersonId,
                GthOnboardingFaseId   = fase.GthOnboardingFaseId,
                GthOnboardingEstadoId = estado.GthOnboardingEstadoId,
                // La fecha de la carta oferta es la pactada; GTH la puede ajustar en el modal.
                FechaIngreso          = dto.FechaIngreso ?? apto.FechaIngreso,
                FileDigitalDriveId    = fileDigital?.FileDigitalDriveId,
                FileDigitalItemId     = fileDigital?.FileDigitalItemId,
                FileDigitalRuta       = fileDigital?.FileDigitalRuta,
                Observacion           = Trim(dto.Observacion),
                CreatedDateTime       = now,
                CreatedUserId         = userId,
            };

            ctx.GthOnboarding.Add(ob);
            await ctx.SaveChangesAsync();

            return await LeerItem(ctx, ob.GthOnboardingId);
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
    /// CARTA_OFERTA_FIRMADA ya no está: la carta oferta pasó a ser el último paso de Reclutamiento y
    /// el checklist arranca en FILE_DIGITAL.
    /// </summary>
    public static class FaseOnboarding
    {
        public const string FileDigital      = "FILE_DIGITAL";
        public const string CorreoBienvenida = "CORREO_BIENVENIDA";
        public const string FormularioWeb    = "FORMULARIO_WEB";
        public const string Preinicio        = "PREINICIO";
        public const string CierreOnboarding = "CIERRE_ONBOARDING";
        public const string BaseMaestra      = "BASE_MAESTRA";
    }

    /// <summary>Códigos estables del estado del onboarding (espejo de <c>gth_onboarding_estado.codigo</c>).</summary>
    public static class EstadoOnboarding
    {
        public const string EnProceso = "EN_PROCESO";
        public const string Completo  = "COMPLETO";
    }
}
