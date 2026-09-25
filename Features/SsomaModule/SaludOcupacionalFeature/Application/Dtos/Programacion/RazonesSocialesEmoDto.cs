namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion
{
    /// <summary>
    /// Lo que necesita el desplegable "Razón social" del modal "Programar EMO con clínica": las
    /// empresas del grupo con sus cupos y si a ESTE trabajador le aplica el tope.
    ///
    /// <para>Las dos cosas viajan juntas porque el desplegable no se puede pintar sin las dos: los
    /// cupos son de la razón social y la excepción es del trabajador, y separarlas serían dos
    /// peticiones para un solo campo.</para>
    /// </summary>
    public class RazonesSocialesEmoDto
    {
        /// <summary>Razones sociales operativas del grupo con sus cupos disponibles.</summary>
        public List<Abril_Backend.Shared.Services.RazonSocialCupoDto> Razones { get; set; } = new();

        /// <summary>
        /// true = a este trabajador no le aplica el tope de 20 porque la vacante de la que sale es
        /// un <b>REEMPLAZO</b>: el que entra y el que sale conviven un mes, así que la razón social
        /// se pasa del tope a propósito hasta que se dé de baja al reemplazado. El desplegable sigue
        /// mostrando los cupos —el dato es cierto— pero deja elegir una razón social llena.
        /// </summary>
        public bool SinTopePorReemplazo { get; set; }

        /// <summary>
        /// La razón social que la ficha de pre-ingreso ya tiene, para traerla elegida. La usa
        /// «Registrar resultados de EMO», que elige al trabajador con el buscador y no tiene la fila
        /// de la lista. Null si no tiene ninguna o si ya está adentro: ahí la vigente es la de su
        /// vinculación y no se elige en el EMO.
        /// </summary>
        public int? RazonSocialActualId { get; set; }
    }
}
