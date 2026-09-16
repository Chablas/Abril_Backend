namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>ga_rendicion_alcance</c>: hasta qué mes hacia atrás se pueden rendir las
    /// salidas. Solo se usan como valor por defecto —el alcance vigente sale de
    /// <c>ga_rendicion_config</c> y sus meses, de la fila del catálogo—, así que agregar una opción
    /// nueva no obliga a tocar esta clase.
    /// </summary>
    public static class RendicionAlcanceIds
    {
        /// <summary>
        /// "Hasta el mes anterior" (1 mes). Es el comportamiento con el que nació la regla y el
        /// valor por defecto de <c>alcance_plazo_id</c>.
        /// </summary>
        public const int MesAnterior = 1;

        /// <summary>"Hasta 6 meses atrás" (6 meses).</summary>
        public const int SeisMeses = 2;
    }
}
