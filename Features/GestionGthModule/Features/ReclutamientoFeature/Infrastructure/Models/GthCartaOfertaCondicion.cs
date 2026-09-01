namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Una condición de contrato de la carta oferta (tabla <c>gth_carta_oferta_condicion</c>): cada
    /// fila es una viñeta del bloque «Las condiciones de contrato se detallan a continuación» del
    /// documento, en el orden en que GTH las escribió.
    ///
    /// Va en tabla hija y no en una columna de texto con saltos de línea porque son N ítems por
    /// carta: la lista se lee, se reordena y se vuelve a mostrar en el formulario al regenerar, y un
    /// bloque de texto obligaría a partirlo por <c>\n</c> en cada lectura —con el primer salto de
    /// línea de más rompiendo la carta en silencio—.
    ///
    /// Tampoco es un catálogo: el texto de cada condición lo redacta GTH por carta. La plantilla que
    /// trajeron trae dos comentarios de Word explicando por qué («la jornada varía si es Staff de
    /// Obra / Personal de Oficina / Call Center…», «la condición laboral también según el cargo»),
    /// así que las combinaciones no son un conjunto cerrado que se pueda sembrar. Lo que sí es fijo
    /// —el bloque de «Otros beneficios» con la EPS— vive escrito en la plantilla, no acá.
    ///
    /// Se reemplazan enteras en cada generación: regenerar la carta es rehacer el documento, y las
    /// condiciones son lo que ese documento dice. Como el resto del módulo, la baja es lógica
    /// (<c>state = false</c>) para no perder qué decía una carta que ya se envió.
    /// </summary>
    public class GthCartaOfertaCondicion
    {
        public int GthCartaOfertaCondicionId { get; set; }

        /// <summary>FK a la carta oferta a la que pertenece la condición.</summary>
        public int GthCartaOfertaId { get; set; }

        /// <summary>
        /// Posición de la viñeta dentro del bloque, empezando en 1. Es un dato del negocio y no del
        /// almacenamiento: el orden en que GTH las escribió es el orden en que el candidato las lee,
        /// y ordenar por el id daría lo mismo hoy pero no si mañana se editan sin regenerar.
        /// </summary>
        public int Orden { get; set; }

        /// <summary>Texto de la condición, tal como lo escribió GTH. Es una viñeta del documento.</summary>
        public string Texto { get; set; } = null!;

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
