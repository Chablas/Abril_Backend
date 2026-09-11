using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Feriados y días no laborables (Configuración → Feriados) resueltos en memoria, para calcular
    /// el plazo de rendición de las salidas.
    ///
    /// Se carga UNA vez por listado y responde por cualquier mes: un listado toca varios meses
    /// distintos (cada solicitud tiene el suyo) y preguntar mes por mes contra la base sería un
    /// N+1 sobre una tabla que entera cabe en memoria.
    ///
    /// Mismo criterio de resolución que Lecciones Aprendidas
    /// (<c>LessonRepository.GetHolidayDatesAsync</c>): solo filas vigentes (<c>state</c>) y activas,
    /// y las marcadas como <c>recurring_yearly</c> aplican todos los años por mes/día.
    /// </summary>
    public sealed class CalendarioNoLaborable
    {
        /// <summary>
        /// Días hábiles de plazo con los que se responde si <c>ga_rendicion_config</c> está vacía
        /// (base sin sembrar). Es el valor con el que nació la regla, antes de que fuera
        /// configurable: así una base a medio migrar no abre el plazo de par en par ni lo cierra.
        /// </summary>
        public const int DiasHabilesDePlazoPorDefecto = 7;

        /// <summary>Fechas concretas (no recurrentes), tal cual están registradas.</summary>
        private readonly HashSet<DateOnly> _fijos;

        /// <summary>(mes, día) de los feriados que se repiten todos los años.</summary>
        private readonly HashSet<(int Mes, int Dia)> _recurrentes;

        private CalendarioNoLaborable(
            HashSet<DateOnly> fijos, HashSet<(int, int)> recurrentes,
            int diasDePlazo, decimal limiteMovilidad)
        {
            _fijos              = fijos;
            _recurrentes        = recurrentes;
            DiasHabilesDePlazo  = diasDePlazo;
            LimiteMovilidad     = limiteMovilidad;
        }

        /// <summary>
        /// Días hábiles que dura el plazo para rendir un mes, contados sobre el mes siguiente. Sale
        /// de <c>ga_rendicion_config</c> (Solicitud de Salidas → Configuración → Días reembolsables), así
        /// que es un dato del calendario cargado y no una constante.
        /// </summary>
        public int DiasHabilesDePlazo { get; }

        /// <summary>
        /// El plazo escrito como lo dicen los mensajes y los tooltips: "5.º día hábil del mes
        /// siguiente". Está acá para que el número no se vuelva a escribir a mano en cada texto.
        /// </summary>
        public string DiasHabilesDePlazoTexto => $"{DiasHabilesDePlazo}.º día hábil del mes siguiente";

        /// <summary>
        /// Tope de movilidad en soles (<c>ga_rendicion_config.limite_diario_movilidad</c>). Viaja
        /// con el calendario porque sale de la MISMA fila que el plazo, y quien imputa las fechas
        /// de la planilla necesita las dos cosas a la vez: el tope dice cuánto entra en un día y
        /// el calendario, cuáles son esos días. Ver <see cref="TopeMovilidad"/>.
        /// </summary>
        public decimal LimiteMovilidad { get; }

        public static async Task<CalendarioNoLaborable> CargarAsync(AppDbContext ctx)
        {
            var dias = await ctx.Holiday
                .Where(h => h.State && h.Active)
                .Select(h => new { h.HolidayDate, h.RecurringYearly })
                .ToListAsync();

            // Plazo y tope salen de la misma fila y en el mismo viaje: son dos columnas de
            // ga_rendicion_config y pedirlas por separado serían dos consultas por lo mismo.
            var config = await ctx.GaRendicionConfig
                .Where(c => c.State)
                .OrderBy(c => c.Id)
                .Select(c => new { c.DiasHabilesPlazo, c.LimiteDiarioMovilidad })
                .FirstOrDefaultAsync();

            var fijos       = new HashSet<DateOnly>();
            var recurrentes = new HashSet<(int, int)>();
            foreach (var d in dias)
            {
                if (d.RecurringYearly) recurrentes.Add((d.HolidayDate.Month, d.HolidayDate.Day));
                else                   fijos.Add(d.HolidayDate);
            }

            return new CalendarioNoLaborable(
                fijos, recurrentes,
                Acotar(config?.DiasHabilesPlazo ?? DiasHabilesDePlazoPorDefecto),
                TopeMovilidad.Acotar(config?.LimiteDiarioMovilidad));
        }

        /// <summary>
        /// Plazo configurado (fila única de <c>ga_rendicion_config</c>). Se expone aparte porque la
        /// pantalla de configuración lo necesita solo, sin cargar los feriados.
        /// </summary>
        public static async Task<int> LeerDiasDePlazoAsync(AppDbContext ctx)
        {
            var configurado = await ctx.GaRendicionConfig
                .Where(c => c.State)
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.DiasHabilesPlazo)
                .FirstOrDefaultAsync();

            return Acotar(configurado ?? DiasHabilesDePlazoPorDefecto);
        }

        /// <summary>
        /// Deja el plazo dentro del rango que valida la base (CHECK de 1 a 28). Una fila torcida por
        /// SQL a mano no puede hacer que <see cref="LimiteDeRendicion"/> devuelva otro mes.
        /// </summary>
        public static int Acotar(int dias) =>
            Math.Clamp(dias, GaRendicionConfig.DiasMinimo, GaRendicionConfig.DiasMaximo);

        /// <summary>Sábado, domingo, feriado o día no laborable registrado.</summary>
        public bool EsNoLaborable(DateOnly fecha)
        {
            if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return true;
            if (_fijos.Contains(fecha)) return true;
            return _recurrentes.Contains((fecha.Month, fecha.Day));
        }

        /// <summary>
        /// Días feriados o registrados como no laborables, SIN contar el fin de semana.
        /// </summary>
        private bool EsFeriado(DateOnly fecha) =>
            _fijos.Contains(fecha) || _recurrentes.Contains((fecha.Month, fecha.Day));

        /// <summary>
        /// Días en los que la planilla de movilidad puede imputar un gasto: todos menos domingos y
        /// feriados. **El sábado sí cuenta**, y por eso esto no es el complemento de
        /// <see cref="EsNoLaborable"/>: el plazo de rendición se cuenta en días hábiles de verdad
        /// (lunes a viernes) y la imputación del gasto no.
        /// </summary>
        public bool EsImputable(DateOnly fecha) =>
            fecha.DayOfWeek != DayOfWeek.Sunday && !EsFeriado(fecha);

        /// <summary>
        /// Cuántos días del mes admiten gasto de movilidad. Multiplicado por el tope diario da lo
        /// máximo que un trabajador puede llegar a rendir en ese mes: más que eso no entra en la
        /// planilla ni desplazando, porque no quedan días donde ponerlo.
        /// </summary>
        public int DiasImputablesDelMes(int anio, int mes)
        {
            var primero = new DateOnly(anio, mes, 1);
            var ultimo  = primero.AddMonths(1).AddDays(-1);

            var total = 0;
            for (var d = primero; d <= ultimo; d = d.AddDays(1))
                if (EsImputable(d)) total++;

            return total;
        }

        /// <summary>
        /// Último día para rendir las salidas de <paramref name="anio"/>/<paramref name="mes"/>: el
        /// <see cref="DiasHabilesDePlazo"/>.º día hábil del mes SIGUIENTE. Con 5, las salidas de
        /// agosto se rinden hasta el 5.º día hábil de setiembre; pasado ese día el periodo queda
        /// cerrado.
        ///
        /// Si el mes siguiente no llegara a tener tantos días hábiles (caso teórico), el plazo es su
        /// último día: nunca se devuelve una fecha de otro mes.
        /// </summary>
        public DateOnly LimiteDeRendicion(int anio, int mes)
        {
            var primeroSiguiente = new DateOnly(anio, mes, 1).AddMonths(1);
            var ultimoSiguiente  = primeroSiguiente.AddMonths(1).AddDays(-1);

            var habiles = 0;
            for (var d = primeroSiguiente; d <= ultimoSiguiente; d = d.AddDays(1))
            {
                if (EsNoLaborable(d)) continue;
                if (++habiles == DiasHabilesDePlazo) return d;
            }
            return ultimoSiguiente;
        }

        /// <summary>
        /// true si el plazo para rendir las salidas de ese mes ya pasó. "Hoy" se toma en hora de
        /// Perú y no la del servidor, que corre en UTC: el día del vencimiento, pasadas las 19:00
        /// de Lima el UTC ya está en el día siguiente y el plazo se cerraría antes de tiempo.
        /// </summary>
        public bool PlazoVencido(int anio, int mes)
            => MesAnteriorPeru.HoyPeru() > LimiteDeRendicion(anio, mes);
    }
}
