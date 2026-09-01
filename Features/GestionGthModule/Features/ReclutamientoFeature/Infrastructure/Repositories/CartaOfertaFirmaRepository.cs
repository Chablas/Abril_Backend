using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    /// <inheritdoc cref="ICartaOfertaFirmaRepository"/>
    public class CartaOfertaFirmaRepository : ICartaOfertaFirmaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CartaOfertaFirmaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Los timestamps se guardan en UTC y se sirven al frontend en hora de Perú.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        /// <summary>
        /// Mensaje único para "el token no sirve". Es a propósito el mismo para un token inexistente,
        /// uno de una carta dada de baja y uno sin archivo cargado: desde afuera no se puede
        /// distinguir un token inventado de uno que existió, y así probar tokens no informa nada.
        /// </summary>
        private const string TokenInvalido =
            "El enlace no es válido o ya no está disponible. Escríbele a Gestión de Talento Humano para que te envíe uno nuevo.";

        public async Task<CartaOfertaFirmaPublicoDto> GetPublicoByToken(string token)
        {
            using var ctx = _factory.CreateDbContext();

            var fila = await (
                from ca in ctx.GthCartaOferta
                where ca.State && ca.Token == token
                join c in ctx.GthCandidato on ca.GthCandidatoId equals c.GthCandidatoId
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                join s in ctx.GthSolicitud on r.GthSolicitudId equals s.GthSolicitudId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                    // Razón social, ficha maestra y jefe directo son todos opcionales en la vacante.
                join co in ctx.Contributor on r.ContributorId equals (int?)co.ContributorId into coJoin
                from co in coJoin.DefaultIfEmpty()
                join pe in ctx.Person on ca.PersonId equals pe.PersonId into peJoin
                from pe in peJoin.DefaultIfEmpty()
                join w in ctx.Worker on s.SolicitanteWorkerId equals (int?)w.Id into wJoin
                from w in wJoin.DefaultIfEmpty()
                select new
                {
                    ca.CartaNombre,
                    ca.CartaUrl,
                    ca.FechaIngreso,
                    ca.FirmadaUrl,
                    ca.FirmadaPostulanteDateTime,
                    ca.FirmadaSubidaDateTime,
                    ca.AprobadaDateTime,
                    ca.FinalizadaDateTime,
                    ca.PrimeraAperturaDateTime,
                    CandidatoNombre = c.Nombre,
                    PersonNombre    = pe == null ? null : pe.FullName,
                    // La firma vive en la ficha de la base maestra: son las mismas columnas que usa la
                    // firma del Gerente General en Contabilidad.
                    FirmaBytes      = pe == null ? null : pe.SignatureImageBytes,
                    FirmaMime       = pe == null ? null : pe.SignatureMime,
                    FirmaFecha      = pe == null ? null : pe.SignatureUpdatedDateTime,
                    Puesto          = p.Nombre,
                    Area            = s.AreaNombre,
                    Empresa         = co == null ? null : co.ContributorName,
                    ProyectoObra    = pr.ProjectDescription,
                    JefeDirecto     = w == null ? null : (w.Person != null ? w.Person.FullName : w.ApellidoNombre),
                })
                .FirstOrDefaultAsync()
                ?? throw new AbrilException(TokenInvalido, 404);

            // Sin carta cargada no hay nada que mostrar ni firmar: la página no tendría contenido.
            if (string.IsNullOrWhiteSpace(fila.CartaUrl))
                throw new AbrilException(TokenInvalido, 404);

            return new CartaOfertaFirmaPublicoDto
            {
                Nombre       = string.IsNullOrWhiteSpace(fila.PersonNombre)
                    ? fila.CandidatoNombre : fila.PersonNombre!,
                Puesto       = fila.Puesto,
                Area         = fila.Area,
                Empresa      = fila.Empresa,
                ProyectoObra = fila.ProyectoObra,
                JefeDirecto  = fila.JefeDirecto,
                FechaIngreso = fila.FechaIngreso,
                CartaNombre  = fila.CartaNombre,

                FirmaDataUrl = fila.FirmaBytes == null
                    ? null
                    : $"data:{fila.FirmaMime ?? "image/png"};base64,{Convert.ToBase64String(fila.FirmaBytes)}",
                FirmaActualizadaEn = fila.FirmaFecha?.ToOffset(PeruOffset).DateTime,

                YaFirmada = !string.IsNullOrWhiteSpace(fila.FirmadaUrl),
                // La fecha de firma del postulante manda; si la carta la subió GTH a mano, se muestra
                // la de esa subida, que es cuando el documento firmado entró al expediente.
                FirmadaEn = (fila.FirmadaPostulanteDateTime ?? fila.FirmadaSubidaDateTime)
                    ?.ToOffset(PeruOffset).DateTime,
                Aprobada  = fila.AprobadaDateTime != null,

                Finalizada   = fila.FinalizadaDateTime != null,
                FinalizadaEn = fila.FinalizadaDateTime?.ToOffset(PeruOffset).DateTime,

                PrimeraAperturaEn = fila.PrimeraAperturaDateTime?.ToOffset(PeruOffset).DateTime,
            };
        }

        /// <summary>
        /// Sella la PRIMERA apertura del enlace y, si el servicio rehizo el documento con esa fecha,
        /// guarda también dónde quedaron el .docx y el PDF nuevos. Devuelve la fecha que quedó
        /// registrada (la de antes si otra pestaña llegó primero).
        ///
        /// Va todo en una sola escritura a propósito: la fecha de conformidad es la que imprime el
        /// documento, así que sellarla sin haber podido rehacerlo dejaría la carta diciendo una fecha
        /// que no está en ninguna parte —y sin forma de reintentarlo, porque el sello es de una sola
        /// vez—. Si la conversión falla, el servicio no llama acá y la próxima apertura lo reintenta.
        ///
        /// La guarda del sello (<c>PrimeraAperturaDateTime != null</c>) se repite acá y no se confía
        /// solo en la del servicio: dos pestañas abiertas a la vez llegan las dos.
        /// </summary>
        public async Task<DateTimeOffset?> GuardarConformidad(
            string token,
            DateTimeOffset fecha,
            FileDigitalDocumentoDto? generada,
            FileDigitalDocumentoDto? carta)
        {
            using var ctx = _factory.CreateDbContext();

            var fila = await ctx.GthCartaOferta
                .FirstOrDefaultAsync(c => c.State && c.Token == token);
            if (fila == null) return null;

            if (fila.PrimeraAperturaDateTime != null) return fila.PrimeraAperturaDateTime;

            fila.PrimeraAperturaDateTime = fecha;

            // El .docx se sube con el mismo nombre estable, así que normalmente vuelve con el mismo
            // itemId; se reasigna igual por si SharePoint tuvo que renombrarlo.
            if (generada != null)
            {
                fila.GeneradaNombre  = generada.Nombre;
                fila.GeneradaUrl     = generada.Url;
                fila.GeneradaItemId  = generada.ItemId;
                fila.GeneradaDriveId = generada.DriveId;
            }

            // El PDF sí es un archivo nuevo: es el que el colaborador lee y el que se firma.
            if (carta != null)
            {
                fila.CartaNombre  = carta.Nombre;
                fila.CartaUrl     = carta.Url;
                fila.CartaItemId  = carta.ItemId;
                fila.CartaDriveId = carta.DriveId;
            }

            fila.UpdatedDateTime = DateTimeOffset.UtcNow;
            // Sin updated_user_id: del otro lado no hay un usuario del sistema.

            await ctx.SaveChangesAsync();
            return fecha;
        }

        /// <summary>
        /// Registra el cierre del trámite por parte del colaborador. Devuelve null si ya estaba
        /// finalizada, que es como el servicio sabe que no tiene que volver a avisarle al
        /// solicitante: recargar la página de confirmación no es un cierre nuevo.
        /// </summary>
        public async Task<DateTime?> MarcarFinalizada(int cartaOfertaId)
        {
            using var ctx = _factory.CreateDbContext();

            var carta = await ctx.GthCartaOferta
                .FirstOrDefaultAsync(c => c.GthCartaOfertaId == cartaOfertaId && c.State)
                ?? throw new AbrilException(TokenInvalido, 404);

            if (carta.FinalizadaDateTime != null) return null;

            var now = DateTimeOffset.UtcNow;
            carta.FinalizadaDateTime = now;
            carta.UpdatedDateTime    = now;

            await ctx.SaveChangesAsync();
            return now.ToOffset(PeruOffset).DateTime;
        }

        public async Task<CartaOfertaFirmaContextoDto> PrepararPorToken(string token)
        {
            using var ctx = _factory.CreateDbContext();

            var fila = await (
                from ca in ctx.GthCartaOferta
                where ca.State && ca.Token == token
                join c in ctx.GthCandidato on ca.GthCandidatoId equals c.GthCandidatoId
                join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                    // De acá para abajo, todo es para el aviso a GTH de que la carta quedó firmada:
                    // los mismos joins que ya hace GetPublicoByToken, sobre la misma fila.
                join s in ctx.GthSolicitud on r.GthSolicitudId equals s.GthSolicitudId
                join p in ctx.Puesto on r.PuestoId equals p.PuestoId
                join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                    // Razón social, ficha maestra y jefe directo son todos opcionales en la vacante.
                join co in ctx.Contributor on r.ContributorId equals (int?)co.ContributorId into coJoin
                from co in coJoin.DefaultIfEmpty()
                join pe in ctx.Person on ca.PersonId equals pe.PersonId into peJoin
                from pe in peJoin.DefaultIfEmpty()
                join w in ctx.Worker on s.SolicitanteWorkerId equals (int?)w.Id into wJoin
                from w in wJoin.DefaultIfEmpty()
                    // Solicitante de la vacante: es el destinatario del aviso de «carta finalizada».
                    // Left join por lo mismo que en el resto del módulo — una solicitud vieja puede
                    // haber quedado sin usuario solicitante.
                join us in ctx.User on s.SolicitanteUserId equals (int?)us.UserId into usJoin
                from us in usJoin.DefaultIfEmpty()
                join pso in ctx.Person on s.SolicitanteUserId equals pso.UserId into psoJoin
                from pso in psoJoin.DefaultIfEmpty()
                select new
                {
                    Ca = ca,
                    RequerimientoId = r.GthRequerimientoId,
                    r.Codigo,
                    SolicitanteEmail  = us == null ? null : us.Email,
                    SolicitanteNombre = pso == null ? null : pso.FullName,
                    CandidatoNombre = c.Nombre,
                    PersonNombre    = pe == null ? null : pe.FullName,
                    Puesto          = p.Nombre,
                    Area            = s.AreaNombre,
                    Empresa         = co == null ? null : co.ContributorName,
                    ProyectoObra    = pr.ProjectDescription,
                    JefeDirecto     = w == null ? null : (w.Person != null ? w.Person.FullName : w.ApellidoNombre),
                    // El nombre con el que se armó el file digital: el que declaró el postulante en su
                    // formulario y, si no hay formulario, el que registró GTH. NO el de la base
                    // maestra, que puede haber cambiado después del envío y llevaría el documento
                    // firmado a una carpeta distinta de la que tiene la carta original.
                    FormularioNombre = ctx.GthPostulanteFormulario
                        .Where(f => f.State && f.GthCandidatoId == c.GthCandidatoId)
                        .Select(f => f.NombresCompletos)
                        .FirstOrDefault(),
                    Dni = ctx.Person
                        .Where(x => x.PersonId == ca.PersonId)
                        .Select(x => x.DocumentIdentityCode)
                        .FirstOrDefault()
                        ?? ctx.GthPostulanteFormulario
                            .Where(f => f.State && f.GthCandidatoId == c.GthCandidatoId)
                            .Select(f => f.NumeroDocumento)
                            .FirstOrDefault(),
                })
                .FirstOrDefaultAsync()
                ?? throw new AbrilException(TokenInvalido, 404);

            var carta = fila.Ca;

            if (string.IsNullOrWhiteSpace(carta.CartaUrl))
                throw new AbrilException(TokenInvalido, 404);

            return new CartaOfertaFirmaContextoDto
            {
                CartaOfertaId   = carta.GthCartaOfertaId,
                RequerimientoId = fila.RequerimientoId,
                PersonId        = carta.PersonId,
                Codigo          = fila.Codigo,
                Nombre          = string.IsNullOrWhiteSpace(fila.FormularioNombre)
                    ? fila.CandidatoNombre : fila.FormularioNombre!,
                Dni             = fila.Dni ?? string.Empty,

                // Mismo criterio que el detalle de GTH: manda el nombre de la base maestra y el del
                // candidato es el respaldo mientras no tenga ficha.
                NombreColaborador = string.IsNullOrWhiteSpace(fila.PersonNombre)
                    ? fila.CandidatoNombre : fila.PersonNombre!,
                Puesto       = fila.Puesto,
                Area         = fila.Area,
                Empresa      = fila.Empresa,
                ProyectoObra = fila.ProyectoObra,
                JefeDirecto  = fila.JefeDirecto,
                FechaIngreso = carta.FechaIngreso,
                Correo       = carta.Correo,

                SolicitanteEmail  = fila.SolicitanteEmail,
                SolicitanteNombre = fila.SolicitanteNombre,

                CartaNombre  = carta.CartaNombre,
                CartaUrl     = carta.CartaUrl,
                CartaDriveId = carta.CartaDriveId,
                CartaItemId  = carta.CartaItemId,

                GeneradaDriveId = carta.GeneradaDriveId,
                GeneradaItemId  = carta.GeneradaItemId,
                GeneradaNombre  = carta.GeneradaNombre,
                PrimeraApertura = carta.PrimeraAperturaDateTime,
                FirmadaNombre  = carta.FirmadaNombre,
                FirmadaUrl     = carta.FirmadaUrl,
                FirmadaDriveId = carta.FirmadaDriveId,
                FirmadaItemId  = carta.FirmadaItemId,
                Carpeta        = string.IsNullOrWhiteSpace(carta.FileDigitalDriveId)
                              || string.IsNullOrWhiteSpace(carta.FileDigitalItemId)
                    ? null
                    : new FileDigitalCarpetaDto
                    {
                        DriveId = carta.FileDigitalDriveId!,
                        ItemId  = carta.FileDigitalItemId!,
                        Ruta    = carta.FileDigitalRuta,
                    },
                YaFirmada  = !string.IsNullOrWhiteSpace(carta.FirmadaUrl),
                Aprobada   = carta.AprobadaDateTime != null,
                Finalizada = carta.FinalizadaDateTime != null,
            };
        }

        public async Task<CartaOfertaFirmaGuardarResultDto> GuardarFirma(int personId, byte[] imageBytes, string mime)
        {
            using var ctx = _factory.CreateDbContext();

            var person = await ctx.Person.FirstOrDefaultAsync(x => x.PersonId == personId)
                ?? throw new AbrilException(
                    "No encontramos tu ficha en nuestros registros. Escríbele a Gestión de Talento Humano.", 404);

            var now = DateTimeOffset.UtcNow;
            person.SignatureImageBytes      = imageBytes;
            person.SignatureMime            = mime;
            person.SignatureUpdatedDateTime = now;

            await ctx.SaveChangesAsync();

            return new CartaOfertaFirmaGuardarResultDto
            {
                Message            = "Firma registrada.",
                FirmaDataUrl       = $"data:{mime};base64,{Convert.ToBase64String(imageBytes)}",
                FirmaActualizadaEn = now.ToOffset(PeruOffset).DateTime,
            };
        }

        public async Task<(byte[] Bytes, string Mime)?> GetFirmaBytes(int personId)
        {
            using var ctx = _factory.CreateDbContext();

            var p = await ctx.Person
                .Where(x => x.PersonId == personId && x.SignatureImageBytes != null)
                .Select(x => new { x.SignatureImageBytes, x.SignatureMime })
                .FirstOrDefaultAsync();

            return p == null ? null : (p.SignatureImageBytes!, p.SignatureMime ?? "image/png");
        }

        public async Task<DateTime> GuardarFirmadaPorPostulante(
            int cartaOfertaId, FileDigitalDocumentoDto documento, FileDigitalCarpetaDto? carpeta)
        {
            using var ctx = _factory.CreateDbContext();

            var carta = await ctx.GthCartaOferta
                .FirstOrDefaultAsync(c => c.GthCartaOfertaId == cartaOfertaId && c.State)
                ?? throw new AbrilException(TokenInvalido, 404);

            var now = DateTimeOffset.UtcNow;

            carta.FirmadaNombre  = documento.Nombre;
            carta.FirmadaUrl     = documento.Url;
            carta.FirmadaItemId  = documento.ItemId;
            carta.FirmadaDriveId = documento.DriveId;
            carta.FirmadaSubidaDateTime = now;
            // Queda en null a propósito: el que firmó es el postulante, que no es un usuario del
            // sistema. La fecha de abajo es la que dice que la firma vino del enlace y no de GTH.
            carta.FirmadaSubidaUserId       = null;
            carta.FirmadaPostulanteDateTime = now;

            // El documento firmado no se aprueba solo: reemplazar el archivo anula cualquier
            // aprobación anterior, igual que cuando lo reemplaza GTH.
            carta.AprobadaDateTime = null;
            carta.AprobadaUserId   = null;

            // Cartas anteriores a que se persistiera el file digital: se completa con la carpeta que
            // acaba de resolver el servicio.
            if (carpeta != null && string.IsNullOrWhiteSpace(carta.FileDigitalItemId))
            {
                carta.FileDigitalDriveId = carpeta.DriveId;
                carta.FileDigitalItemId  = carpeta.ItemId;
                carta.FileDigitalRuta    = carpeta.Ruta;
            }

            carta.UpdatedDateTime = now;
            // Sin updated_user_id: no hay usuario del sistema detrás de esta escritura.

            // La firma mueve el requerimiento: es lo que le pone la revisión en la bandeja a GTH.
            // Va en el mismo SaveChanges que la carta para que no pueda quedar uno sin el otro.
            var estado = await ctx.GthEstadoRequerimiento
                .Where(e => e.State && e.Codigo == EstadoReclutamiento.CartaOfertaFirmada)
                .Select(e => (int?)e.GthEstadoRequerimientoId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException(
                    $"No está configurado el estado {EstadoReclutamiento.CartaOfertaFirmada} de reclutamiento.", 500);

            var req = await (from c in ctx.GthCandidato
                             where c.GthCandidatoId == carta.GthCandidatoId
                             join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
                             where r.State
                             select r).FirstOrDefaultAsync();
            if (req != null)
            {
                req.GthEstadoRequerimientoId = estado;
                req.UpdatedDateTime          = now;
            }

            await ctx.SaveChangesAsync();

            return now.ToOffset(PeruOffset).DateTime;
        }
    }
}
