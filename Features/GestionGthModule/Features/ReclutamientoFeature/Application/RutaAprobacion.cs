namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application
{
    /// <summary>
    /// Por dónde tiene que pasar una vacante para quedar aprobada. Es una propiedad de la VACANTE y
    /// no de la solicitud: una misma solicitud puede pedir un puesto nuevo y el reemplazo de alguien
    /// que se va, y cada uno lo aprueba gente distinta.
    ///
    /// No es un catálogo de base de datos: son las rutas del flujo, fijas por diseño, y se derivan
    /// de datos que sí están en BD (<c>gth_requerimiento.es_fft</c> y el código del tipo de
    /// requerimiento). Guardarlas en una columna las dejaría congeladas frente a un cambio de tipo.
    /// </summary>
    public static class RutaAprobacion
    {
        /// <summary>
        /// La firma de Gerencia General y nada más. Es la ruta de los requerimientos NUEVOS que no
        /// son un ingreso directo.
        /// </summary>
        public const string GerenciaGeneral = "GG";

        /// <summary>
        /// Las firmas del gerente del área del solicitante Y de GTH, las dos. Es la ruta de los
        /// REEMPLAZOS que no son FFT: cubrir a alguien que se va no crea plaza, así que no sube a
        /// Gerencia General — lo valida quien conoce el área y quien lleva el proceso.
        /// </summary>
        public const string AreaYGth = "AREA_GTH";

        /// <summary>
        /// Ninguna firma: la vacante no se aprueba. Es la ruta de TODO ingreso directo <b>FFT</b>,
        /// lo pida quien lo pida — en un FFT no hay nada que decidir (quien pide ya nombró a la
        /// persona), así que la vacante nace en manos de GTH esperando el EMO de ingreso y nunca
        /// aparece en la pantalla «Aprobaciones». Ver <see cref="Shared.FftFlujo"/>.
        /// </summary>
        public const string Ninguna = "NINGUNA";

        /// <summary>
        /// Ruta de una vacante. El FFT gana sobre el tipo: un ingreso directo no pasa por ninguna
        /// firma aunque esté registrado como nuevo o como reemplazo.
        /// </summary>
        /// <param name="esFft"><c>gth_requerimiento.es_fft</c>.</param>
        /// <param name="tipoCodigo">
        /// Código del tipo de requerimiento (<c>gth_tipo_requerimiento.codigo</c>): NUEVO /
        /// REEMPLAZO. Se compara por código y nunca por nombre, que es presentación y se puede
        /// renombrar desde Configuración sin avisarle a nadie.
        /// </param>
        /// <param name="fftEnAprobacionLegada">
        /// Solo para las vacantes FFT: true cuando la vacante quedó enganchada a una aprobación
        /// (tiene su fila en <c>gth_aprobacion_gg_detalle</c>) porque se registró ANTES de que el
        /// ingreso directo dejara de aprobarse. Esas siguen por su camino viejo —Gerencia General
        /// las decide, y las que ya decidió se siguen viendo en su bandeja—; las nuevas no tienen
        /// esa fila y no pasan por ninguna firma. Mismo criterio que
        /// <see cref="Shared.FftFlujo.FaseFormularioLegado"/>: lo que cambia es el flujo de hoy, no
        /// el de los procesos que ya estaban en marcha.
        /// </param>
        public static string De(bool esFft, string? tipoCodigo, bool fftEnAprobacionLegada = false) =>
            esFft
                ? (fftEnAprobacionLegada ? GerenciaGeneral : Ninguna)
                : string.Equals(tipoCodigo, CodigoReemplazo, StringComparison.OrdinalIgnoreCase)
                    ? AreaYGth
                    : GerenciaGeneral;

        /// <summary>
        /// Código del tipo de requerimiento que manda la vacante por la ruta del área + GTH. Espejo
        /// de <c>TipoRequerimientoReclutamiento.Reemplazo</c>, que vive en Infrastructure y no se
        /// puede referenciar desde acá sin invertir la dependencia.
        /// </summary>
        public const string CodigoReemplazo = "REEMPLAZO";

        /// <summary>
        /// ¿Le toca decidir a este nivel una vacante de esta ruta? Es la regla que reparte el
        /// trabajo en la pantalla «Aprobaciones» y la que decide qué vacantes lleva cada correo, así
        /// que vive en un solo sitio para que las dos no puedan discrepar.
        /// </summary>
        public static bool DecideEsteNivel(string ruta, string nivel) => ruta switch
        {
            GerenciaGeneral => nivel == AprobacionNivel.GerenteGeneral,
            AreaYGth        => nivel is AprobacionNivel.GerenteArea or AprobacionNivel.Gth,
            // Ninguna incluida: un ingreso directo no lo firma nadie.
            _               => false,
        };
    }
}
