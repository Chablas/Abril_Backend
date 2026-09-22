namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>
    /// El seguimiento del reembolso en forma de fases, tal como lo pinta el pipeline horizontal del
    /// modal de detalle. Lo arma <c>ReembolsoPipelineBuilder</c>.
    ///
    /// Es el MISMO recorrido en las cinco pantallas —de la solicitud al pago—; lo que cambia es el
    /// ítem que se está mirando (<see cref="ItemTipo"/>): una salida suelta en Solicitud de Salidas
    /// y Gestión de Salidas, una planilla en Mis Rendiciones y Gestión de Rendiciones, y una
    /// rendición grupal en Consolidados. Por eso las frases se arman con el nombre del ítem y no
    /// con "la salida" fijo.
    ///
    /// Viaja dentro del detalle que la pantalla ya pide: no tiene endpoint propio.
    /// </summary>
    public class ReembolsoPipelineDto
    {
        /// <summary>"Salida" | "Rendición" | "Consolidado" — de qué se está siguiendo el reembolso.</summary>
        public string ItemTipo { get; set; } = string.Empty;

        /// <summary>Código del ítem (SOL-…, REN-…, CONS-…). Null en los registros sin código.</summary>
        public string? ItemCodigo { get; set; }

        /// <summary>
        /// Paso en el que está parado (1-based, contra <see cref="TotalPasos"/>). Con todo cumplido
        /// es el último: el recorrido terminó en el pago.
        /// </summary>
        public int PasoActual { get; set; }

        public int TotalPasos { get; set; }

        /// <summary>
        /// Estado del paso actual, repetido acá para que la pantalla pinte el resumen sin tener que
        /// indexar la lista. Mismos valores que <see cref="ReembolsoPipelinePasoDto.Estado"/>.
        /// </summary>
        public string EstadoActual { get; set; } = string.Empty;

        /// <summary>
        /// Titular de en qué anda ("En primera revisión"). Es lo ÚNICO que se lee de corrido: lo que
        /// haya que ampliar —la observación, el motivo del rechazo, las firmas que faltan— ya lo
        /// muestra el modal en su propio bloque, y repetirlo acá era decirlo dos veces.
        /// </summary>
        public string Resumen { get; set; } = string.Empty;

        /// <summary>
        /// Aviso extra debajo del pipeline: que la salida no genera reembolso, que el grupo no está
        /// todo en el mismo punto, o que hay una corrección en curso con el Coordinador ERP. Null
        /// en el caso normal.
        /// </summary>
        public string? Nota { get; set; }

        public List<ReembolsoPipelinePasoDto> Pasos { get; set; } = new();
    }

    /// <summary>Una fase del recorrido.</summary>
    public class ReembolsoPipelinePasoDto
    {
        /// <summary>Identificador estable de la fase (SOLICITUD, APROBACION, …). No se muestra.</summary>
        public string Clave { get; set; } = string.Empty;

        /// <summary>Lo que se lee debajo del círculo. Corto: entra en dos líneas.</summary>
        public string Titulo { get; set; } = string.Empty;

        /// <summary>Qué pasa en esta fase y quién la resuelve. Va en el tooltip.</summary>
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>
        /// "completado" | "actual" | "pendiente" | "observado" | "cancelado".
        ///
        /// <c>observado</c> es el paso que devolvió algo (la primera revisión, la jefatura o
        /// Tesorería): no se retrocede de paso —el recorrido es el mismo— sino que la fase se pinta
        /// en rojo y el resumen dice qué hay que subsanar. <c>cancelado</c> es el final feo de la
        /// salida (rechazada o cancelada), donde el recorrido no sigue.
        /// </summary>
        public string Estado { get; set; } = string.Empty;

        /// <summary>Cuándo se cumplió (o cuándo empezó a esperar). Null si no aplica.</summary>
        public DateTimeOffset? Fecha { get; set; }
    }
}
