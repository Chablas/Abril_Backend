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
        /// Último día para rendir el mes anterior con el plazo actual. Es el efecto concreto del
        /// número, así que se calcula acá (necesita los feriados) y no en la pantalla.
        /// </summary>
        public DateOnly LimiteMesAnterior { get; set; }

        /// <summary>Mes al que corresponde <see cref="LimiteMesAnterior"/> (1-12) y su año.</summary>
        public int MesAnteriorAnio { get; set; }
        public int MesAnteriorMes { get; set; }

        /// <summary>true = el plazo de ese mes ya venció con el número configurado hoy.</summary>
        public bool MesAnteriorVencido { get; set; }
    }

    /// <summary>Cuerpo del guardado: solo el número.</summary>
    public class PlazoRendicionSaveDto
    {
        public int DiasHabilesPlazo { get; set; }
    }
}
