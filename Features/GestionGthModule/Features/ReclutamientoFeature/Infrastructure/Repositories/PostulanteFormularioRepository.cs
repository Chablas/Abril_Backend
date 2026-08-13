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
                // Solo el APROBADO cierra el formulario. El RECHAZADO se reabre con las respuestas ya
                // cargadas para que el postulante corrija lo observado y lo vuelva a enviar.
                SoloLectura     = estado?.Codigo == EstadoFormularioPostulante.Aprobado,
                Observaciones   = estado?.Codigo == EstadoFormularioPostulante.Rechazado ? f.MotivoRechazo : null,
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
            dto.Distritos = await ctx.GthDistrito.Where(x => x.State && x.Active).OrderBy(x => x.Orden)
                .Select(x => new DistritoOpcionDto { Id = x.GthDistritoId, Nombre = x.Nombre, Provincia = x.Provincia }).ToListAsync();

            return dto;
        }

        public async Task<FormularioCompletadoContextoDto> GuardarRespuestasByToken(
            string token, PostulanteFormularioRespuestasDto r)
        {
            using var ctx = _factory.CreateDbContext();

            var f = await ctx.GthPostulanteFormulario.FirstOrDefaultAsync(x => x.Token == token && x.State);
            if (f == null)
                throw new AbrilException("El enlace del formulario no es válido o ya no está disponible.", 404);

            var estados = await ctx.GthPostulanteFormularioEstado.Where(e => e.State).ToListAsync();
            var actual = estados.FirstOrDefault(e => e.GthPostulanteFormularioEstadoId == f.GthPostulanteFormularioEstadoId);
            // Un formulario RECHAZADO sí admite cambios: es justamente lo que se le pide al postulante
            // en el correo de rechazo. El APROBADO es el único que queda cerrado.
            if (actual?.Codigo == EstadoFormularioPostulante.Aprobado)
                throw new AbrilException("Este formulario ya fue aprobado por la empresa y no admite cambios.", 409);

            var completadoId = estados.FirstOrDefault(e => e.Codigo == EstadoFormularioPostulante.Completado)?.GthPostulanteFormularioEstadoId
                ?? throw new AbrilException("No está configurado el estado COMPLETADO del formulario del postulante.", 500);

            var esCorreccion = actual?.Codigo == EstadoFormularioPostulante.Rechazado;
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

            // Cabecera del proceso para el aviso a GTH: se lee acá y no en el servicio para no
            // abrir una segunda conexión por un envío que ya está resuelto.
            var head = await (
                from c in ctx.GthCandidato
                where c.GthCandidatoId == f.GthCandidatoId
                join req in ctx.GthRequerimiento on c.GthRequerimientoId equals req.GthRequerimientoId
                join p in ctx.Puesto on req.PuestoId equals p.PuestoId
                join pr in ctx.Project on req.ProjectId equals pr.ProjectId
                select new
                {
                    req.Codigo,
                    Puesto       = p.Nombre,
                    Area         = req.Solicitud!.AreaNombre,
                    ProyectoObra = pr.ProjectDescription,
                    c.Nombre,
                }).FirstOrDefaultAsync();

            return new FormularioCompletadoContextoDto
            {
                Codigo           = head?.Codigo ?? string.Empty,
                Puesto           = head?.Puesto ?? string.Empty,
                Area             = head?.Area,
                ProyectoObra     = head?.ProyectoObra,
                CandidatoNombre  = f.NombresCompletos ?? head?.Nombre ?? string.Empty,
                CorreoPostulante = f.CorreoElectronico,
                NumeroCelular    = f.NumeroCelular,
                CompletadoEn     = now.ToOffset(PeruOffset).DateTime,
                EsCorreccion     = esCorreccion,
            };
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

            // Estado en el que queda el formulario tras el envío: ENVIADO salvo cuando se reenvía uno
            // rechazado, que se queda como está (ver abajo).
            var destino = enviado;
            var esRechazo = false;

            var f = await ctx.GthPostulanteFormulario.FirstOrDefaultAsync(x => x.GthCandidatoId == candidatoId && x.State);
            if (f != null)
            {
                var actual = estados.FirstOrDefault(e => e.GthPostulanteFormularioEstadoId == f.GthPostulanteFormularioEstadoId);

                // Un formulario ya APROBADO también se puede reenviar (el postulante se equivocó en
                // algún dato): cae en el camino de abajo, que lo devuelve a ENVIADO conservando lo
                // que declaró y limpiando la aprobación. Deja de contar como aprobado hasta que lo
                // complete de nuevo y GTH lo vuelva a revisar; la pantalla lo confirma antes.

                // Solo cuenta como "rechazo con observaciones" el de un formulario que el postulante
                // sí completó. El que se rechazó porque nunca lo llenó (sin CompletadoDateTime) no
                // tiene nada que corregir: reenviarlo es volver a invitarlo, camino de abajo.
                esRechazo = actual?.Codigo == EstadoFormularioPostulante.Rechazado
                            && f.CompletadoDateTime != null;

                if (esRechazo)
                {
                    // Reenvío de un formulario observado: se conserva el estado RECHAZADO junto con el
                    // motivo y la revisión. Si se pasara a ENVIADO se borrarían las observaciones, y el
                    // postulante recibiría el correo de invitación —como si nunca lo hubieran rechazado—
                    // y abriría el formulario sin saber qué corregir. Solo se actualiza el rastro del envío.
                    destino = actual!;
                    f.CorreoEnvio     = correo;
                    f.EnviadoDateTime = now;
                    f.EnviadoUserId   = userId;
                    f.UpdatedDateTime = now;
                    f.UpdatedUserId   = userId;
                }
                else
                {
                    // Reenvío normal: vuelve a ENVIADO (conserva las respuestas para que el postulante
                    // corrija) y limpia la revisión previa. Conserva el token original del enlace.
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
                Token  = f.Token,
                Puesto = cand.Puesto,
                // El nombre que declaró el propio postulante manda sobre el que registró GTH.
                CandidatoNombre = Trim(f.NombresCompletos) ?? cand.Nombre,
                Correo          = correo,
                EsRechazo       = esRechazo,
                Motivo          = esRechazo ? f.MotivoRechazo : null,
                Resumen = new CandidatoFormularioResumenDto
                {
                    EstadoCodigo = destino.Codigo,
                    EstadoNombre = destino.Nombre,
                    CorreoEnvio  = correo,
                    EnviadoEn    = now.ToOffset(PeruOffset).DateTime,
                    // Al reenviar un rechazo el formulario sigue completado y revisado: la bandeja debe
                    // seguir mostrando el badge "Rechazado" y quién lo revisó.
                    CompletadoEn   = f.CompletadoDateTime?.ToOffset(PeruOffset).DateTime,
                    RevisadoNombre = f.RevisadoNombre,
                    RevisadoEn     = f.RevisadoDateTime?.ToOffset(PeruOffset).DateTime,
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

        public async Task<DecisionFormularioContextoDto> RegistrarDecision(int candidatoId, bool aprobado, string? motivo, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var f = await ctx.GthPostulanteFormulario.FirstOrDefaultAsync(x => x.GthCandidatoId == candidatoId && x.State);
            if (f == null)
                throw new AbrilException("No se encontró el formulario del candidato.", 404);

            var estados = await ctx.GthPostulanteFormularioEstado.Where(e => e.State).ToListAsync();
            var actual = estados.FirstOrDefault(e => e.GthPostulanteFormularioEstadoId == f.GthPostulanteFormularioEstadoId);
            var estabaCompletado = actual?.Codigo == EstadoFormularioPostulante.Completado;

            // Aprobar exige que el postulante lo haya completado: no se puede dar por buena
            // información que nadie declaró. Rechazar también vale sobre un formulario ENVIADO que
            // nunca llenó — es lo que destraba el paso a entrevistas cuando el postulante no
            // responde. El token no se toca: si lo completa después, vuelve a caer como COMPLETADO.
            if (aprobado && !estabaCompletado)
                throw new AbrilException("Solo puedes aprobar un formulario que el postulante ya completó.", 409);
            if (!aprobado && !estabaCompletado && actual?.Codigo != EstadoFormularioPostulante.Enviado)
                throw new AbrilException("Solo puedes rechazar un formulario que ya se le envió al postulante.", 409);

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

            var contexto = new DecisionFormularioContextoDto
            {
                Resumen = new CandidatoFormularioResumenDto
                {
                    EstadoCodigo   = destino.Codigo,
                    EstadoNombre   = destino.Nombre,
                    CorreoEnvio    = f.CorreoEnvio,
                    EnviadoEn      = f.EnviadoDateTime?.ToOffset(PeruOffset).DateTime,
                    CompletadoEn   = f.CompletadoDateTime?.ToOffset(PeruOffset).DateTime,
                    RevisadoNombre = revisorNombre,
                    RevisadoEn     = now.ToOffset(PeruOffset).DateTime,
                },
            };

            // Los datos del correo solo hacen falta al rechazar un formulario que el postulante sí
            // completó, así que la consulta extra se paga únicamente en ese caso.
            contexto.AvisarAlPostulante = !aprobado && estabaCompletado;
            if (contexto.AvisarAlPostulante)
            {
                var head = await (
                    from c in ctx.GthCandidato
                    where c.GthCandidatoId == candidatoId
                    join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                    join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                    select new { c.Nombre, Puesto = p.Nombre }).FirstOrDefaultAsync();

                contexto.Token  = f.Token;
                contexto.Correo = f.CorreoEnvio;
                contexto.Puesto = head?.Puesto ?? string.Empty;
                // El nombre declarado por el propio postulante manda sobre el que registró GTH.
                contexto.CandidatoNombre = Trim(f.NombresCompletos) ?? head?.Nombre ?? string.Empty;
                contexto.Motivo = f.MotivoRechazo;
            }

            return contexto;
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
