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
        /// Proyecta la fila cruda al DTO de la tabla. <paramref name="totalFases"/> es cuántas fases
        /// tiene el catálogo vigente: el % de avance es la fase actual sobre ese total.
        /// </summary>
        private static OnboardingListItemDto MapItem(OnboardingRawRow x, int totalFases) => new()
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
            // El correo vigente es el de la base maestra; el de la carta es el histórico del envío.
            Correo       = x.PersonEmail ?? x.Ob.CartaOfertaCorreo,
            FaseCodigo   = x.FaseCodigo,
            FaseNombre   = x.FaseNombre,
            FaseOrden    = x.FaseOrden,
            EstadoCodigo = x.EstadoCodigo,
            EstadoNombre = x.EstadoNombre,
            AvancePorcentaje = totalFases <= 0
                ? 0
                : (int)Math.Round(x.FaseOrden * 100d / totalFases, MidpointRounding.AwayFromZero),
            CartaOfertaNombre    = x.Ob.CartaOfertaNombre,
            CartaOfertaUrl       = x.Ob.CartaOfertaUrl,
            CartaOfertaEnviadaEn = x.Ob.CartaOfertaEnviadaDateTime?.ToOffset(PeruOffset).DateTime,
            IniciadoEn           = x.Ob.CreatedDateTime.ToOffset(PeruOffset).DateTime,
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
                })
                .ToListAsync();

            var filas = await QueryOnboardings(ctx)
                .OrderByDescending(x => x.Ob.CreatedDateTime)
                .ThenByDescending(x => x.Ob.GthOnboardingId)
                .ToListAsync();

            var colaboradores = filas.Select(x => MapItem(x, fases.Count)).ToList();

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
        /// SELECCIONADO (la decisión final del área solicitante) sobre un requerimiento que quedó
        /// CERRADO, y que todavía no tienen un onboarding abierto.
        ///
        /// El correo destino de la carta oferta se resuelve acá, en la base de datos, y no se le pide
        /// a nadie: sale de <c>person.email</c> — el correo personal que GTH validó al aprobar el
        /// formulario del postulante — y, si esa ficha aún no existe, del correo que el postulante
        /// declaró (o, en última instancia, del que GTH usó para enviarle el formulario).
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
                join s in ctx.GthSolicitud on r.GthSolicitudId equals s.GthSolicitudId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                    // Razón social, formulario, ficha maestra y jefe directo son todos opcionales.
                join co in ctx.Contributor on r.ContributorId equals (int?)co.ContributorId into coJoin
                from co in coJoin.DefaultIfEmpty()
                join fo in ctx.GthPostulanteFormulario.Where(x => x.State)
                    on c.GthCandidatoId equals fo.GthCandidatoId into foJoin
                from fo in foJoin.DefaultIfEmpty()
                join w in ctx.Worker on s.SolicitanteWorkerId equals (int?)w.Id into wJoin
                from w in wJoin.DefaultIfEmpty()
                orderby r.Codigo descending
                select new CandidatoAptoDto
                {
                    CandidatoId     = c.GthCandidatoId,
                    RequerimientoId = r.GthRequerimientoId,
                    PersonId        = fo == null ? null : fo.PersonId,
                    // El nombre declarado por el propio postulante manda sobre el que registró GTH.
                    Nombre          = fo != null && fo.NombresCompletos != null ? fo.NombresCompletos : c.Nombre,
                    Codigo          = r.Codigo,
                    Puesto          = p.Nombre,
                    Area            = s.AreaNombre,
                    Empresa         = co == null ? null : co.ContributorName,
                    ProyectoObra    = pr.ProjectDescription,
                    // El correo de la base maestra manda; si esa ficha aún no existe (formulario sin
                    // aprobar), se cae al que declaró el postulante y, en última instancia, al que
                    // GTH usó para enviarle el formulario. Va como subconsulta y no como left join
                    // porque `person_id` cuelga de `fo`, que ya es un left join: encadenarlos deja la
                    // consulta a merced de cómo EF traduzca un DefaultIfEmpty sobre otro.
                    Correo = ctx.Person
                                .Where(x => x.PersonId == fo.PersonId)
                                .Select(x => x.Email)
                                .FirstOrDefault()
                             ?? fo.CorreoElectronico
                             ?? fo.CorreoEnvio,
                    FechaRequeridaIngreso = r.FechaRequeridaIngreso,
                    JefeDirecto = w == null ? null : (w.Person != null ? w.Person.FullName : w.ApellidoNombre),
                }).ToListAsync();

        public async Task<OnboardingContextoDto> PrepararInicio(int candidatoId, DateOnly? fechaIngreso, string? correo)
        {
            using var ctx = _factory.CreateDbContext();

            // Se reusa la misma consulta de aptos: así "quién puede entrar a onboarding" está escrito
            // una sola vez y el desplegable y esta validación no pueden discrepar.
            var apto = (await QueryCandidatosAptos(ctx)).FirstOrDefault(x => x.CandidatoId == candidatoId);
            if (apto == null)
                throw new AbrilException(
                    "Este candidato no puede pasar a onboarding: debe estar seleccionado en un proceso ya cerrado y no tener otro onboarding abierto.",
                    409);

            var destino = Trim(correo) ?? Trim(apto.Correo);
            if (string.IsNullOrWhiteSpace(destino))
                throw new AbrilException(
                    "El colaborador no tiene un correo personal registrado. Aprueba su formulario de postulante en Reclutamiento para que quede en la base maestra, o indica el correo a mano.",
                    409);

            return new OnboardingContextoDto
            {
                CandidatoId     = apto.CandidatoId,
                RequerimientoId = apto.RequerimientoId,
                PersonId        = apto.PersonId,
                Codigo          = apto.Codigo,
                Nombre          = apto.Nombre,
                Puesto          = apto.Puesto,
                Area            = apto.Area,
                Empresa         = apto.Empresa,
                ProyectoObra    = apto.ProyectoObra,
                Correo          = destino.ToLowerInvariant(),
                // Sin fecha pactada se usa la que el solicitante pidió en el requerimiento.
                FechaIngreso    = fechaIngreso ?? apto.FechaRequeridaIngreso,
                JefeDirecto     = apto.JefeDirecto,
            };
        }

        public async Task<OnboardingListItemDto> Crear(
            OnboardingContextoDto contexto,
            CartaOfertaPersistDto carta,
            string? observacion,
            int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var fase = await ctx.GthOnboardingFase
                .FirstOrDefaultAsync(f => f.Codigo == FaseOnboarding.CartaOfertaFirmada && f.State)
                ?? throw new AbrilException(
                    $"No está configurada la fase {FaseOnboarding.CartaOfertaFirmada} del onboarding.", 500);

            var estado = await ctx.GthOnboardingEstado
                .FirstOrDefaultAsync(e => e.Codigo == EstadoOnboarding.CartaEnviada && e.State)
                ?? throw new AbrilException(
                    $"No está configurado el estado {EstadoOnboarding.CartaEnviada} del onboarding.", 500);

            var now = DateTimeOffset.UtcNow;
            var ob = new GthOnboarding
            {
                GthCandidatoId        = contexto.CandidatoId,
                PersonId              = contexto.PersonId,
                GthOnboardingFaseId   = fase.GthOnboardingFaseId,
                GthOnboardingEstadoId = estado.GthOnboardingEstadoId,
                FechaIngreso          = contexto.FechaIngreso,
                CartaOfertaNombre     = carta.Nombre,
                CartaOfertaUrl        = carta.Url,
                CartaOfertaItemId     = carta.ItemId,
                CartaOfertaDriveId    = carta.DriveId,
                CartaOfertaCorreo     = contexto.Correo,
                CartaOfertaEnviadaDateTime = now,
                CartaOfertaEnviadaUserId   = userId,
                Observacion           = Trim(observacion),
                CreatedDateTime       = now,
                CreatedUserId         = userId,
            };

            ctx.GthOnboarding.Add(ob);
            await ctx.SaveChangesAsync();

            var totalFases = await ctx.GthOnboardingFase.CountAsync(f => f.State && f.Active);
            var fila = await QueryOnboardings(ctx, ob.GthOnboardingId).FirstOrDefaultAsync();

            // La fila se acaba de insertar en esta misma conexión, así que siempre está; el fallback
            // existe para no devolver null desde un método que promete una fila.
            return fila != null
                ? MapItem(fila, totalFases)
                : new OnboardingListItemDto
                {
                    OnboardingId = ob.GthOnboardingId,
                    CandidatoId  = contexto.CandidatoId,
                    PersonId     = contexto.PersonId,
                    Codigo       = contexto.Codigo,
                    Nombre       = contexto.Nombre,
                    Puesto       = contexto.Puesto,
                    Area         = contexto.Area,
                    Empresa      = contexto.Empresa,
                    ProyectoObra = contexto.ProyectoObra,
                    FechaIngreso = contexto.FechaIngreso,
                    JefeDirecto  = contexto.JefeDirecto,
                    Correo       = contexto.Correo,
                    FaseCodigo   = fase.Codigo,
                    FaseNombre   = fase.Nombre,
                    FaseOrden    = fase.Orden,
                    EstadoCodigo = estado.Codigo,
                    EstadoNombre = estado.Nombre,
                    CartaOfertaNombre    = carta.Nombre,
                    CartaOfertaUrl       = carta.Url,
                    CartaOfertaEnviadaEn = now.ToOffset(PeruOffset).DateTime,
                    IniciadoEn           = now.ToOffset(PeruOffset).DateTime,
                };
        }

        public async Task<string?> GetCartaOfertaFolderUrl()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.GthCartaOfertaFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GthCartaOfertaFolderId)
                .Select(f => f.LinkUrl)
                .FirstOrDefaultAsync();
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Espejo de <c>gth_candidato_resultado.codigo</c> para el seleccionado (dueño del puesto).</summary>
        private const string ResultadoCandidatoSeleccionado = "SELECCIONADO";

        /// <summary>Espejo de <c>gth_estado_requerimiento.codigo</c> para el proceso ya cerrado.</summary>
        private const string EstadoRequerimientoCerrado = "CERRADO";
    }

    /// <summary>Códigos estables de las fases del onboarding (espejo de <c>gth_onboarding_fase.codigo</c>).</summary>
    public static class FaseOnboarding
    {
        public const string CartaOfertaFirmada = "CARTA_OFERTA_FIRMADA";
        public const string FileDigital        = "FILE_DIGITAL";
        public const string CorreoBienvenida   = "CORREO_BIENVENIDA";
        public const string FormularioWeb      = "FORMULARIO_WEB";
        public const string Preinicio          = "PREINICIO";
        public const string CierreOnboarding   = "CIERRE_ONBOARDING";
        public const string BaseMaestra        = "BASE_MAESTRA";
    }

    /// <summary>Códigos estables del estado del onboarding (espejo de <c>gth_onboarding_estado.codigo</c>).</summary>
    public static class EstadoOnboarding
    {
        public const string CartaEnviada = "CARTA_ENVIADA";
        public const string EnProceso    = "EN_PROCESO";
        public const string Completo     = "COMPLETO";
    }
}
