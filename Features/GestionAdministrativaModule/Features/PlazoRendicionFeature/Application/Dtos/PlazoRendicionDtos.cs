namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos
{
    /// <summary>
    /// Plazo para rendir un mes, tal como lo muestra la sección "Días reembolsables" de
    /// Solicitud de Salidas → Configuración.
    /// </summary>
    public class PlazoRendicionDto
    {
        /// <summary>Días hábiles del mes SIGUIENTE que dura el plazo (ga_rendicion_config).</summary>
        public int DiasHabilesPlazo { get; set; }

        /// <summary>Mínimo y máximo aceptados, para que la pantalla acote el campo sin repetirlos.</summary>
        public int DiasMinimo { get; set; }
        public int DiasMaximo { get; set; }

        /// <summary>
        /// Mes más viejo que HOY se puede rendir con esta configuración. Es el efecto concreto de
        /// los tres campos juntos, así que se calcula acá (necesita los feriados) y no en la
        /// pantalla.
        /// </summary>
        public int RendibleDesdeAnio { get; set; }
        public int RendibleDesdeMes { get; set; }

        /// <summary>
        /// Hasta qué mes hacia atrás alcanza la ventana de los días hábiles
        /// (<c>ga_rendicion_alcance</c>).
        /// </summary>
        public int AlcancePlazoId { get; set; }

        /// <summary>
        /// Hasta qué mes hacia atrás se puede rendir en cualquier momento del mes, con la ventana
        /// abierta o cerrada. null = no aplica, y entonces manda <see cref="AlcancePlazoId"/>.
        /// </summary>
        public int? AlcancePermanenteId { get; set; }

        /// <summary>
        /// Opciones de los dos desplegables. Son las mismas para ambos: la diferencia está en
        /// cuándo aplica cada uno, no en cuánto alcanza.
        /// </summary>
        public List<AlcanceRendicionOpcionDto> Alcances { get; set; } = new();
    }

    /// <summary>Una fila viva de <c>ga_rendicion_alcance</c>.</summary>
    public class AlcanceRendicionOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Meses hacia atrás que abarca (1 = el mes anterior).</summary>
        public int MesesAtras { get; set; }
    }

    /// <summary>Cuerpo del guardado: el número y los dos alcances.</summary>
    public class PlazoRendicionSaveDto
    {
        public int DiasHabilesPlazo { get; set; }

        public int AlcancePlazoId { get; set; }

        /// <summary>null = sin alcance permanente (el desplegable quedó vacío).</summary>
        public int? AlcancePermanenteId { get; set; }
    }
}
