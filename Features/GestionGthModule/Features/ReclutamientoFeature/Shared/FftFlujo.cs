using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Shared
{
    /// <summary>
    /// Los dos saltos del flujo <b>FFT</b> (ingreso directo), en un solo lugar porque los disparan
    /// tres repositorios distintos y tienen que hacer exactamente lo mismo en los tres:
    ///
    /// <list type="number">
    ///   <item><description>
    ///     <see cref="AbrirCandidatoAsync"/> — al entrar el requerimiento a manos de GTH (lo
    ///     registra el propio Gerente General, o Gerencia General lo aprueba) se le abre la ficha
    ///     de candidato a la persona que nombró el solicitante, ya APROBADA. Sin esa ficha no hay
    ///     a quién enviarle el formulario, que es el único paso que le queda a GTH.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="CerrarConSeleccionadoAsync"/> — al aprobar GTH el formulario del candidato
    ///     FFT, el proceso se salta entrevistas, multitest, envío de finalistas y decisión del
    ///     solicitante, y queda en EMO de ingreso con el candidato ya marcado como seleccionado y
    ///     su ficha de pre-ingreso abierta.
    ///   </description></item>
    /// </list>
    ///
    /// Ninguno de los dos guarda: dejan los cambios en el <see cref="AppDbContext"/> que se les
    /// pasa para que entren en el mismo <c>SaveChanges</c> que la operación que los disparó. Un
    /// requerimiento FFT sin su candidato (o un formulario aprobado sin su ficha) sería un proceso
    /// trabado sin forma de destrabarlo desde la pantalla.
    /// </summary>
    internal static class FftFlujo
    {
        /// <summary>
        /// Fase en la que espera un requerimiento FFT que ya está en manos de GTH: la misma en la
        /// que el flujo normal deja al proceso cuando el solicitante aprobó la long list, porque es
        /// exactamente el mismo trabajo pendiente — mandarle el formulario al candidato. El catálogo
        /// la llama «Long list aprobada / Formulario pendiente»; en FFT solo aplica la segunda mitad.
        /// </summary>
        public const string FaseFormulario = EstadoReclutamiento.LongListAprobada;

        /// <summary>
        /// Texto del "siguiente paso" de un requerimiento FFT parado en
        /// <see cref="FaseFormulario"/>. La descripción del catálogo habla de la long list, que en
        /// este flujo no existe.
        /// </summary>
        public const string SiguientePasoFormulario =
            "GTH le enviará el formulario de información al candidato del ingreso directo (FFT).";

        /// <summary>
        /// Fases que un requerimiento FFT nunca recorre. Se usan para recortar la línea de tiempo
        /// del seguimiento: mostrar «Publicación», «Long list» o «Entrevistas» como pasos pendientes
        /// de un proceso que no los tiene deja al solicitante esperando algo que no va a pasar.
        ///
        /// <see cref="FaseFormulario"/> NO está acá: es la fase en la que el proceso realmente se
        /// para. APROBACION_GG tampoco, porque depende de quién pidió (solo se omite cuando el
        /// pedido lo registra el propio Gerente General) y eso se resuelve al leer.
        /// </summary>
        public static readonly HashSet<string> FasesOmitidas = new()
        {
            EstadoReclutamiento.Publicacion,
            EstadoReclutamiento.LongList,
            EstadoReclutamiento.LongListEnviada,
            EstadoReclutamiento.Entrevistas,
            EstadoReclutamiento.SeleccionJefatura,
        };

        /// <summary>
        /// Abre la ficha de candidato del ingreso FFT y deja el requerimiento en
        /// <see cref="FaseFormulario"/>. Idempotente: si el candidato ya existe (reintento de la
        /// execution strategy, o una aprobación que se registró dos veces) no crea otro.
        /// </summary>
        /// <param name="estadoFormularioId">
        /// Id de <see cref="FaseFormulario"/> ya resuelto por el llamador: casi siempre lo necesita
        /// junto con otros estados y así no se paga un roundtrip por vacante.
        /// </param>
        /// <param name="estadoCandidatoAprobadoId">
        /// Id del estado APROBADO de <c>gth_candidato_estado</c>, por el mismo motivo.
        /// </param>
        /// <param name="puestoNombre">
        /// Nombre del puesto del requerimiento, para el snapshot del candidato (igual que en la long
        /// list normal, donde lo copia el sistema y no lo escribe GTH). Lo pasa el llamador, que ya
        /// lo tiene o lo puede traer de una vez para todas las vacantes del lote.
        /// </param>
        /// <param name="yaConCandidato">
        /// Requerimientos que ya tienen candidato, resuelto de una vez para todo el lote con
        /// <see cref="RequerimientosConCandidatoAsync"/> (una decisión en bloque puede traer varias
        /// vacantes FFT y preguntarlo por cada una sería un roundtrip por vacante). El método lo
        /// actualiza al crear, así que también protege de una doble llamada dentro del mismo lote.
        /// </param>
        /// <remarks>
        /// El requerimiento tiene que estar YA persistido: <c>gth_candidato</c> no tiene navegación
        /// hacia él, así que la FK se copia a mano y un id en 0 dejaría al candidato colgado. Los
        /// dos llamadores lo cumplen (la creación de la solicitud guarda antes de llamar acá).
        /// </remarks>
        public static void AbrirCandidato(
            AppDbContext ctx,
            GthRequerimiento req,
            int estadoFormularioId,
            int estadoCandidatoAprobadoId,
            string? puestoNombre,
            ISet<int> yaConCandidato,
            int? userId,
            DateTimeOffset now)
        {
            if (!req.EsFft) return;

            req.GthEstadoRequerimientoId = estadoFormularioId;
            req.UpdatedDateTime          = now;
            req.UpdatedUserId            = userId;

            if (req.GthRequerimientoId == 0)
                throw new AbrilException(
                    "No se pudo abrir la ficha del candidato FFT: el requerimiento aún no está registrado.", 500);

            // Idempotente: un reintento de la execution strategy o una aprobación que se registró
            // dos veces no puede dejar dos candidatos en el mismo proceso.
            if (!yaConCandidato.Add(req.GthRequerimientoId)) return;

            ctx.GthCandidato.Add(new GthCandidato
            {
                GthRequerimientoId   = req.GthRequerimientoId,
                Nombre               = req.FftCandidatoNombre?.Trim() ?? "Candidato FFT",
                Puesto               = puestoNombre,
                // Sin CV: en FFT no hay revisión curricular. El CV llega, si llega, como el
                // documentado que el propio postulante adjunta en su formulario.
                GthCandidatoEstadoId = estadoCandidatoAprobadoId,
                Orden                = 0,
                NumeroLongList       = 1,
                CreatedDateTime      = now,
                CreatedUserId        = userId,
                Active               = true,
                State                = true,
            });
        }

        /// <summary>
        /// De los requerimientos indicados, cuáles ya tienen candidato. Una sola consulta para todo
        /// el lote: es el guardia de idempotencia de <see cref="AbrirCandidato"/>. Vacío (sin tocar
        /// la BD) cuando no hay ninguna vacante FFT que abrir.
        /// </summary>
        public static async Task<HashSet<int>> RequerimientosConCandidatoAsync(
            AppDbContext ctx, IReadOnlyCollection<int> requerimientoIds)
        {
            if (requerimientoIds.Count == 0) return new HashSet<int>();

            var ids = requerimientoIds.ToList();
            return (await ctx.GthCandidato
                    .Where(c => c.State && ids.Contains(c.GthRequerimientoId))
                    .Select(c => c.GthRequerimientoId)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();
        }

        /// <summary>
        /// Cierra la parte de selección de un proceso FFT: marca al candidato como SELECCIONADO,
        /// mueve el requerimiento a EMO de ingreso y le abre su ficha de pre-ingreso en
        /// <c>workers</c>. Es el equivalente de la decisión del finalista del flujo normal, con la
        /// diferencia de que acá no hay nada que decidir — el candidato lo eligió quien lo pidió.
        /// </summary>
        /// <returns>
        /// La ficha de pre-ingreso (su <c>Id</c> queda en 0 hasta el <c>SaveChanges</c> si es nueva) y
        /// el nombre del estado en el que quedó el requerimiento. La ficha es null cuando el
        /// formulario no dejó <c>person_id</c> y por lo tanto no hay a quién programarle el EMO.
        /// </returns>
        public static async Task<(Worker? Ficha, string EstadoNombre)> CerrarConSeleccionadoAsync(
            AppDbContext ctx, int candidatoId, GthRequerimiento req, int? userId, DateTimeOffset now)
        {
            var seleccionadoId = await ctx.GthCandidatoResultado
                .Where(r => r.Codigo == ResultadoCandidato.Seleccionado && r.State)
                .Select(r => (int?)r.GthCandidatoResultadoId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No está configurado el resultado SELECCIONADO de candidatos.", 500);

            // Lo que ya está en el contexto sin guardar cuenta igual que lo que está en la base: si
            // la execution strategy reintenta el bloque, buscar solo en la BD no encontraría la fila
            // que el intento anterior dejó como Added y se insertaría dos veces.
            var evaluacion = ctx.GthCandidatoEvaluacion.Local
                    .FirstOrDefault(e => e.GthCandidatoId == candidatoId && e.State)
                ?? await ctx.GthCandidatoEvaluacion
                    .FirstOrDefaultAsync(e => e.GthCandidatoId == candidatoId && e.State);

            if (evaluacion == null)
            {
                evaluacion = new GthCandidatoEvaluacion
                {
                    GthCandidatoId  = candidatoId,
                    CreatedDateTime = now,
                    CreatedUserId   = userId,
                    Active          = true,
                    State           = true,
                };
                ctx.GthCandidatoEvaluacion.Add(evaluacion);
            }
            else
            {
                evaluacion.UpdatedDateTime = now;
                evaluacion.UpdatedUserId   = userId;
            }

            evaluacion.GthCandidatoResultadoId = seleccionadoId;
            // Quién y cuándo: en FFT la decisión es la aprobación del formulario que acaba de hacer
            // GTH, así que ese es el momento y ese es el usuario. No hay decisión del solicitante
            // que registrar — su decisión fue pedir a esta persona por nombre.
            evaluacion.DecisionDateTime = now;
            evaluacion.DecisionUserId   = userId;

            var emoIngreso = await ctx.GthEstadoRequerimiento
                .FirstOrDefaultAsync(e => e.Codigo == EstadoReclutamiento.EmoIngreso && e.State)
                ?? throw new AbrilException("No está configurado el estado EMO_INGRESO de reclutamiento.", 500);

            req.GthEstadoRequerimientoId = emoIngreso.GthEstadoRequerimientoId;
            req.UpdatedDateTime          = now;
            req.UpdatedUserId            = userId;

            return (await AbrirFichaPreIngresoAsync(ctx, candidatoId, req, now), emoIngreso.Nombre);
        }

        /// <summary>
        /// Ficha de pre-ingreso del candidato FFT: la misma regla que la del finalista aprobado del
        /// flujo normal (reusar la ficha viva si ya existe, abrirla si no) porque es la misma cosa —
        /// alguien que todavía no ingresó pero al que hay que poder programarle su EMO.
        ///
        /// El área sale del puesto que se pidió (<c>puesto_area_scope</c>), que es lo que permite
        /// resolver su jefatura subiendo por el árbol. Si el puesto pertenece a varias áreas no hay
        /// a quién preguntarle —en FFT no existe la pantalla de decisión del finalista, que es donde
        /// se elige— así que entra al área del solicitante, que es quien pidió a esta persona.
        /// </summary>
        private static async Task<Worker?> AbrirFichaPreIngresoAsync(
            AppDbContext ctx, int candidatoId, GthRequerimiento req, DateTimeOffset now)
        {
            var personId = await ctx.GthPostulanteFormulario
                .Where(f => f.GthCandidatoId == candidatoId && f.State && f.PersonId != null)
                .Select(f => f.PersonId)
                .FirstOrDefaultAsync();
            if (personId == null) return null;

            var areaSolicitante = await ctx.GthSolicitud
                .Where(s => s.GthSolicitudId == req.GthSolicitudId)
                .Select(s => s.AreaScopeId)
                .FirstOrDefaultAsync();

            var areasPuesto = await ctx.PuestoAreaScope
                .Where(pas => pas.State && pas.PuestoId == req.PuestoId)
                .Select(pas => pas.AreaScopeId)
                .ToListAsync();

            var areaDestino = areasPuesto.Count == 1 ? areasPuesto[0] : areaSolicitante;

            // Una persona puede tener varias fichas (reingresos): si ya tiene una viva se reusa en
            // vez de abrir otra. Se prioriza la de pre-ingreso y, si no la hay, la más reciente.
            // La ficha que dejó Added un intento anterior de la execution strategy cuenta igual:
            // buscar solo en la BD abriría una segunda al reintentar.
            var existente = ctx.Worker.Local
                    .FirstOrDefault(w => w.PersonId == personId
                                      && w.WorkersEstadoId == WorkersEstadoIds.FinalistaAprobado)
                ?? await ctx.Worker
                    .Where(w => w.PersonId == personId
                             && (w.WorkersEstadoId == WorkersEstadoIds.Activo
                              || w.WorkersEstadoId == WorkersEstadoIds.InhabilitadoSsoma
                              || w.WorkersEstadoId == WorkersEstadoIds.FinalistaAprobado))
                    .OrderByDescending(w => w.WorkersEstadoId == WorkersEstadoIds.FinalistaAprobado ? 1 : 0)
                    .ThenByDescending(w => w.Id)
                    .FirstOrDefaultAsync();

            if (existente != null)
            {
                // El área de una ficha de pre-ingreso sale siempre del requerimiento que la puso
                // ahí. La de un trabajador real es su área de verdad: solo se llena si estaba vacía,
                // para no moverlo de sitio en el árbol antes de que exista el contrato.
                var puedeReasignarArea = existente.WorkersEstadoId == WorkersEstadoIds.FinalistaAprobado
                                      || existente.AreaScopeId == null;
                if (areaDestino != null && puedeReasignarArea && existente.AreaScopeId != areaDestino)
                {
                    existente.AreaScopeId = areaDestino;
                    existente.UpdatedAt   = now;
                }
                return existente;
            }

            var ficha = new Worker
            {
                PersonId        = personId,
                WorkersEstadoId = WorkersEstadoIds.FinalistaAprobado,
                // Solo el puesto: la categoría de la ficha sale de puesto.categoria_id.
                PuestoId        = req.PuestoId,
                ContributorId   = req.ContributorId,
                AreaScopeId     = areaDestino,
                // Clasificación desde ya, no en el onboarding: es lo que hace que los correos de
                // EMO encuentren su columna en la matriz de Configuración de EMOs cuando GTH le
                // programa el examen de ingreso. Ver ClasificacionPreIngreso.
                ContrataCasa       = ClasificacionPreIngreso.ContrataCasaPropia,
                ObraOficinaStaffId = await ClasificacionPreIngreso
                    .ResolverObraOficinaStaffIdAsync(ctx, req.ProjectId),
                // Sin fecha de ingreso: todavía no ingresó.
                FechaIngreso    = null,
                CreatedAt       = now,
                UpdatedAt       = now,
            };
            ctx.Worker.Add(ficha);
            return ficha;
        }
    }
}
