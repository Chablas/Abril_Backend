using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>De qué se está siguiendo el reembolso. Decide cómo se nombra el ítem en las frases.</summary>
    public enum ReembolsoPipelineItem
    {
        /// <summary>Una salida suelta (Solicitud de Salidas, Gestión de Salidas).</summary>
        Salida = 1,
        /// <summary>Una planilla, que agrupa salidas (Mis Rendiciones, Gestión de Rendiciones).</summary>
        Rendicion = 2,
        /// <summary>Una rendición grupal, que agrupa planillas (Consolidados).</summary>
        Consolidado = 3,
    }

    /// <summary>
    /// Lo que hace falta saber para ubicar un ítem en el recorrido del reembolso. Cada pantalla lo
    /// llena con lo que YA cargó para su detalle: el builder no va a la base.
    ///
    /// Los campos que una pantalla no puede saber van en null y el builder los interpreta como
    /// "esa parte ya quedó atrás": una planilla no guarda el estado de aprobación de sus salidas
    /// porque solo se rinde lo aprobado, y un consolidado no guarda la primera revisión porque solo
    /// se consolida lo aprobado.
    /// </summary>
    public class ReembolsoPipelineInput
    {
        public ReembolsoPipelineItem Tipo { get; set; } = ReembolsoPipelineItem.Salida;

        /// <summary>Código del ítem (SOL-…, REN-…, CONS-…).</summary>
        public string? Codigo { get; set; }

        // ── Pasos 1 y 2: la salida ───────────────────────────────────────────

        public DateTimeOffset? SolicitadaAt { get; set; }

        /// <summary>
        /// <c>ga_estado_aprobacion</c> de la salida. Null en rendiciones y consolidados: lo que
        /// agrupan ya está aprobado por definición.
        /// </summary>
        public int? EstadoAprobacionId { get; set; }

        public DateTimeOffset? AprobadaAt { get; set; }

        /// <summary>
        /// Si el ítem genera reembolso de movilidad: al menos un trayecto reembolsable. En false el
        /// recorrido se corta en la aprobación del revisor, que es donde de verdad termina.
        /// </summary>
        public bool EsReembolsable { get; set; } = true;

        // ── Pasos 3 y 4: sustentos y rendición ───────────────────────────────

        /// <summary>Todos los trayectos reembolsables tienen con qué sustentarse (captura o tarifario).</summary>
        public bool SustentosCompletos { get; set; }

        /// <summary>El backend ya la dio por lista para rendir (plazo, capturas del área, montos).</summary>
        public bool AptaParaRendir { get; set; }

        public bool Rendida { get; set; }
        public DateTimeOffset? RendidaAt { get; set; }

        // ── Paso 5: primera revisión ─────────────────────────────────────────

        /// <summary>
        /// <c>ga_estado_primera_revision</c> de la planilla. Null cuando la pantalla no lo carga
        /// (Consolidados): se asume aprobada, porque sin eso no habría consolidado.
        /// </summary>
        public int? EstadoPrimeraRevisionId { get; set; }

        public DateTimeOffset? EnviadaRevisionAt { get; set; }
        public DateTimeOffset? PrimeraRevisionAt { get; set; }

        // ── Paso 6: consolidado del S10 ──────────────────────────────────────

        public bool TieneConsolidado { get; set; }
        public DateTimeOffset? ConsolidadoAt { get; set; }

        // ── Pasos 7 a 9: firma, Tesorería y pago ─────────────────────────────

        public DateTimeOffset? FirmadoAt { get; set; }

        /// <summary><c>ga_estado_reembolso</c>. Ver <see cref="EstadosSalida.Reembolso"/>.</summary>
        public int EstadoReembolsoId { get; set; } = EstadosSalida.Reembolso.Pendiente;

        /// <summary>
        /// <c>ga_origen_observacion_reembolso</c>: quién devolvió el reembolso. Decide si el rojo va
        /// en la firma (jefatura) o en Tesorería, que son dos momentos distintos del recorrido.
        /// </summary>
        public int? ObservacionOrigenId { get; set; }

        public DateTimeOffset? RevisionTesoreriaAt { get; set; }
        public DateTimeOffset? PagadoAt { get; set; }

        /// <summary>
        /// Lo que agrupa el ítem no está todo en el mismo punto (el <c>reembolsoMixto</c> de las
        /// pantallas). El pipeline muestra lo más atrasado y lo dice en la nota.
        /// </summary>
        public bool Mixto { get; set; }
    }

    /// <summary>
    /// Arma el recorrido del reembolso —de la solicitud al pago— para el pipeline horizontal del
    /// modal de detalle.
    ///
    /// Vive en el Shared del módulo porque lo muestran cinco pantallas sobre tres ítems distintos
    /// (Solicitud de Salidas y Gestión de Salidas sobre una salida, Mis Rendiciones y Gestión de
    /// Rendiciones sobre una planilla, Consolidados sobre una rendición grupal). Con una copia por
    /// pantalla, el mismo gasto podría decir que está en dos pasos distintos según desde dónde se
    /// mire.
    ///
    /// No retrocede pasos cuando algo se observa: el recorrido es siempre el mismo y lo que cambia
    /// es que esa fase queda en <c>observado</c> (rojo) y el resumen dice qué hay que subsanar.
    /// Retroceder escondería todo lo que ya se había cumplido después de ella.
    ///
    /// Es puro: recibe lo que la pantalla ya cargó y no toca la base.
    /// </summary>
    public static class ReembolsoPipelineBuilder
    {
        // Claves estables de las fases. No se muestran: son el identificador con el que el frontend
        // puede reconocer una fase sin depender de su título.
        public const string ClaveSolicitud       = "SOLICITUD";
        public const string ClaveAprobacion      = "APROBACION";
        public const string ClaveSustentos       = "SUSTENTOS";
        public const string ClaveRendicion       = "RENDICION";
        public const string ClavePrimeraRevision = "PRIMERA_REVISION";
        public const string ClaveConsolidado     = "CONSOLIDADO";
        public const string ClaveFirma           = "FIRMA";
        public const string ClaveTesoreria       = "TESORERIA";
        public const string ClavePago            = "PAGO";

        private const string Completado = "completado";
        private const string Actual     = "actual";
        private const string Pendiente  = "pendiente";
        private const string Observado  = "observado";
        private const string Cancelado  = "cancelado";

        public static ReembolsoPipelineDto Build(ReembolsoPipelineInput e)
        {
            // Las dos formas del "observado": son dos momentos distintos del recorrido y por eso no
            // pintan la misma fase. La de Tesorería llega DESPUÉS de la firma (RG-49), así que ahí
            // el paso de la firma sigue cumplido.
            var observado          = e.EstadoReembolsoId == EstadosSalida.Reembolso.Observado;
            var observadoTesoreria = observado && e.ObservacionOrigenId == EstadosSalida.OrigenObservacionReembolso.Tesoreria;
            var observadoJefatura  = observado && !observadoTesoreria;

            // ── 1. Solicitud ────────────────────────────────────────────────
            // Cancelada por el propio solicitante: el recorrido no arrancó y no hay nada más que
            // mostrar, así que el pipeline se queda en este único paso.
            var cancelada = e.EstadoAprobacionId == EstadosSalida.Aprobacion.Cancelado;

            var p1 = Paso(ClaveSolicitud, "Solicitud",
                e.Tipo == ReembolsoPipelineItem.Salida
                    ? "El trabajador registra la salida con sus trayectos y motivos."
                    : $"Las salidas que agrupa {Este(e.Tipo)} quedaron registradas.",
                cancelada ? Cancelado : Completado, e.SolicitadaAt);

            if (cancelada)
            {
                return Cerrar(e, new List<ReembolsoPipelinePasoDto> { p1 }, 1, "Solicitud cancelada");
            }

            // ── 2. Aprobación del revisor ───────────────────────────────────
            var estado2 = e.EstadoAprobacionId switch
            {
                null                               => Completado, // rendición o consolidado: ya está aprobado
                EstadosSalida.Aprobacion.Pendiente => Actual,
                EstadosSalida.Aprobacion.Rechazado => Cancelado,
                EstadosSalida.Aprobacion.Aprobado  => Completado,
                _                                  => Pendiente,
            };

            var p2 = Paso(ClaveAprobacion, "Aprobación del jefe",
                "El revisor del área —jefe, gerente o administrador de obra— aprueba la salida.",
                estado2, e.AprobadaAt);

            if (estado2 == Cancelado)
            {
                // El motivo no se repite acá: el modal ya lo muestra en su propia fila.
                return Cerrar(e, new List<ReembolsoPipelinePasoDto> { p1, p2 }, 2, "Salida rechazada");
            }

            // Lo que no genera reembolso muere acá, así que no se muestran siete pasos que nunca
            // van a cumplirse: el recorrido se recorta a lo que de verdad le toca.
            if (!e.EsReembolsable)
            {
                // Que el recorrido sean dos pasos y no nueve va dicho en el propio titular: sin eso
                // el "Paso 2 de 2" se lee como si faltara la mitad del pipeline.
                return Cerrar(e, new List<ReembolsoPipelinePasoDto> { p1, p2 }, 2,
                    estado2 == Completado
                        ? "Salida aprobada — no genera reembolso"
                        : "Esperando la aprobación del jefe");
            }

            // ── 3. Sustentos ────────────────────────────────────────────────
            var estado3 = estado2 != Completado ? Pendiente
                        : e.Rendida || e.SustentosCompletos || e.AptaParaRendir ? Completado
                        : Actual;

            var p3 = Paso(ClaveSustentos, "Sustentos",
                "Capturas del gasto de movilidad de cada trayecto reembolsable.",
                estado3, null);

            // ── 4. Rendición ────────────────────────────────────────────────
            // Una planilla recién generada nace "Lista para enviar": existe, pero el trabajador
            // todavía no la mandó a revisión, así que el paso sigue siendo suyo.
            var enBorrador = e.EstadoPrimeraRevisionId == EstadosSalida.PrimeraRevision.Borrador;
            var estado4 = estado3 != Completado ? Pendiente
                        : !e.Rendida || enBorrador ? Actual
                        : Completado;

            var p4 = Paso(ClaveRendicion, "Rendición",
                "Se genera la planilla de gasto y se envía a la primera revisión.",
                estado4, e.RendidaAt);
            // Rendida pero sin enviar: la fecha de la rendición sí vale, y es la que explica el paso.
            if (estado4 == Actual && e.Rendida) p4.Fecha = e.RendidaAt;

            // ── 5. Primera revisión ─────────────────────────────────────────
            var estado5 = estado4 != Completado ? Pendiente
                        : e.EstadoPrimeraRevisionId switch
                          {
                              null                                     => Completado, // Consolidados: sin aprobar no habría consolidado
                              EstadosSalida.PrimeraRevision.EnRevision  => Actual,
                              EstadosSalida.PrimeraRevision.Observada   => Observado,
                              EstadosSalida.PrimeraRevision.Aprobada    => Completado,
                              _                                         => Pendiente,
                          };

            var p5 = Paso(ClavePrimeraRevision, "1.ª revisión",
                "La jefatura revisa trayectos, montos y capturas de la planilla.",
                estado5, e.PrimeraRevisionAt);
            // Mientras la revisa, la fecha que importa es desde cuándo la tiene.
            if (estado5 == Actual) p5.Fecha = e.EnviadaRevisionAt;

            // ── 6. Consolidado del S10 ──────────────────────────────────────
            var estado6 = estado5 != Completado ? Pendiente
                        : e.TieneConsolidado ? Completado
                        : Actual;

            var p6 = Paso(ClaveConsolidado, "Consolidado S10",
                "El consolidador registra las planillas en el S10 y adjunta el consolidado.",
                estado6, e.ConsolidadoAt);

            // ── 7. Firma de la jefatura ─────────────────────────────────────
            // La fase se cumple cuando el documento reunió TODAS sus firmas, y eso lo dice el estado
            // del reembolso: en obra firman dos y `FirmadoAt` se sella con la PRIMERA, así que
            // tomarla como prueba mandaba a Tesorería un consolidado que todavía espera al
            // residente. Aprobar el reembolso ES firmarlo, así que cualquier estado posterior da la
            // firma por hecha aunque la fecha no haya viajado en el detalle de esta pantalla.
            var yaFirmado = observadoTesoreria
                         || e.EstadoReembolsoId is EstadosSalida.Reembolso.Aprobado
                                                or EstadosSalida.Reembolso.Firmado
                                                or EstadosSalida.Reembolso.PorPagar
                                                or EstadosSalida.Reembolso.Pagado;

            var estado7 = estado6 != Completado ? Pendiente
                        : observadoJefatura ? Observado
                        : yaFirmado ? Completado
                        : Actual;

            var p7 = Paso(ClaveFirma, "Firma de jefatura",
                "La jefatura aprueba el reembolso firmando el consolidado. En obra firman dos.",
                estado7, e.FirmadoAt);

            // ── 8. Tesorería ────────────────────────────────────────────────
            var estado8 = estado7 != Completado ? Pendiente
                        : observadoTesoreria ? Observado
                        : e.EstadoReembolsoId is EstadosSalida.Reembolso.PorPagar or EstadosSalida.Reembolso.Pagado ? Completado
                        : Actual;

            var p8 = Paso(ClaveTesoreria, "Tesorería",
                "Tesorería revisa la documentación y habilita el desembolso.",
                estado8, e.RevisionTesoreriaAt);

            // ── 9. Pago ─────────────────────────────────────────────────────
            var estado9 = estado8 != Completado ? Pendiente
                        : e.EstadoReembolsoId == EstadosSalida.Reembolso.Pagado ? Completado
                        : Actual;

            var p9 = Paso(ClavePago, "Pago",
                "Tesorería abona el reembolso.",
                estado9, e.PagadoAt);

            var pasos = new List<ReembolsoPipelinePasoDto> { p1, p2, p3, p4, p5, p6, p7, p8, p9 };

            // El paso actual es el primero que todavía pide algo. Con todo cumplido es el último:
            // el recorrido terminó en el pago y el pipeline queda entero en verde.
            var idx = pasos.FindIndex(p => p.Estado is Actual or Observado or Cancelado);
            var pasoActual = idx >= 0 ? idx + 1 : pasos.Count;

            return Cerrar(e, pasos, pasoActual, Mensaje(e, pasos[pasoActual - 1]));
        }

        /// <summary>
        /// El recorrido de una PLANILLA, armado desde los estados tal como los nombra su fila (que
        /// es lo que las dos pantallas de planillas ya tienen cargado). Existe para que Mis
        /// Rendiciones y Gestión de Rendiciones no traduzcan cada una por su cuenta: son DTOs
        /// distintos con los mismos campos, y una traducción que se desincronice haría que la misma
        /// planilla se viera en dos pasos distintos según quién la mire.
        ///
        /// Los primeros cuatro pasos van cumplidos por definición: solo se rinde lo aprobado y
        /// sustentado, y la planilla existe porque alguien la rindió.
        /// </summary>
        public static ReembolsoPipelineDto ParaPlanilla(
            string? codigo,
            DateTimeOffset rendidoAt,
            string estadoPrimeraRevision,
            DateTimeOffset? enviadaRevisionAt,
            DateTimeOffset? primeraRevisionAt,
            ConsolidadoS10Dto? consolidado,
            DateTimeOffset? firmadoAt,
            string estadoReembolso,
            string? observacionOrigen,
            bool mixto) => Build(new ReembolsoPipelineInput
            {
                Tipo                       = ReembolsoPipelineItem.Rendicion,
                Codigo                     = codigo,
                Rendida                    = true,
                RendidaAt                  = rendidoAt,
                SustentosCompletos         = true,
                EstadoPrimeraRevisionId    = EstadosSalida.PrimeraRevision.IdFromNombre(estadoPrimeraRevision),
                EnviadaRevisionAt          = enviadaRevisionAt,
                PrimeraRevisionAt          = primeraRevisionAt,
                TieneConsolidado           = consolidado != null,
                ConsolidadoAt              = consolidado?.UploadedAt,
                // La firma que cuenta es la del consolidado (es lo que se firma al aprobar el
                // reembolso); la de la planilla es la misma decisión estampada en el otro papel.
                FirmadoAt                  = consolidado?.FirmadoAt ?? firmadoAt,
                EstadoReembolsoId          = EstadosSalida.Reembolso.IdFromNombre(estadoReembolso)
                                             ?? EstadosSalida.Reembolso.Pendiente,
                // El origen sigue viajando aunque su texto no: es lo que decide si el rojo va en la
                // firma (jefatura) o en Tesorería.
                ObservacionOrigenId        = EstadosSalida.OrigenObservacionReembolso.IdFromNombre(observacionOrigen),
                Mixto                      = mixto,
            });

        // ── Armado ──────────────────────────────────────────────────────────

        private static ReembolsoPipelinePasoDto Paso(
            string clave, string titulo, string descripcion, string estado, DateTimeOffset? fecha) => new()
        {
            Clave       = clave,
            Titulo      = titulo,
            Descripcion = descripcion,
            Estado      = estado,
            // La fecha solo tiene sentido en lo que ya pasó: en un paso pendiente sería la de un
            // intento anterior (una planilla observada y vuelta a generar conserva la suya).
            Fecha       = estado is Completado or Observado or Cancelado ? fecha : null,
        };

        private static ReembolsoPipelineDto Cerrar(
            ReembolsoPipelineInput e,
            List<ReembolsoPipelinePasoDto> pasos,
            int pasoActual,
            string resumen)
        {
            var dto = new ReembolsoPipelineDto
            {
                ItemTipo     = Etiqueta(e.Tipo),
                ItemCodigo   = e.Codigo,
                PasoActual   = pasoActual,
                TotalPasos   = pasos.Count,
                EstadoActual = pasos[pasoActual - 1].Estado,
                Resumen      = resumen,
                Pasos        = pasos,
            };

            if (e.Mixto)
            {
                dto.Nota = e.Tipo == ReembolsoPipelineItem.Consolidado
                    ? "Sus planillas no están todas en el mismo punto: se muestra la más atrasada."
                    : "Sus salidas no están todas en el mismo punto: se muestra la más atrasada.";
            }

            return dto;
        }

        /// <summary>
        /// El titular del paso en el que está parado. Nombra el estado, no lo explica: lo que haya
        /// que ampliar ya vive en su propio bloque del modal.
        /// </summary>
        private static string Mensaje(ReembolsoPipelineInput e, ReembolsoPipelinePasoDto paso) =>
            (paso.Clave, paso.Estado) switch
            {
                (ClaveAprobacion, Actual)         => "Esperando la aprobación del jefe",
                (ClaveSustentos, Actual)          => "Faltan sustentos",

                (ClaveRendicion, Actual) when e.Rendida        => "Rendida, sin enviar a revisión",
                (ClaveRendicion, Actual) when e.AptaParaRendir => "Lista para rendir",
                (ClaveRendicion, Actual)                       => "Sin rendir",

                (ClavePrimeraRevision, Actual)    => "En primera revisión",
                (ClavePrimeraRevision, Observado) => "Observada en la primera revisión",

                (ClaveConsolidado, Actual)        => "Esperando el consolidado del S10",

                (ClaveFirma, Actual)              => "Esperando la firma de la jefatura",
                (ClaveFirma, Observado)           => "Reembolso observado por la jefatura",

                (ClaveTesoreria, Actual)          => "En revisión de Tesorería",
                (ClaveTesoreria, Observado)       => "Reembolso observado por Tesorería",

                (ClavePago, Actual)               => "Listo para el pago",
                (ClavePago, Completado)           => "Reembolso pagado",

                // No debería llegar acá: los casos cortados (rechazo, cancelación, sin reembolso) se
                // cierran antes con su propio titular.
                _                                 => paso.Titulo,
            };

        /// <summary>Cómo se nombra el ítem dentro de una frase ("esta rendición").</summary>
        private static string Este(ReembolsoPipelineItem t) => t switch
        {
            ReembolsoPipelineItem.Rendicion   => "esta rendición",
            ReembolsoPipelineItem.Consolidado => "este consolidado",
            _                                 => "esta salida",
        };

        private static string Etiqueta(ReembolsoPipelineItem t) => t switch
        {
            ReembolsoPipelineItem.Rendicion   => "Rendición",
            ReembolsoPipelineItem.Consolidado => "Consolidado",
            _                                 => "Salida",
        };

    }
}
