using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    public class PostulanteFormularioRepository : IPostulanteFormularioRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PostulanteFormularioRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        // ── Público (postulante, por token) ───────────────────────────────────
        public async Task<PostulanteFormularioPublicoDto?> GetByToken(string token)
        {
            using var ctx = _factory.CreateDbContext();

            var f = await ctx.GthPostulanteFormulario.FirstOrDefaultAsync(x => x.Token == token && x.State);
            if (f == null) return null;

            var estado = await ctx.GthPostulanteFormularioEstado
                .FirstOrDefaultAsync(e => e.GthPostulanteFormularioEstadoId == f.GthPostulanteFormularioEstadoId);

            var head = await (
                from c in ctx.GthCandidato
                where c.GthCandidatoId == f.GthCandidatoId
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                select new { c.Nombre, Puesto = p.Nombre }).FirstOrDefaultAsync();

            var dto = new PostulanteFormularioPublicoDto
            {
                Puesto          = head?.Puesto ?? string.Empty,
                CandidatoNombre = head?.Nombre ?? string.Empty,
                EstadoCodigo    = estado?.Codigo ?? string.Empty,
                EstadoNombre    = estado?.Nombre ?? string.Empty,
                SoloLectura     = estado?.Codigo is EstadoFormularioPostulante.Aprobado or EstadoFormularioPostulante.Rechazado,
                Respuestas      = MapRespuestas(f),
            };

            dto.EstadosCiviles = await ctx.GthEstadoCivil.Where(x => x.State && x.Active).OrderBy(x => x.Orden)
                .Select(x => new OpcionDto { Id = x.GthEstadoCivilId, Nombre = x.Nombre }).ToListAsync();
            dto.TiposDocumento = await ctx.GthTipoDocumento.Where(x => x.State && x.Active).OrderBy(x => x.Orden)
                .Select(x => new TipoDocumentoOpcionDto { Id = x.GthTipoDocumentoId, Nombre = x.Nombre, Codigo = x.Codigo }).ToListAsync();
            dto.Universidades = await ctx.GthUniversidad.Where(x => x.State && x.Active).OrderBy(x => x.Orden)
                .Select(x => new OpcionDto { Id = x.GthUniversidadId, Nombre = x.Nombre }).ToListAsync();
            dto.GradosAcademicos = await ctx.GthGradoAcademico.Where(x => x.State && x.Active).OrderBy(x => x.Orden)
                .Select(x => new OpcionDto { Id = x.GthGradoAcademicoId, Nombre = x.Nombre }).ToListAsync();
            dto.Disponibilidades = await ctx.GthDisponibilidad.Where(x => x.State && x.Active).OrderBy(x => x.Orden)
                .Select(x => new OpcionDto { Id = x.GthDisponibilidadId, Nombre = x.Nombre }).ToListAsync();
            dto.MotivosCese = await ctx.GthMotivoCese.Where(x => x.State && x.Active).OrderBy(x => x.Orden)
                .Select(x => new OpcionDto { Id = x.GthMotivoCeseId, Nombre = x.Nombre }).ToListAsync();
            dto.Convocatorias = await ctx.Puesto.Where(x => x.State && x.Active).OrderBy(x => x.Orden).ThenBy(x => x.Nombre)
                .Select(x => new OpcionDto { Id = x.PuestoId, Nombre = x.Nombre }).ToListAsync();
            dto.Distritos = await ctx.GthDistrito.Where(x => x.State && x.Active).OrderBy(x => x.Orden)
                .Select(x => new DistritoOpcionDto { Id = x.GthDistritoId, Nombre = x.Nombre, Provincia = x.Provincia }).ToListAsync();

            return dto;
        }

        public async Task GuardarRespuestasByToken(string token, PostulanteFormularioRespuestasDto r)
        {
            using var ctx = _factory.CreateDbContext();

            var f = await ctx.GthPostulanteFormulario.FirstOrDefaultAsync(x => x.Token == token && x.State);
            if (f == null)
                throw new AbrilException("El enlace del formulario no es válido o ya no está disponible.", 404);

            var estados = await ctx.GthPostulanteFormularioEstado.Where(e => e.State).ToListAsync();
            var actual = estados.FirstOrDefault(e => e.GthPostulanteFormularioEstadoId == f.GthPostulanteFormularioEstadoId);
            if (actual?.Codigo is EstadoFormularioPostulante.Aprobado or EstadoFormularioPostulante.Rechazado)
                throw new AbrilException("Este formulario ya fue revisado por la empresa y no admite cambios.", 409);

            var completadoId = estados.FirstOrDefault(e => e.Codigo == EstadoFormularioPostulante.Completado)?.GthPostulanteFormularioEstadoId
                ?? throw new AbrilException("No está configurado el estado COMPLETADO del formulario del postulante.", 500);

            var now = DateTimeOffset.UtcNow;

            // Página 1
            f.NombresCompletos        = Trim(r.NombresCompletos);
            f.FechaNacimiento         = r.FechaNacimiento;
            f.GthEstadoCivilId        = r.EstadoCivilId;
            f.GthTipoDocumentoId      = r.TipoDocumentoId;
            f.NumeroDocumento         = Trim(r.NumeroDocumento);
            f.GthDistritoId           = r.DistritoId;
            f.CorreoElectronico       = Trim(r.CorreoElectronico);
            f.NumeroCelular           = Trim(r.NumeroCelular);
            f.ConvocatoriaPuestoId = r.ConvocatoriaId;
            f.PretensionesSalariales  = Trim(r.PretensionesSalariales);
            f.GthDisponibilidadId     = r.DisponibilidadId;
            f.Linkedin                = Trim(r.Linkedin);
            f.PortafolioLink          = Trim(r.PortafolioLink);
            // Página 2
            f.Profesion               = Trim(r.Profesion);
            f.GthUniversidadId        = r.UniversidadId;
            f.GthGradoAcademicoId     = r.GradoAcademicoId;
            f.NumeroColegiatura       = Trim(r.NumeroColegiatura);
            // Página 3
            f.Empresa                          = Trim(r.Empresa);
            f.AreaTrabajo                       = Trim(r.AreaTrabajo);
            f.Cargo                             = Trim(r.Cargo);
            f.FechaInicio                       = r.FechaInicio;
            f.FechaTermino                      = r.FechaTermino;
            f.GthMotivoCeseId                   = r.MotivoCeseId;
            f.FuncionesPrincipales              = Trim(r.FuncionesPrincipales);
            f.Logros                            = Trim(r.Logros);
            f.IngresoBrutoMensual               = Trim(r.IngresoBrutoMensual);
            f.PersonasACargo                    = r.PersonasACargo;
            f.JefeInmediato                     = Trim(r.JefeInmediato);
            f.AutorizaVerificacionReferencias   = r.AutorizaVerificacionReferencias;
            // Página 4
            f.DeclaracionVeracidad   = r.DeclaracionVeracidad;
            f.ConfirmacionDocumentos = r.ConfirmacionDocumentos;

            f.GthPostulanteFormularioEstadoId = completadoId;
            f.CompletadoDateTime = now;
            f.UpdatedDateTime    = now;

            await ctx.SaveChangesAsync();
        }

        // ── GTH (enviar / revisar / decidir) ──────────────────────────────────
        public async Task<EnviarFormularioContextoDto> PrepararEnvio(int candidatoId, string correo, string nuevoToken, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var cand = await (
                from c in ctx.GthCandidato
                where c.GthCandidatoId == candidatoId && c.State
                join est in ctx.GthCandidatoEstado on c.GthCandidatoEstadoId equals est.GthCandidatoEstadoId
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                select new { c.Nombre, EstadoCandidato = est.Codigo, Puesto = p.Nombre }).FirstOrDefaultAsync();
            if (cand == null)
                throw new AbrilException("Candidato no encontrado.", 404);
            if (cand.EstadoCandidato != EstadoCandidato.Aprobado)
                throw new AbrilException("El formulario solo se envía a candidatos aprobados por el solicitante.", 400);

            var estados = await ctx.GthPostulanteFormularioEstado.Where(e => e.State).ToListAsync();
            var enviado = estados.FirstOrDefault(e => e.Codigo == EstadoFormularioPostulante.Enviado)
                ?? throw new AbrilException("No está configurado el estado ENVIADO del formulario del postulante.", 500);

            var now = DateTimeOffset.UtcNow;

            var f = await ctx.GthPostulanteFormulario.FirstOrDefaultAsync(x => x.GthCandidatoId == candidatoId && x.State);
            if (f != null)
            {
                var actual = estados.FirstOrDefault(e => e.GthPostulanteFormularioEstadoId == f.GthPostulanteFormularioEstadoId);
                if (actual?.Codigo == EstadoFormularioPostulante.Aprobado)
                    throw new AbrilException("El formulario de este candidato ya fue aprobado; no es necesario reenviarlo.", 409);

                // Reenvío: vuelve a ENVIADO (conserva las respuestas para que el postulante corrija) y
                // limpia la revisión previa. Conserva el token original del enlace.
                f.GthPostulanteFormularioEstadoId = enviado.GthPostulanteFormularioEstadoId;
                f.CorreoEnvio       = correo;
                f.EnviadoDateTime   = now;
                f.EnviadoUserId     = userId;
                f.RevisadoUserId    = null;
                f.RevisadoNombre    = null;
                f.RevisadoDateTime  = null;
                f.MotivoRechazo     = null;
                f.CompletadoDateTime = null;
                f.UpdatedDateTime   = now;
                f.UpdatedUserId     = userId;
            }
            else
            {
                f = new GthPostulanteFormulario
                {
                    GthCandidatoId                  = candidatoId,
                    Token                           = nuevoToken,
                    GthPostulanteFormularioEstadoId = enviado.GthPostulanteFormularioEstadoId,
                    CorreoEnvio                     = correo,
                    EnviadoDateTime                 = now,
                    EnviadoUserId                   = userId,
                    CreatedDateTime                 = now,
                    CreatedUserId                   = userId,
                    Active                          = true,
                    State                           = true,
                };
                ctx.GthPostulanteFormulario.Add(f);
            }

            await ctx.SaveChangesAsync();

            return new EnviarFormularioContextoDto
            {
                Token           = f.Token,
                Puesto          = cand.Puesto,
                CandidatoNombre = cand.Nombre,
                Correo          = correo,
                Resumen = new CandidatoFormularioResumenDto
                {
                    EstadoCodigo = enviado.Codigo,
                    EstadoNombre = enviado.Nombre,
                    CorreoEnvio  = correo,
                    EnviadoEn    = now.ToOffset(PeruOffset).DateTime,
                },
            };
        }

        public async Task<FormularioRevisionDto> GetRevision(int candidatoId)
        {
            using var ctx = _factory.CreateDbContext();

            var candNombre = await ctx.GthCandidato
                .Where(c => c.GthCandidatoId == candidatoId && c.State)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync();
            if (candNombre == null)
                throw new AbrilException("Candidato no encontrado.", 404);

            var row = await (
                from f in ctx.GthPostulanteFormulario
                where f.GthCandidatoId == candidatoId && f.State
                join fe in ctx.GthPostulanteFormularioEstado on f.GthPostulanteFormularioEstadoId equals fe.GthPostulanteFormularioEstadoId
                join ecv in ctx.GthEstadoCivil on f.GthEstadoCivilId equals ecv.GthEstadoCivilId into ecvJ
                from ecv in ecvJ.DefaultIfEmpty()
                join tdoc in ctx.GthTipoDocumento on f.GthTipoDocumentoId equals tdoc.GthTipoDocumentoId into tdocJ
                from tdoc in tdocJ.DefaultIfEmpty()
                join dist in ctx.GthDistrito on f.GthDistritoId equals dist.GthDistritoId into distJ
                from dist in distJ.DefaultIfEmpty()
                join conv in ctx.Puesto on f.ConvocatoriaPuestoId equals conv.PuestoId into convJ
                from conv in convJ.DefaultIfEmpty()
                join disp in ctx.GthDisponibilidad on f.GthDisponibilidadId equals disp.GthDisponibilidadId into dispJ
                from disp in dispJ.DefaultIfEmpty()
                join uni in ctx.GthUniversidad on f.GthUniversidadId equals uni.GthUniversidadId into uniJ
                from uni in uniJ.DefaultIfEmpty()
                join grad in ctx.GthGradoAcademico on f.GthGradoAcademicoId equals grad.GthGradoAcademicoId into gradJ
                from grad in gradJ.DefaultIfEmpty()
                join mce in ctx.GthMotivoCese on f.GthMotivoCeseId equals mce.GthMotivoCeseId into mceJ
                from mce in mceJ.DefaultIfEmpty()
                select new
                {
                    F            = f,
                    EstadoCodigo = fe.Codigo,
                    EstadoNombre = fe.Nombre,
                    EstadoCivil  = ecv != null ? ecv.Nombre : null,
                    TipoDocumento = tdoc != null ? tdoc.Nombre : null,
                    Distrito     = dist != null ? dist.Nombre : null,
                    Convocatoria = conv != null ? conv.Nombre : null,
                    Disponibilidad = disp != null ? disp.Nombre : null,
                    Universidad  = uni != null ? uni.Nombre : null,
                    GradoAcademico = grad != null ? grad.Nombre : null,
                    MotivoCese   = mce != null ? mce.Nombre : null,
                }).FirstOrDefaultAsync();

            if (row == null)
                return new FormularioRevisionDto { Existe = false, CandidatoNombre = candNombre };

            var f2 = row.F;
            var dto = new FormularioRevisionDto
            {
                Existe          = true,
                EstadoCodigo    = row.EstadoCodigo,
                EstadoNombre    = row.EstadoNombre,
                CandidatoNombre = candNombre,
                CorreoEnvio     = f2.CorreoEnvio,
                EnviadoEn       = f2.EnviadoDateTime?.ToOffset(PeruOffset).DateTime,
                CompletadoEn    = f2.CompletadoDateTime?.ToOffset(PeruOffset).DateTime,
                RevisadoNombre  = f2.RevisadoNombre,
                RevisadoEn      = f2.RevisadoDateTime?.ToOffset(PeruOffset).DateTime,
                MotivoRechazo   = f2.MotivoRechazo,
            };

            // Los datos se muestran una vez que el postulante completó el formulario (no en ENVIADO).
            if (row.EstadoCodigo != EstadoFormularioPostulante.Enviado)
            {
                dto.Datos = new FormularioDatosDto
                {
                    NombresCompletos       = f2.NombresCompletos,
                    FechaNacimiento        = f2.FechaNacimiento,
                    EstadoCivil            = row.EstadoCivil,
                    TipoDocumento          = row.TipoDocumento,
                    NumeroDocumento        = f2.NumeroDocumento,
                    Distrito               = row.Distrito,
                    CorreoElectronico      = f2.CorreoElectronico,
                    NumeroCelular          = f2.NumeroCelular,
                    Convocatoria           = row.Convocatoria,
                    PretensionesSalariales = f2.PretensionesSalariales,
                    Disponibilidad         = row.Disponibilidad,
                    Linkedin               = f2.Linkedin,
                    PortafolioLink         = f2.PortafolioLink,
                    Profesion              = f2.Profesion,
                    Universidad            = row.Universidad,
                    GradoAcademico         = row.GradoAcademico,
                    NumeroColegiatura      = f2.NumeroColegiatura,
                    Empresa                = f2.Empresa,
                    AreaTrabajo            = f2.AreaTrabajo,
                    Cargo                  = f2.Cargo,
                    FechaInicio            = f2.FechaInicio,
                    FechaTermino           = f2.FechaTermino,
                    MotivoCese             = row.MotivoCese,
                    FuncionesPrincipales   = f2.FuncionesPrincipales,
                    Logros                 = f2.Logros,
                    IngresoBrutoMensual    = f2.IngresoBrutoMensual,
                    PersonasACargo         = f2.PersonasACargo,
                    JefeInmediato          = f2.JefeInmediato,
                    AutorizaVerificacionReferencias = f2.AutorizaVerificacionReferencias,
                    DeclaracionVeracidad   = f2.DeclaracionVeracidad,
                    ConfirmacionDocumentos = f2.ConfirmacionDocumentos,
                };
            }

            return dto;
        }

        public async Task<CandidatoFormularioResumenDto> RegistrarDecision(int candidatoId, bool aprobado, string? motivo, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var f = await ctx.GthPostulanteFormulario.FirstOrDefaultAsync(x => x.GthCandidatoId == candidatoId && x.State);
            if (f == null)
                throw new AbrilException("No se encontró el formulario del candidato.", 404);

            var estados = await ctx.GthPostulanteFormularioEstado.Where(e => e.State).ToListAsync();
            var actual = estados.FirstOrDefault(e => e.GthPostulanteFormularioEstadoId == f.GthPostulanteFormularioEstadoId);
            if (actual?.Codigo != EstadoFormularioPostulante.Completado)
                throw new AbrilException("Solo puedes aprobar o rechazar un formulario que el postulante ya completó.", 409);

            var destinoCodigo = aprobado ? EstadoFormularioPostulante.Aprobado : EstadoFormularioPostulante.Rechazado;
            var destino = estados.FirstOrDefault(e => e.Codigo == destinoCodigo)
                ?? throw new AbrilException($"No está configurado el estado {destinoCodigo} del formulario del postulante.", 500);

            // Nombre del revisor (snapshot para mostrar "Aprobado por …"). Best-effort.
            string? revisorNombre = userId.HasValue
                ? await ctx.Worker
                    .Where(w => w.Person != null && w.Person.UserId == userId.Value)
                    .Select(w => w.Person!.FullName ?? w.ApellidoNombre)
                    .FirstOrDefaultAsync()
                : null;

            var now = DateTimeOffset.UtcNow;
            f.GthPostulanteFormularioEstadoId = destino.GthPostulanteFormularioEstadoId;
            f.RevisadoUserId   = userId;
            f.RevisadoNombre   = revisorNombre;
            f.RevisadoDateTime = now;
            f.MotivoRechazo    = aprobado ? null : Trim(motivo);
            f.UpdatedDateTime  = now;
            f.UpdatedUserId    = userId;

            await ctx.SaveChangesAsync();

            return new CandidatoFormularioResumenDto
            {
                EstadoCodigo   = destino.Codigo,
                EstadoNombre   = destino.Nombre,
                CorreoEnvio    = f.CorreoEnvio,
                EnviadoEn      = f.EnviadoDateTime?.ToOffset(PeruOffset).DateTime,
                CompletadoEn   = f.CompletadoDateTime?.ToOffset(PeruOffset).DateTime,
                RevisadoNombre = revisorNombre,
                RevisadoEn     = now.ToOffset(PeruOffset).DateTime,
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static PostulanteFormularioRespuestasDto MapRespuestas(GthPostulanteFormulario f) => new()
        {
            NombresCompletos       = f.NombresCompletos,
            FechaNacimiento        = f.FechaNacimiento,
            EstadoCivilId          = f.GthEstadoCivilId,
            TipoDocumentoId        = f.GthTipoDocumentoId,
            NumeroDocumento        = f.NumeroDocumento,
            DistritoId             = f.GthDistritoId,
            CorreoElectronico      = f.CorreoElectronico,
            NumeroCelular          = f.NumeroCelular,
            ConvocatoriaId         = f.ConvocatoriaPuestoId,
            PretensionesSalariales = f.PretensionesSalariales,
            DisponibilidadId       = f.GthDisponibilidadId,
            Linkedin               = f.Linkedin,
            PortafolioLink         = f.PortafolioLink,
            Profesion              = f.Profesion,
            UniversidadId          = f.GthUniversidadId,
            GradoAcademicoId       = f.GthGradoAcademicoId,
            NumeroColegiatura      = f.NumeroColegiatura,
            Empresa                = f.Empresa,
            AreaTrabajo            = f.AreaTrabajo,
            Cargo                  = f.Cargo,
            FechaInicio            = f.FechaInicio,
            FechaTermino           = f.FechaTermino,
            MotivoCeseId           = f.GthMotivoCeseId,
            FuncionesPrincipales   = f.FuncionesPrincipales,
            Logros                 = f.Logros,
            IngresoBrutoMensual    = f.IngresoBrutoMensual,
            PersonasACargo         = f.PersonasACargo,
            JefeInmediato          = f.JefeInmediato,
            AutorizaVerificacionReferencias = f.AutorizaVerificacionReferencias,
            DeclaracionVeracidad   = f.DeclaracionVeracidad,
            ConfirmacionDocumentos = f.ConfirmacionDocumentos,
        };
    }

    /// <summary>Códigos estables del estado del formulario del postulante (espejo de gth_postulante_formulario_estado.codigo).</summary>
    internal static class EstadoFormularioPostulante
    {
        public const string Enviado    = "ENVIADO";
        public const string Completado = "COMPLETADO";
        public const string Aprobado   = "APROBADO";
        public const string Rechazado  = "RECHAZADO";
    }
}
