using System.Security.Cryptography;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Repositories
{
    /// <summary>
    /// Formulario «Nuevos Talentos»: el que abre el correo de bienvenida y llena el colaborador
    /// desde una página pública. Convive con <see cref="OnboardingRepository"/> —la bandeja— pero
    /// vive aparte porque tiene su propia cara anónima y su propio ciclo.
    /// </summary>
    public class OnboardingFormularioRepository : IOnboardingFormularioRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IOnboardingRepository _onboardingRepo;

        public OnboardingFormularioRepository(
            IDbContextFactory<AppDbContext> factory, IOnboardingRepository onboardingRepo)
        {
            _factory        = factory;
            _onboardingRepo = onboardingRepo;
        }

        /// <summary>Estados de <c>gth_onboarding_formulario_estado</c>.</summary>
        private const string EstadoEnviado    = "ENVIADO";
        private const string EstadoCompletado = "COMPLETADO";

        // ── Envío del correo de bienvenida ────────────────────────────────────

        public async Task<BienvenidaContextoDto> PrepararBienvenida(
            int onboardingId, DateOnly? fechaLimite, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var ctxDatos = await (
                from ob in ctx.GthOnboarding
                where ob.State && ob.GthOnboardingId == onboardingId
                join c in ctx.GthCandidato on ob.GthCandidatoId equals c.GthCandidatoId
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                join s in ctx.GthSolicitud on r.GthSolicitudId equals s.GthSolicitudId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join co in ctx.Contributor on r.ContributorId equals (int?)co.ContributorId into coJoin
                from co in coJoin.DefaultIfEmpty()
                join pe in ctx.Person on ob.PersonId equals (int?)pe.PersonId into peJoin
                from pe in peJoin.DefaultIfEmpty()
                select new
                {
                    Onboarding   = ob,
                    Codigo       = r.Codigo,
                    Nombre       = pe != null && pe.FullName != null ? pe.FullName : c.Nombre,
                    Correo       = pe == null ? null : pe.Email,
                    Puesto       = p.Nombre,
                    Area         = s.AreaNombre,
                    Empresa      = co == null ? null : co.ContributorName,
                    ProyectoObra = pr.ProjectDescription,
                }).FirstOrDefaultAsync()
                ?? throw new AbrilException("El onboarding indicado no existe o fue dado de baja.", 404);

            if (string.IsNullOrWhiteSpace(ctxDatos.Correo))
                throw new AbrilException(
                    "El colaborador no tiene correo personal en su ficha maestra: sin él no hay a "
                    + "quién mandarle la bienvenida.", 409);

            var formulario = await ctx.GthOnboardingFormulario
                .FirstOrDefaultAsync(f => f.GthOnboardingId == onboardingId && f.State);

            var estadoEnviadoId = await EstadoId(ctx, EstadoEnviado);
            var ahora = DateTimeOffset.UtcNow;

            if (formulario == null)
            {
                formulario = new GthOnboardingFormulario
                {
                    GthOnboardingId = onboardingId,
                    Token           = NuevoToken(),
                    GthOnboardingFormularioEstadoId = estadoEnviadoId,
                    CorreoEnvio     = ctxDatos.Correo!,
                    FechaLimite     = fechaLimite,
                    CreatedDateTime = ahora,
                    CreatedUserId   = userId,
                };

                // Precarga de lo ya pactado (puesto, fecha, sueldo, razón social): el colaborador
                // confirma en vez de transcribir el correo. Va aparte para no meter los joins
                // dentro del inicializador.
                await PrecargarPactado(ctx, formulario, onboardingId);

                ctx.GthOnboardingFormulario.Add(formulario);
            }
            else
            {
                // Reenvío: el enlace que ya recibió tiene que seguir sirviendo, así que el token no
                // se toca. Lo que sí se actualiza es a qué buzón sale y hasta cuándo tiene.
                formulario.CorreoEnvio = ctxDatos.Correo!;
                if (fechaLimite.HasValue) formulario.FechaLimite = fechaLimite;
                formulario.UpdatedDateTime = ahora;
                formulario.UpdatedUserId   = userId;
            }

            // `enviado_date_time` NO se toca acá: es lo que marca la actividad del checklist como
            // cumplida, y en este punto el correo todavía no salió. Si el envío falla, el
            // formulario queda abierto (su enlace ya es válido) pero la actividad sigue pendiente
            // para reintentar. Lo escribe MarcarBienvenidaEnviada, después del envío.
            await ctx.SaveChangesAsync();

            return new BienvenidaContextoDto
            {
                OnboardingId = onboardingId,
                Codigo       = ctxDatos.Codigo,
                Nombre       = ctxDatos.Nombre,
                Puesto       = ctxDatos.Puesto,
                Area         = ctxDatos.Area,
                Empresa      = ctxDatos.Empresa,
                ProyectoObra = ctxDatos.ProyectoObra,
                FechaIngreso = ctxDatos.Onboarding.FechaIngreso,
                Correo       = ctxDatos.Correo!,
                Token        = formulario.Token,
                FechaLimite  = formulario.FechaLimite,
                EnviadoEn    = formulario.EnviadoDateTime,
            };
        }

        /// <summary>
        /// Precarga en el formulario nuevo lo que la carta oferta ya pactó: puesto, fecha de
        /// ingreso, sueldo y razón social. El colaborador los ve cargados y confirma; si algo no
        /// coincide con su correo, lo corrige y GTH lo ve al revisar.
        /// </summary>
        private static async Task PrecargarPactado(AppDbContext ctx, GthOnboardingFormulario f, int onboardingId)
        {
            var pactado = await (
                from ob in ctx.GthOnboarding
                where ob.GthOnboardingId == onboardingId
                join c in ctx.GthCandidato on ob.GthCandidatoId equals c.GthCandidatoId
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                join ca in ctx.GthCartaOferta.Where(x => x.State)
                    on c.GthCandidatoId equals ca.GthCandidatoId into caJoin
                from ca in caJoin.DefaultIfEmpty()
                select new
                {
                    r.PuestoId,
                    r.ContributorId,
                    // El sueldo pactado manda sobre el presupuestado de la vacante.
                    Sueldo = ca != null && ca.Sueldo != null ? ca.Sueldo : r.SalarioBrutoMensual,
                    FechaIngreso = ob.FechaIngreso,
                }).FirstOrDefaultAsync();

            if (pactado == null) return;

            f.PuestoId            = pactado.PuestoId;
            f.ContributorId       = pactado.ContributorId;
            f.RemuneracionMensual = pactado.Sueldo;
            f.FechaIngreso        = pactado.FechaIngreso;
        }

        public async Task<OnboardingListItemDto> MarcarBienvenidaEnviada(
            int onboardingId, string email, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var formulario = await ctx.GthOnboardingFormulario
                .FirstOrDefaultAsync(f => f.GthOnboardingId == onboardingId && f.State)
                ?? throw new AbrilException("El formulario del colaborador ya no existe.", 404);

            var ahora = DateTimeOffset.UtcNow;
            formulario.CorreoEnvio     = email;
            formulario.EnviadoDateTime = ahora;
            formulario.EnviadoUserId   = userId;
            formulario.UpdatedDateTime = ahora;
            formulario.UpdatedUserId   = userId;

            await ctx.SaveChangesAsync();

            // La fila de la tabla se relee con la misma proyección de la bandeja: así el detalle y
            // la tabla no pueden mostrar dos avances distintos.
            return await _onboardingRepo.GetItem(onboardingId);
        }

        // ── Cara pública (el colaborador, por token) ──────────────────────────

        public async Task<OnboardingFormularioPublicoDto?> GetPublico(string token)
        {
            using var ctx = _factory.CreateDbContext();

            var fila = await (
                from f in ctx.GthOnboardingFormulario
                where f.State && f.Token == token
                join ob in ctx.GthOnboarding on f.GthOnboardingId equals ob.GthOnboardingId
                join c in ctx.GthCandidato on ob.GthCandidatoId equals c.GthCandidatoId
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                join s in ctx.GthSolicitud on r.GthSolicitudId equals s.GthSolicitudId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                join pe in ctx.Person on ob.PersonId equals (int?)pe.PersonId into peJoin
                from pe in peJoin.DefaultIfEmpty()
                select new
                {
                    Formulario   = f,
                    Codigo       = r.Codigo,
                    Nombre       = pe != null && pe.FullName != null ? pe.FullName : c.Nombre,
                    Puesto       = p.Nombre,
                    Area         = s.AreaNombre,
                    ProyectoObra = pr.ProjectDescription,
                    CandidatoId  = c.GthCandidatoId,
                    Persona      = pe,
                }).FirstOrDefaultAsync();

            if (fila == null) return null;

            var f0 = fila.Formulario;

            return new OnboardingFormularioPublicoDto
            {
                Nombre           = fila.Nombre,
                Codigo           = fila.Codigo,
                Puesto           = fila.Puesto,
                Area             = fila.Area,
                ProyectoObra     = fila.ProyectoObra,
                FechaLimite      = f0.FechaLimite,
                SoloLectura      = f0.CompletadoDateTime != null,
                DatosRegistrados = await LeerDatosRegistrados(ctx, fila.CandidatoId, fila.Persona),

                Puestos = await ctx.Puesto
                    .Where(p => p.State && p.Active && p.AreaDestinoScopeId != null)
                    .OrderBy(p => p.Nombre)
                    .Select(p => new OpcionFormularioDto { Id = p.PuestoId, Nombre = p.Nombre })
                    .ToListAsync(),

                Ubicaciones = await ctx.GthOnboardingUbicacion
                    .Where(u => u.State && u.Active)
                    .OrderBy(u => u.Orden)
                    .Select(u => new OpcionFormularioDto { Id = u.GthOnboardingUbicacionId, Nombre = u.Nombre })
                    .ToListAsync(),

                // Solo las razones sociales del grupo: son las únicas con las que se contrata, y
                // cada una viaja con su banco para poder formular la pregunta de la cuenta sueldo.
                RazonesSociales = await ctx.Contributor
                    .Where(c => c.State && c.Active && c.EsAbril)
                    .OrderBy(c => c.ContributorName)
                    .Select(c => new RazonSocialOpcionDto
                    {
                        Id          = c.ContributorId,
                        Nombre      = c.ContributorName,
                        BancoNombre = ctx.Banco.Where(b => b.BancoId == c.BancoId)
                                               .Select(b => b.Nombre).FirstOrDefault(),
                    })
                    .ToListAsync(),

                Sexos = await ctx.Sexo
                    .Where(x => x.State && x.Active)
                    .OrderBy(x => x.Orden)
                    .Select(x => new OpcionFormularioDto { Id = x.SexoId, Nombre = x.Nombre })
                    .ToListAsync(),

                TallasCalzado = await ctx.TallaCalzado
                    .Where(x => x.State && x.Active)
                    .OrderBy(x => x.Orden)
                    .Select(x => new OpcionFormularioDto { Id = x.TallaCalzadoId, Nombre = x.Nombre })
                    .ToListAsync(),

                Tallas = await ctx.Talla
                    .Where(x => x.State && x.Active)
                    .OrderBy(x => x.Orden)
                    .Select(x => new OpcionFormularioDto { Id = x.TallaId, Nombre = x.Nombre })
                    .ToListAsync(),

                RentaQuinta = await ctx.GthRentaQuinta
                    .Where(x => x.State && x.Active)
                    .OrderBy(x => x.Orden)
                    .Select(x => new OpcionFormularioDto { Id = x.GthRentaQuintaId, Nombre = x.Nombre })
                    .ToListAsync(),

                Respuestas = MapRespuestas(f0),
            };
        }

        /// <summary>
        /// Lo que el proceso ya sabe del colaborador y por eso el formulario NO vuelve a preguntar.
        /// Sale del formulario del postulante —que es donde lo declaró él mismo y GTH lo validó— y,
        /// para lo que ese formulario no tiene, de su ficha maestra.
        /// </summary>
        private static async Task<DatosRegistradosDto> LeerDatosRegistrados(
            AppDbContext ctx, int candidatoId, Abril_Backend.Infrastructure.Models.Person? persona)
        {
            var postulante = await (
                from pf in ctx.GthPostulanteFormulario
                where pf.State && pf.GthCandidatoId == candidatoId
                join td in ctx.GthTipoDocumento on pf.GthTipoDocumentoId equals (int?)td.GthTipoDocumentoId into tdJoin
                from td in tdJoin.DefaultIfEmpty()
                join di in ctx.GthDistrito on pf.GthDistritoId equals (int?)di.GthDistritoId into diJoin
                from di in diJoin.DefaultIfEmpty()
                join ec in ctx.GthEstadoCivil on pf.GthEstadoCivilId equals (int?)ec.GthEstadoCivilId into ecJoin
                from ec in ecJoin.DefaultIfEmpty()
                select new
                {
                    pf.NombresCompletos,
                    TipoDocumento   = td == null ? null : td.Nombre,
                    pf.NumeroDocumento,
                    pf.FechaNacimiento,
                    pf.NumeroCelular,
                    Distrito    = di == null ? null : di.Nombre,
                    EstadoCivil = ec == null ? null : ec.Nombre,
                    pf.CorreoElectronico,
                }).FirstOrDefaultAsync();

            return new DatosRegistradosDto
            {
                NombresCompletos  = postulante?.NombresCompletos ?? persona?.FullName,
                TipoDocumento     = postulante?.TipoDocumento,
                NumeroDocumento   = postulante?.NumeroDocumento ?? persona?.DocumentIdentityCode,
                FechaNacimiento   = postulante?.FechaNacimiento ?? persona?.FechaNacimiento,
                // El celular de la ficha maestra es numérico: solo sirve de respaldo si el
                // formulario del postulante no existe (ingreso directo).
                NumeroCelular     = postulante?.NumeroCelular ?? persona?.PhoneNumber?.ToString(),
                Distrito          = postulante?.Distrito ?? persona?.Distrito,
                EstadoCivil       = postulante?.EstadoCivil,
                CorreoElectronico = postulante?.CorreoElectronico ?? persona?.Email,
            };
        }

        public async Task<string> GuardarPublico(string token, OnboardingFormularioRespuestasDto r)
        {
            using var ctx = _factory.CreateDbContext();

            var formulario = await ctx.GthOnboardingFormulario
                .FirstOrDefaultAsync(f => f.State && f.Token == token)
                ?? throw new AbrilException("El enlace del formulario no es válido o ya no está disponible.", 404);

            if (formulario.CompletadoDateTime != null)
                throw new AbrilException(
                    "Este formulario ya fue enviado y no admite más cambios. Si necesitas corregir "
                    + "algo, comunícate con Gestión del Talento Humano.", 409);

            formulario.Direccion             = Trim(r.Direccion);
            formulario.PuestoId              = r.PuestoId;
            formulario.FechaIngreso          = r.FechaIngreso;
            formulario.RemuneracionMensual   = r.RemuneracionMensual;
            formulario.GthOnboardingUbicacionId = r.UbicacionId;
            formulario.ContributorId         = r.ContributorId;
            formulario.CuentaSueldo          = r.CuentaSueldo;
            formulario.SexoId                = r.SexoId;
            formulario.ContactoEmergencia    = Trim(r.ContactoEmergencia);
            formulario.CelularEmergencia     = Trim(r.CelularEmergencia);
            formulario.NumeroHijos           = r.NumeroHijos;
            formulario.TallaCalzadoId        = r.TallaCalzadoId;
            formulario.TallaId               = r.TallaId;
            formulario.UsaLentes             = r.UsaLentes;
            formulario.Hobbies               = Trim(r.Hobbies);
            formulario.GthRentaQuintaId      = r.RentaQuintaId;
            formulario.FechaEmo              = r.FechaEmo;
            formulario.DeclaracionVeracidad  = r.DeclaracionVeracidad;

            var ahora = DateTimeOffset.UtcNow;
            formulario.CompletadoDateTime = ahora;
            formulario.GthOnboardingFormularioEstadoId = await EstadoId(ctx, EstadoCompletado);
            formulario.UpdatedDateTime = ahora;

            await ctx.SaveChangesAsync();

            return await (
                from ob in ctx.GthOnboarding
                where ob.GthOnboardingId == formulario.GthOnboardingId
                join c in ctx.GthCandidato on ob.GthCandidatoId equals c.GthCandidatoId
                join pe in ctx.Person on ob.PersonId equals (int?)pe.PersonId into peJoin
                from pe in peJoin.DefaultIfEmpty()
                select pe != null && pe.FullName != null ? pe.FullName : c.Nombre
            ).FirstOrDefaultAsync() ?? "";
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static OnboardingFormularioRespuestasDto MapRespuestas(GthOnboardingFormulario f) => new()
        {
            Direccion           = f.Direccion,
            PuestoId            = f.PuestoId,
            FechaIngreso        = f.FechaIngreso,
            RemuneracionMensual = f.RemuneracionMensual,
            UbicacionId         = f.GthOnboardingUbicacionId,
            ContributorId       = f.ContributorId,
            CuentaSueldo        = f.CuentaSueldo,
            SexoId              = f.SexoId,
            ContactoEmergencia  = f.ContactoEmergencia,
            CelularEmergencia   = f.CelularEmergencia,
            NumeroHijos         = f.NumeroHijos,
            TallaCalzadoId      = f.TallaCalzadoId,
            TallaId             = f.TallaId,
            UsaLentes           = f.UsaLentes,
            Hobbies             = f.Hobbies,
            RentaQuintaId       = f.GthRentaQuintaId,
            FechaEmo            = f.FechaEmo,
            DeclaracionVeracidad = f.DeclaracionVeracidad,
        };

        private static async Task<int> EstadoId(AppDbContext ctx, string codigo)
        {
            var id = await ctx.GthOnboardingFormularioEstado
                .Where(e => e.State && e.Codigo == codigo)
                .Select(e => e.GthOnboardingFormularioEstadoId)
                .FirstOrDefaultAsync();

            if (id == 0)
                throw new AbrilException(
                    $"No está configurado el estado {codigo} del formulario de onboarding.", 500);

            return id;
        }

        /// <summary>Token del enlace público: 48 caracteres hexadecimales de entropía criptográfica.</summary>
        private static string NuevoToken() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
