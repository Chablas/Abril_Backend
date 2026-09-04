namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application
{
    /// <summary>
    /// Cómo se rotula el estado de una vacante parada en la fase <c>APROBACION_GG</c>.
    ///
    /// El catálogo <c>gth_estado_requerimiento</c> tiene UNA sola fase de aprobación porque es un
    /// paso del pipeline y no un actor: por ahí pasan todas las vacantes que necesitan firma. Pero
    /// QUIÉN firma lo decide la vacante (ver <see cref="RutaAprobacion"/>) y en los reemplazos
    /// además cambia con el turno, así que rotularlas a todas con el nombre del catálogo le decía
    /// "Aprobación Gerencia General" al solicitante de un reemplazo — que el Gerente General no ve
    /// ni firma.
    ///
    /// El nombre genérico del catálogo se sigue usando donde se describe la fase sin hablar de una
    /// vacante concreta: la línea de tiempo del seguimiento.
    /// </summary>
    public static class EtiquetaAprobacion
    {
        /// <summary>Vacante nueva: la firma el Gerente General y nadie más.</summary>
        public const string GerenciaGeneral = "Aprobación Gerencia General";

        /// <summary>
        /// Reemplazo esperando la PRIMERA firma, la del gerente del área del solicitante. Hasta que
        /// no la dé, GTH ni siquiera ve la vacante.
        /// </summary>
        public const string GerenciaArea = "Aprobación Gerencia del Área";

        /// <summary>Reemplazo que el área ya aprobó: falta la segunda y última firma, la de GTH.</summary>
        public const string Gth = "Aprobación GTH";

        /// <summary>
        /// De quién se está esperando la firma AHORA. Null cuando a la vacante no la firma nadie
        /// (ingreso directo FFT), para que el llamador se quede con el nombre del catálogo.
        /// </summary>
        /// <param name="ruta">Ruta de la vacante — <see cref="RutaAprobacion.De"/>.</param>
        /// <param name="aprobadoGerenteArea"><c>gth_aprobacion_gg_detalle.aprobado_gerente_area</c>.</param>
        /// <param name="aprobadoGth"><c>gth_aprobacion_gg_detalle.aprobado_gth</c>.</param>
        public static string? DeLaVacante(string ruta, bool? aprobadoGerenteArea, bool? aprobadoGth) => ruta switch
        {
            RutaAprobacion.GerenciaGeneral => GerenciaGeneral,
            // El turno lo decide la MISMA regla que reparte la bandeja de «Aprobaciones» y los
            // correos, para que el badge no pueda decir una cosa y la pantalla de quien firma otra.
            RutaAprobacion.AreaYGth => RutaAprobacion.LeTocaAhora(
                    ruta, AprobacionNivel.Gth, aprobadoGerenteArea, aprobadoGth)
                ? Gth
                : GerenciaArea,
            _ => null,
        };
    }
}
