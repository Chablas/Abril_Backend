namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application
{
    /// <summary>
    /// Por dónde tiene que pasar una vacante para quedar aprobada. Es una propiedad de la VACANTE y
    /// no de la solicitud: una misma solicitud puede pedir un puesto nuevo y el reemplazo de alguien
    /// que se va, y cada uno lo aprueba gente distinta.
    ///
    /// No es un catálogo de base de datos: son las dos rutas del flujo, fijas por diseño, y se
    /// derivan de datos que sí están en BD (<c>gth_requerimiento.es_fft</c> y el código del tipo de
    /// requerimiento). Guardarlas en una columna las dejaría congeladas frente a un cambio de tipo.
    /// </summary>
    public static class RutaAprobacion
    {
        /// <summary>
        /// La firma de Gerencia General y nada más. Es la ruta de los requerimientos NUEVOS y la de
        /// todas las vacantes FFT — en un ingreso directo lo que se aprueba es a una persona con
        /// nombre propio, y esa decisión es de Gerencia General sea nuevo o reemplazo.
        /// </summary>
        public const string GerenciaGeneral = "GG";

        /// <summary>
        /// Las firmas del gerente del área del solicitante Y de GTH, las dos. Es la ruta de los
        /// REEMPLAZOS que no son FFT: cubrir a alguien que se va no crea plaza, así que no sube a
        /// Gerencia General — lo valida quien conoce el área y quien lleva el proceso.
        /// </summary>
        public const string AreaYGth = "AREA_GTH";

        /// <summary>
        /// Ruta de una vacante. El FFT gana sobre el tipo: un ingreso directo va a Gerencia General
        /// aunque esté registrado como reemplazo.
        /// </summary>
        /// <param name="esFft"><c>gth_requerimiento.es_fft</c>.</param>
        /// <param name="tipoCodigo">
        /// Código del tipo de requerimiento (<c>gth_tipo_requerimiento.codigo</c>): NUEVO /
        /// REEMPLAZO. Se compara por código y nunca por nombre, que es presentación y se puede
        /// renombrar desde Configuración sin avisarle a nadie.
        /// </param>
        public static string De(bool esFft, string? tipoCodigo) =>
            !esFft && string.Equals(tipoCodigo, CodigoReemplazo, StringComparison.OrdinalIgnoreCase)
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
            _               => false,
        };
    }
}
