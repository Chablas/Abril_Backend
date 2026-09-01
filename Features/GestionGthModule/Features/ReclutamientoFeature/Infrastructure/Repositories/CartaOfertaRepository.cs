using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    /// <inheritdoc cref="ICartaOfertaRepository"/>
    public class CartaOfertaRepository : ICartaOfertaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CartaOfertaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Los timestamps se guardan en UTC y se sirven al frontend en hora de Perú.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        /// <summary>
        /// Fila cruda del seleccionado de un requerimiento con todo lo que hace falta para la carta
        /// oferta: su ficha de la base maestra (de donde salen el correo y el documento), los datos
        /// de la vacante que viajan en el correo, la fase del requerimiento y la carta ya enviada si
        /// existe. Es una clase con nombre porque la comparten la lectura del detalle y las tres
        /// validaciones de escritura, que necesitan exactamente lo mismo.
        /// </summary>
        private sealed class CartaOfertaRaw
        {
            public int RequerimientoId { get; set; }
            public int CandidatoId { get; set; }
            public string Codigo { get; set; } = string.Empty;
            public string CandidatoNombre { get; set; } = string.Empty;

            /// <summary>
            /// Nombre que declaró el propio postulante en su formulario. Es el que nombra la carpeta
            /// del file digital: NO el de la base maestra, que puede cambiar después del envío y
            /// llevaría la carta firmada a una carpeta distinta de la que tiene el original.
            /// </summary>
            public string? FormularioNombre { get; set; }

            public string? PersonNombre { get; set; }

            /// <summary>
            /// Ficha resuelta por el enlace de su flujo (formulario aprobado o ingreso directo).
            /// Null cuando ese enlace no existe todavía, aunque la ficha sí pueda estar en la base
            /// maestra por coincidencia de documento — para eso está <see cref="TieneFicha"/>.
            /// </summary>
            public int? PersonId { get; set; }

            /// <summary>
            /// true si el candidato tiene ficha en <c>person</c>, por el enlace de su flujo o por
            /// coincidencia de documento. Es lo que decide si se le puede mandar la carta: la firma
            /// que dibuja en el enlace se guarda en esa ficha.
            /// </summary>
            public bool TieneFicha { get; set; }

            public string? Correo { get; set; }
            public string? Dni { get; set; }
            public string? Puesto { get; set; }
            public string? Area { get; set; }
            public string? Empresa { get; set; }
            public string? ProyectoObra { get; set; }
            public string? JefeDirecto { get; set; }
            public string EstadoCodigo { get; set; } = string.Empty;
            public GthCartaOferta? Carta { get; set; }

            // ── Solo para la carta GENERADA desde la plantilla ─────────────────
            // Viajan en la misma consulta y no en una aparte porque la generación necesita
            // exactamente los mismos joins que el resto (candidato → requerimiento → puesto) más
            // dos saltos, y separarlas sería un roundtrip de más para repetir lo mismo.

            /// <summary>
            /// Área a la que ENTRA el contratado: la de DESTINO del puesto pedido
            /// (<c>puesto.area_destino_scope_id</c>), no la del solicitante — la Gerencia
            /// Inmobiliaria pide un INGENIERO RESIDENTE y el residente entra a Residencia. Es de
            /// donde sale la jefatura que imprime la carta. Null cuando el puesto no tiene destino;
            /// ahí manda <see cref="Area"/>, la del solicitante.
            /// </summary>
            public string? AreaDestino { get; set; }

            /// <summary>
            /// Razón social de la ficha del trabajador (<c>workers.contributor_id</c>): es con la
            /// empresa de SU ficha con la que firma, que puede no ser la del requerimiento si ya
            /// tenía ficha de antes. Null si su ficha todavía no la tiene; ahí manda
            /// <see cref="Empresa"/>, la del requerimiento.
            /// </summary>
            public string? RazonSocialFicha { get; set; }
        }

        /// <summary>
        /// El seleccionado del requerimiento con su ficha maestra, su vacante y su carta oferta, en
        /// una sola consulta. Devuelve null cuando el requerimiento todavía no tiene seleccionado,
        /// que es lo que usa el detalle como bandera para no dibujar la sección.
        ///
        /// El enlace a la ficha de <c>person</c> sale de dos columnas distintas según el flujo — el
        /// <c>person_id</c> del formulario aprobado (flujo normal) o el <c>fft_person_id</c> del
        /// requerimiento (ingreso directo, que no pide formulario) — y, sin ninguno de los dos, de la
        /// coincidencia por documento. Es el mismo criterio con el que se resuelve el candidato en
        /// Onboarding: mirar solo el formulario dejaba a todo ingreso directo sin correo y sin ficha.
        /// </summary>
        private static Task<CartaOfertaRaw?> QuerySeleccionado(AppDbContext ctx, int requerimientoId) =>
            (from c in ctx.GthCandidato
             where c.GthRequerimientoId == requerimientoId && c.State
             join ev in ctx.GthCandidatoEvaluacion.Where(x => x.State)
                 on c.GthCandidatoId equals ev.GthCandidatoId
             join res in ctx.GthCandidatoResultado on ev.GthCandidatoResultadoId equals res.GthCandidatoResultadoId
             where res.Codigo == ResultadoCandidato.Seleccionado
             join r in ctx.GthRequerimiento on c.GthRequerimientoId equals r.GthRequerimientoId
             join e in ctx.GthEstadoRequerimiento on r.GthEstadoRequerimientoId equals e.GthEstadoRequerimientoId
             join s in ctx.GthSolicitud on r.GthSolicitudId equals s.GthSolicitudId
             join p in ctx.Puesto on r.PuestoId equals p.PuestoId
             join pr in ctx.Project on r.ProjectId equals pr.ProjectId
                 // Razón social, formulario, jefe directo y carta oferta son todos opcionales.
             join co in ctx.Contributor on r.ContributorId equals (int?)co.ContributorId into coJoin
             from co in coJoin.DefaultIfEmpty()
             join fo in ctx.GthPostulanteFormulario.Where(x => x.State)
                 on c.GthCandidatoId equals fo.GthCandidatoId into foJoin
             from fo in foJoin.DefaultIfEmpty()
             join w in ctx.Worker on s.SolicitanteWorkerId equals (int?)w.Id into wJoin
             from w in wJoin.DefaultIfEmpty()
             join ca in ctx.GthCartaOferta.Where(x => x.State)
                 on c.GthCandidatoId equals ca.GthCandidatoId into caJoin
             from ca in caJoin.DefaultIfEmpty()
                 // Con carta ya enviada la ficha es la que quedó registrada en ella; antes del envío
                 // hay que resolverla por el flujo del candidato.
             let personId = ca != null ? (int?)ca.PersonId : (fo.PersonId ?? r.FftPersonId)
             let documento = fo.NumeroDocumento ?? r.FftCandidatoDocumento
                 // Va como bandera y no dentro del personId para no meter una subconsulta adentro de
                 // un COALESCE: cada campo la resuelve con este mismo predicado y las dos ramas son
                 // excluyentes (el documento es único en person, así que sale una sola fila).
             let porDocumento = personId == null && documento != null
             select new CartaOfertaRaw
             {
                 RequerimientoId  = r.GthRequerimientoId,
                 CandidatoId      = c.GthCandidatoId,
                 Codigo           = r.Codigo,
                 CandidatoNombre  = c.Nombre,
                 FormularioNombre = fo != null ? fo.NombresCompletos : null,
                 PersonId         = personId,
                 PersonNombre = ctx.Person
                     .Where(x => x.PersonId == personId || (porDocumento && x.DocumentIdentityCode == documento))
                     .Select(x => x.FullName)
                     .FirstOrDefault(),
                 Correo = ctx.Person
                     .Where(x => x.PersonId == personId || (porDocumento && x.DocumentIdentityCode == documento))
                     .Select(x => x.Email)
                     .FirstOrDefault(),
                 Dni = ctx.Person
                     .Where(x => x.PersonId == personId || (porDocumento && x.DocumentIdentityCode == documento))
                     .Select(x => x.DocumentIdentityCode)
                     .FirstOrDefault()
                     ?? documento,
                 // Mismo predicado que el correo y el documento, para que los tres no puedan
                 // discrepar. Va como un solo Any con el OR adentro —y no como dos unidos por ||—
                 // para que sea un único EXISTS en la consulta.
                 TieneFicha = ctx.Person.Any(x =>
                     x.PersonId == personId || (porDocumento && x.DocumentIdentityCode == documento)),
                 Puesto       = p.Nombre,
                 Area         = s.AreaNombre,
                 Empresa      = co == null ? null : co.ContributorName,
                 ProyectoObra = pr.ProjectDescription,
                 JefeDirecto  = w == null ? null : (w.Person != null ? w.Person.FullName : w.ApellidoNombre),
                 EstadoCodigo = e.Codigo,
                 Carta        = ca,

                 // Área DESTINO del puesto: la del árbol, no el texto plano de la solicitud.
                 AreaDestino = ctx.AreaScope
                     .Where(sc => sc.AreaScopeId == p.AreaDestinoScopeId)
                     .Select(sc => sc.AreaItem!.AreaItemName)
                     .FirstOrDefault(),

                 // Razón social de la ficha del seleccionado. Se ordena igual que al abrirla
                 // (ReclutamientoRepository.ResolverFichaFinalistaAsync): primero la de
                 // pre-ingreso y, si no la hay, la más reciente — una persona puede tener
                 // varias fichas por reingreso y la vigente es la que manda.
                 RazonSocialFicha = ctx.Worker
                     .Where(x => x.PersonId == personId && x.ContributorId != null)
                     .OrderByDescending(x => x.WorkersEstadoId == WorkersEstadoIds.FinalistaAprobado ? 1 : 0)
                     .ThenByDescending(x => x.Id)
                     .Select(x => x.Contributor!.ContributorName)
                     .FirstOrDefault(),
             }).FirstOrDefaultAsync();

        /// <summary>Proyecta la fila cruda a lo que ve el detalle de GTH.</summary>
        private static CartaOfertaRequerimientoDto MapCarta(CartaOfertaRaw x)
        {
            var dto = new CartaOfertaRequerimientoDto
            {
                Nombre = string.IsNullOrWhiteSpace(x.PersonNombre) ? x.CandidatoNombre : x.PersonNombre!,
                // El correo vigente es el de la base maestra; el de la carta es el histórico del envío.
                CorreoSugerido    = x.Correo,
                Dni               = x.Dni,
                TieneFichaMaestra = x.TieneFicha,
            };

            var carta = x.Carta;
            if (carta == null) return dto;

            dto.CartaOfertaId         = carta.GthCartaOfertaId;
            dto.FechaIngreso          = carta.FechaIngreso;
            dto.Sueldo                = carta.Sueldo;
            dto.FechaLimiteAceptacion = carta.FechaLimiteAceptacion;
            dto.GeneradaNombre        = carta.GeneradaNombre;
            dto.GeneradaUrl           = carta.GeneradaUrl;
            dto.GeneradaEn            = carta.GeneradaDateTime?.ToOffset(PeruOffset).DateTime;
            dto.CartaNombre         = carta.CartaNombre;
            dto.CartaUrl            = carta.CartaUrl;
            dto.Correo              = carta.Correo;
            dto.EnviadaEn           = carta.EnviadaDateTime?.ToOffset(PeruOffset).DateTime;
            dto.FirmadaNombre       = carta.FirmadaNombre;
            dto.FirmadaUrl          = carta.FirmadaUrl;
            dto.FirmadaSubidaEn     = carta.FirmadaSubidaDateTime?.ToOffset(PeruOffset).DateTime;
            dto.FirmadaPostulanteEn = carta.FirmadaPostulanteDateTime?.ToOffset(PeruOffset).DateTime;
            dto.AprobadaEn          = carta.AprobadaDateTime?.ToOffset(PeruOffset).DateTime;
            dto.FileDigitalCarpeta  = carta.FileDigitalRuta;
            return dto;
        }

        public async Task<CartaOfertaRequerimientoDto?> GetPorRequerimiento(int requerimientoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await Leer(ctx, requerimientoId);
        }

        /// <summary>
        /// La misma lectura, sobre un contexto que el llamador ya tiene abierto. La usa
        /// <c>ReclutamientoRepository.GetDetalleGth</c>, que arma el modal entero: la carta es una
        /// sección más de ese detalle, así que pedirla con su propia conexión sería un roundtrip de
        /// más por cada vez que GTH abre un requerimiento.
        /// </summary>
        internal static async Task<CartaOfertaRequerimientoDto?> Leer(AppDbContext ctx, int requerimientoId)
        {
            var raw = await QuerySeleccionado(ctx, requerimientoId);
            return raw == null ? null : MapCarta(raw);
        }

        // ── Envío de la carta oferta ───────────────────────────────────────────

        public async Task<CartaOfertaContextoDto> PrepararEnvio(
            int requerimientoId, DateOnly? fechaIngreso, string? correo, string token)
        {
            using var ctx = _factory.CreateDbContext();

            var raw = await QuerySeleccionado(ctx, requerimientoId)
                ?? throw new AbrilException(
                    "Este requerimiento todavía no tiene un candidato seleccionado al que ofrecerle el puesto.", 409);

            // La carta oferta es la salida de las dos fases de "el examen salió bien" y de ninguna
            // otra: desde EMO_OBSERVADO no hay aptitud todavía, desde EMO_NO_APTO el candidato quedó
            // fuera y desde una fase anterior sería saltarse el examen.
            if (!EstadoReclutamiento.EmoAptas.Contains(raw.EstadoCodigo))
                throw new AbrilException(
                    "Solo se puede enviar la carta oferta cuando el EMO de ingreso del seleccionado "
                    + "salió Apto o Apto con Restricciones.", 409);

            // Solo bloquea la que YA SE ENVIÓ. Una fila sin envío es el borrador que dejó la
            // generación del documento: ese es justamente el que se viene a mandar.
            if (raw.Carta?.EnviadaDateTime != null)
                throw new AbrilException(
                    "Este candidato ya tiene una carta oferta enviada. Reenvíale el enlace desde el detalle.", 409);

            var destino = Trim(correo) ?? Trim(raw.Correo);
            if (string.IsNullOrWhiteSpace(destino))
                throw new AbrilException(
                    "El candidato no tiene correo personal en su ficha de la base maestra. Regístralo ahí o indica el correo a mano.",
                    409);

            // Sin documento no hay carpeta: el file del colaborador se llama «{DNI} - {NOMBRE}» y de
            // ese nombre dependen tanto encontrar su file como los permisos que se le dan encima.
            var dni = Trim(raw.Dni);
            if (string.IsNullOrWhiteSpace(dni))
                throw new AbrilException(
                    "El candidato no tiene documento de identidad en su ficha de la base maestra.", 409);

            // La ficha es obligatoria: ahí se guarda la firma que el candidato dibuja en el enlace.
            // Mismo orden de búsqueda que el correo y el documento: el enlace que dejó su flujo —el
            // formulario aprobado o el ingreso directo— y, si no hay ninguno, la coincidencia por
            // documento contra la base maestra.
            var personId = raw.PersonId
                ?? await ctx.Person
                    .Where(x => x.DocumentIdentityCode == dni)
                    .Select(x => (int?)x.PersonId)
                    .FirstOrDefaultAsync();

            if (personId == null)
                throw new AbrilException(
                    "El candidato no tiene ficha en la base maestra y su firma se guarda ahí.", 409);

            return new CartaOfertaContextoDto
            {
                RequerimientoId = raw.RequerimientoId,
                CandidatoId     = raw.CandidatoId,
                PersonId        = personId.Value,
                Codigo          = raw.Codigo,
                Nombre          = NombreFile(raw),
                Dni             = dni,
                Token           = token,
                Puesto          = raw.Puesto,
                Area            = raw.Area,
                Empresa         = raw.Empresa,
                ProyectoObra    = raw.ProyectoObra,
                Correo          = destino.ToLowerInvariant(),
                // Enviar sin fecha no puede borrar la que quedó pactada al generar el documento:
                // la carta ya la imprimió y el onboarding la hereda de acá.
                FechaIngreso    = fechaIngreso ?? raw.Carta?.FechaIngreso,
                JefeDirecto     = raw.JefeDirecto,
                Carpeta         = raw.Carta == null ? null : Carpeta(raw.Carta),
                GeneradaDriveId = raw.Carta?.GeneradaDriveId,
                GeneradaItemId  = raw.Carta?.GeneradaItemId,
                GeneradaNombre  = raw.Carta?.GeneradaNombre,
            };
        }

        /// <summary>
        /// Deja registrado el envío: completa el borrador si la carta se generó acá, o crea la fila
        /// entera si se adjuntó ya armada sin pasar por la generación. En los dos casos es lo que
        /// mueve el requerimiento a CARTA_OFERTA.
        /// </summary>
        public async Task<CartaOfertaAccionResultDto> Crear(
            CartaOfertaContextoDto contexto,
            FileDigitalDocumentoDto carta,
            FileDigitalCarpetaDto carpeta,
            int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var now = DateTimeOffset.UtcNow;

            var fila = await BuscarCarta(ctx, contexto.RequerimientoId);
            if (fila == null)
            {
                fila = new GthCartaOferta
                {
                    GthCandidatoId  = contexto.CandidatoId,
                    PersonId        = contexto.PersonId,
                    CreatedDateTime = now,
                    CreatedUserId   = userId,
                };
                ctx.GthCartaOferta.Add(fila);
            }
            else
            {
                // Carrera contra otra pestaña que ya mandó esta misma carta: se relee acá y no solo
                // en PrepararEnvio porque entre la validación y este punto pasaron la subida a
                // SharePoint y la resolución de la carpeta.
                if (fila.EnviadaDateTime != null)
                    throw new AbrilException(
                        "Este candidato ya tiene una carta oferta enviada. Reenvíale el enlace desde el detalle.", 409);

                fila.UpdatedDateTime = now;
                fila.UpdatedUserId   = userId;
            }

            fila.FechaIngreso    = contexto.FechaIngreso;
            fila.CartaNombre     = carta.Nombre;
            fila.CartaUrl        = carta.Url;
            fila.CartaItemId     = carta.ItemId;
            fila.CartaDriveId    = carta.DriveId;
            fila.Correo          = contexto.Correo;
            fila.Token           = contexto.Token;
            fila.EnviadaDateTime = now;
            fila.EnviadaUserId   = userId;

            fila.FileDigitalDriveId = carpeta.DriveId;
            fila.FileDigitalItemId  = carpeta.ItemId;
            fila.FileDigitalRuta    = carpeta.Ruta;

            var estado = await MoverFase(ctx, contexto.RequerimientoId, EstadoReclutamiento.CartaOferta, userId);
            await ctx.SaveChangesAsync();

            return await LeerResultado(ctx, contexto.RequerimientoId, estado);
        }

        // ── Generación del documento desde la plantilla ────────────────────────

        public async Task<CartaOfertaGeneracionContextoDto> PrepararGeneracion(int requerimientoId)
        {
            using var ctx = _factory.CreateDbContext();

            var raw = await QuerySeleccionado(ctx, requerimientoId)
                ?? throw new AbrilException(
                    "Este requerimiento todavía no tiene un candidato seleccionado al que ofrecerle el puesto.", 409);

            // Mismas dos fases que el envío: generar la carta antes de que el EMO diga que la
            // persona puede entrar sería armar una propuesta que todavía no se puede sostener.
            if (!EstadoReclutamiento.EmoAptas.Contains(raw.EstadoCodigo))
                throw new AbrilException(
                    "Solo se puede armar la carta oferta cuando el EMO de ingreso del seleccionado "
                    + "salió Apto o Apto con Restricciones.", 409);

            if (raw.Carta?.EnviadaDateTime != null)
                throw new AbrilException(
                    "La carta oferta de este candidato ya se envió: no se puede volver a generar el documento.", 409);

            var dni = Trim(raw.Dni);
            if (string.IsNullOrWhiteSpace(dni))
                throw new AbrilException(
                    "El candidato no tiene documento de identidad en su ficha de la base maestra.", 409);

            // La ficha se exige desde la generación —y no recién al enviar— porque el documento se
            // guarda en su file digital y porque el nombre que imprime la carta sale de ahí.
            var personId = raw.PersonId
                ?? await ctx.Person
                    .Where(x => x.DocumentIdentityCode == dni)
                    .Select(x => (int?)x.PersonId)
                    .FirstOrDefaultAsync();

            if (personId == null)
                throw new AbrilException(
                    "El candidato no tiene ficha en la base maestra: de ahí sale el nombre de la carta.", 409);

            return new CartaOfertaGeneracionContextoDto
            {
                RequerimientoId  = raw.RequerimientoId,
                CandidatoId      = raw.CandidatoId,
                PersonId         = personId.Value,
                Codigo           = raw.Codigo,
                PostulanteNombre = Trim(raw.PersonNombre) ?? raw.CandidatoNombre,
                Puesto           = raw.Puesto,
                // Sin área de destino manda la del solicitante, que es lo que hubo siempre.
                AreaDestino      = Trim(raw.AreaDestino) ?? Trim(raw.Area),
                // Sin razón social en su ficha manda la del requerimiento que lo trajo.
                RazonSocial      = Trim(raw.RazonSocialFicha) ?? Trim(raw.Empresa),
                Nombre           = NombreFile(raw),
                Dni              = dni,
                Carpeta          = raw.Carta == null ? null : Carpeta(raw.Carta),
            };
        }

        public async Task<CartaOfertaAccionResultDto> GuardarGenerada(
            CartaOfertaGeneracionContextoDto contexto,
            CartaOfertaGenerarDto datos,
            FileDigitalDocumentoDto documento,
            FileDigitalCarpetaDto carpeta,
            int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var now = DateTimeOffset.UtcNow;

            var fila = await BuscarCarta(ctx, contexto.RequerimientoId);
            if (fila == null)
            {
                fila = new GthCartaOferta
                {
                    GthCandidatoId  = contexto.CandidatoId,
                    PersonId        = contexto.PersonId,
                    CreatedDateTime = now,
                    CreatedUserId   = userId,
                };
                ctx.GthCartaOferta.Add(fila);
            }
            else
            {
                if (fila.EnviadaDateTime != null)
                    throw new AbrilException(
                        "La carta oferta de este candidato ya se envió: no se puede volver a generar el documento.", 409);

                fila.UpdatedDateTime = now;
                fila.UpdatedUserId   = userId;
            }

            // Las condiciones se guardan con el documento: son lo que la carta dice, y lo que se
            // vuelve a mostrar en el formulario si hay que regenerarla.
            fila.FechaIngreso          = datos.FechaIngreso;
            fila.Sueldo                = datos.Sueldo;
            fila.FechaLimiteAceptacion = datos.FechaLimiteAceptacion;

            fila.GeneradaNombre   = documento.Nombre;
            fila.GeneradaUrl      = documento.Url;
            fila.GeneradaItemId   = documento.ItemId;
            fila.GeneradaDriveId  = documento.DriveId;
            fila.GeneradaDateTime = now;
            fila.GeneradaUserId   = userId;

            fila.FileDigitalDriveId = carpeta.DriveId;
            fila.FileDigitalItemId  = carpeta.ItemId;
            fila.FileDigitalRuta    = carpeta.Ruta;

            // Generar NO mueve la fase: el requerimiento sigue esperando el envío.
            await ctx.SaveChangesAsync();

            return await LeerResultado(ctx, contexto.RequerimientoId, null);
        }

        // ── Reenvío del enlace ─────────────────────────────────────────────────

        public async Task<CartaOfertaContextoDto> PrepararReenvio(
            int requerimientoId, string? correo, string tokenSiFalta)
        {
            using var ctx = _factory.CreateDbContext();

            var raw = await QuerySeleccionado(ctx, requerimientoId)
                ?? throw new AbrilException("Este requerimiento no tiene un candidato seleccionado.", 409);

            var carta = raw.Carta
                ?? throw new AbrilException(
                    "Este requerimiento todavía no tiene una carta oferta enviada.", 409);

            // El borrador generado todavía no le llegó a nadie: no hay enlace que reenviar.
            if (carta.EnviadaDateTime == null)
                throw new AbrilException(
                    "La carta oferta todavía no se envió: mándala primero desde el detalle.", 409);

            // Ya aprobada: el proceso está cerrado y el enlace abriría una página de solo lectura.
            if (carta.AprobadaDateTime != null)
                throw new AbrilException(
                    "La carta oferta firmada ya fue aprobada: el proceso de reclutamiento está cerrado.", 409);
            if (!string.IsNullOrWhiteSpace(carta.FirmadaUrl))
                throw new AbrilException(
                    "El candidato ya devolvió su carta oferta firmada; no hace falta reenviarle el enlace.", 409);

            var destino = Trim(correo) ?? Trim(raw.Correo) ?? Trim(carta.Correo);
            if (string.IsNullOrWhiteSpace(destino))
                throw new AbrilException(
                    "El candidato no tiene un correo personal registrado. Complétalo en su ficha de la base maestra o indícalo a mano.",
                    409);

            return new CartaOfertaContextoDto
            {
                RequerimientoId = raw.RequerimientoId,
                CandidatoId     = raw.CandidatoId,
                PersonId        = carta.PersonId,
                Codigo          = raw.Codigo,
                Nombre          = NombreFile(raw),
                Dni             = Trim(raw.Dni) ?? string.Empty,
                // El token no se rota: el candidato puede tener el enlace anterior en su bandeja y
                // los dos tienen que seguir funcionando. El de respaldo solo entra si la fila no
                // tiene ninguno (no debería pasar: se genera al enviar).
                Token           = Trim(carta.Token) ?? tokenSiFalta,
                Puesto          = raw.Puesto,
                Area            = raw.Area,
                Empresa         = raw.Empresa,
                ProyectoObra    = raw.ProyectoObra,
                Correo          = destino.ToLowerInvariant(),
                FechaIngreso    = carta.FechaIngreso,
                JefeDirecto     = raw.JefeDirecto,
            };
        }

        public async Task<CartaOfertaAccionResultDto> MarcarEnlaceEnviado(
            int requerimientoId, CartaOfertaContextoDto contexto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var carta = await CartaDelRequerimiento(ctx, requerimientoId);

            var now = DateTimeOffset.UtcNow;
            carta.Token           = contexto.Token;
            carta.Correo          = contexto.Correo;
            carta.EnviadaDateTime = now;
            carta.EnviadaUserId   = userId;
            carta.UpdatedDateTime = now;
            carta.UpdatedUserId   = userId;

            await ctx.SaveChangesAsync();

            return await LeerResultado(ctx, requerimientoId, null);
        }

        // ── Carta firmada (vía de respaldo: la sube GTH) ────────────────────────

        public async Task<CartaOfertaDocumentoContextoDto> PrepararDocumentoFirmado(int requerimientoId)
        {
            using var ctx = _factory.CreateDbContext();

            var raw = await QuerySeleccionado(ctx, requerimientoId)
                ?? throw new AbrilException("Este requerimiento no tiene un candidato seleccionado.", 409);

            var carta = raw.Carta
                ?? throw new AbrilException(
                    "Este requerimiento todavía no tiene una carta oferta enviada.", 409);

            if (carta.EnviadaDateTime == null)
                throw new AbrilException(
                    "La carta oferta todavía no se envió: no puede haber una versión firmada.", 409);

            // Aprobada = proceso cerrado. Reemplazar el documento acá dejaría un requerimiento
            // CERRADO con una carta sin aprobar, así que se corta: si hay que rehacerla, es un
            // proceso nuevo.
            if (carta.AprobadaDateTime != null)
                throw new AbrilException(
                    "La carta oferta firmada ya fue aprobada y el proceso de reclutamiento está cerrado: "
                    + "no se puede reemplazar el documento.", 409);

            return new CartaOfertaDocumentoContextoDto
            {
                CartaOfertaId   = carta.GthCartaOfertaId,
                RequerimientoId = raw.RequerimientoId,
                Codigo          = raw.Codigo,
                Nombre          = NombreFile(raw),
                Dni             = Trim(raw.Dni) ?? string.Empty,
                Carpeta         = Carpeta(carta),
            };
        }

        public async Task<CartaOfertaAccionResultDto> GuardarFirmada(
            int requerimientoId, FileDigitalDocumentoDto documento, FileDigitalCarpetaDto? carpeta, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var carta = await CartaDelRequerimiento(ctx, requerimientoId);

            if (carta.AprobadaDateTime != null)
                throw new AbrilException(
                    "La carta oferta firmada ya fue aprobada y el proceso de reclutamiento está cerrado.", 409);

            var now = DateTimeOffset.UtcNow;
            carta.FirmadaNombre         = documento.Nombre;
            carta.FirmadaUrl            = documento.Url;
            carta.FirmadaItemId         = documento.ItemId;
            carta.FirmadaDriveId        = documento.DriveId;
            carta.FirmadaSubidaDateTime = now;
            carta.FirmadaSubidaUserId   = userId;
            // La firmó GTH en papel, no el candidato desde el enlace: la marca del enlace se limpia
            // para que la pantalla no diga que la firmó él.
            carta.FirmadaPostulanteDateTime = null;
            CompletarCarpeta(carta, carpeta);
            carta.UpdatedDateTime = now;
            carta.UpdatedUserId   = userId;

            var estado = await MoverFase(ctx, requerimientoId, EstadoReclutamiento.CartaOfertaFirmada, userId);
            await ctx.SaveChangesAsync();

            return await LeerResultado(ctx, requerimientoId, estado);
        }

        // ── Aprobación: el cierre del proceso ──────────────────────────────────

        public async Task<CartaOfertaAccionResultDto> Aprobar(int requerimientoId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var carta = await CartaDelRequerimiento(ctx, requerimientoId);

            if (string.IsNullOrWhiteSpace(carta.FirmadaUrl))
                throw new AbrilException(
                    "Todavía no hay carta oferta firmada: espera a que el candidato la firme desde su enlace, o adjúntala a mano.",
                    409);

            EstadoRequerimientoResultDto? estado = null;
            if (carta.AprobadaDateTime == null)
            {
                var now = DateTimeOffset.UtcNow;
                carta.AprobadaDateTime = now;
                carta.AprobadaUserId   = userId;
                carta.UpdatedDateTime  = now;
                carta.UpdatedUserId    = userId;

                // Aprobar la carta ES cerrar el proceso: no hay ningún otro camino a CERRADO.
                estado = await MoverFase(ctx, requerimientoId, EstadoReclutamiento.Cerrado, userId);
                await ctx.SaveChangesAsync();
            }

            return await LeerResultado(ctx, requerimientoId, estado);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// La carta oferta vigente de un requerimiento, cargada para escribir. Va por el candidato
        /// seleccionado, que es la llave real: la carta cuelga del candidato, no del requerimiento.
        /// </summary>
        private static async Task<GthCartaOferta> CartaDelRequerimiento(AppDbContext ctx, int requerimientoId) =>
            await BuscarCarta(ctx, requerimientoId)
            ?? throw new AbrilException("Este requerimiento no tiene una carta oferta registrada.", 404);

        /// <summary>
        /// La misma búsqueda, pero devolviendo null en vez de lanzar. La usan la generación y el
        /// envío, que tienen que poder crear la fila cuando todavía no existe: para ellos «no hay
        /// carta» es el caso normal, no un error.
        /// </summary>
        private static Task<GthCartaOferta?> BuscarCarta(AppDbContext ctx, int requerimientoId) =>
            (from ca in ctx.GthCartaOferta
             where ca.State
             join c in ctx.GthCandidato on ca.GthCandidatoId equals c.GthCandidatoId
             where c.State && c.GthRequerimientoId == requerimientoId
             select ca).FirstOrDefaultAsync();

        /// <summary>
        /// Deja el requerimiento en la fase indicada. No hace SaveChanges: lo llama quien está
        /// escribiendo la carta, para que el cambio de fase y el de la carta viajen en la misma
        /// transacción y no pueda quedar uno sin el otro.
        /// </summary>
        private static async Task<EstadoRequerimientoResultDto> MoverFase(
            AppDbContext ctx, int requerimientoId, string codigoDestino, int? userId)
        {
            var req = await ctx.GthRequerimiento
                .FirstOrDefaultAsync(r => r.GthRequerimientoId == requerimientoId && r.State)
                ?? throw new AbrilException("Requerimiento no encontrado.", 404);

            var destino = await ctx.GthEstadoRequerimiento
                .Where(e => e.State && e.Codigo == codigoDestino)
                .Select(e => new { e.GthEstadoRequerimientoId, e.Codigo, e.Nombre })
                .FirstOrDefaultAsync()
                ?? throw new AbrilException(
                    $"No está configurado el estado {codigoDestino} de reclutamiento.", 500);

            req.GthEstadoRequerimientoId = destino.GthEstadoRequerimientoId;
            req.UpdatedDateTime          = DateTimeOffset.UtcNow;
            req.UpdatedUserId            = userId;

            return new EstadoRequerimientoResultDto
            {
                EstadoCodigo = destino.Codigo,
                EstadoNombre = destino.Nombre,
            };
        }

        /// <summary>
        /// Relee la carta ya escrita y arma la respuesta con la que el modal repinta.
        /// <paramref name="estado"/> viene relleno cuando la acción movió la fase; si no, se lee la
        /// que quedó (el reenvío del enlace no mueve nada).
        /// </summary>
        private static async Task<CartaOfertaAccionResultDto> LeerResultado(
            AppDbContext ctx, int requerimientoId, EstadoRequerimientoResultDto? estado)
        {
            var raw = await QuerySeleccionado(ctx, requerimientoId)
                ?? throw new AbrilException("No se pudo releer la carta oferta actualizada.", 500);

            var estadoNombre = estado?.EstadoNombre;
            if (estadoNombre == null)
                estadoNombre = await ctx.GthEstadoRequerimiento
                    .Where(e => e.Codigo == raw.EstadoCodigo)
                    .Select(e => e.Nombre)
                    .FirstOrDefaultAsync() ?? raw.EstadoCodigo;

            return new CartaOfertaAccionResultDto
            {
                CartaOferta  = MapCarta(raw),
                EstadoCodigo = estado?.EstadoCodigo ?? raw.EstadoCodigo,
                EstadoNombre = estadoNombre,
            };
        }

        /// <summary>
        /// Nombre con el que se arma la carpeta del file digital: el que declaró el postulante en su
        /// formulario y, si no hay formulario (ingreso directo), el que registró GTH. NO el de la
        /// base maestra: ese puede cambiar después del envío y llevaría la carta firmada a una
        /// carpeta distinta de la que tiene el original.
        /// </summary>
        private static string NombreFile(CartaOfertaRaw raw) =>
            string.IsNullOrWhiteSpace(raw.FormularioNombre) ? raw.CandidatoNombre : raw.FormularioNombre!;

        private static FileDigitalCarpetaDto? Carpeta(GthCartaOferta carta) =>
            string.IsNullOrWhiteSpace(carta.FileDigitalDriveId) || string.IsNullOrWhiteSpace(carta.FileDigitalItemId)
                ? null
                : new FileDigitalCarpetaDto
                {
                    DriveId = carta.FileDigitalDriveId!,
                    ItemId  = carta.FileDigitalItemId!,
                    Ruta    = carta.FileDigitalRuta,
                };

        /// <summary>
        /// Completa el file digital de las cartas que no lo tienen persistido con la carpeta que
        /// acaba de resolver el servicio, para que el resto de documentos ya no la vuelva a derivar.
        /// </summary>
        private static void CompletarCarpeta(GthCartaOferta carta, FileDigitalCarpetaDto? carpeta)
        {
            if (carpeta == null || !string.IsNullOrWhiteSpace(carta.FileDigitalItemId)) return;
            carta.FileDigitalDriveId = carpeta.DriveId;
            carta.FileDigitalItemId  = carpeta.ItemId;
            carta.FileDigitalRuta    = carpeta.Ruta;
        }

        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
