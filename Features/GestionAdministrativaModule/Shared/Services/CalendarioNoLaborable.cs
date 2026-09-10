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

        private CalendarioNoLaborable(HashSet<DateOnly> fijos, HashSet<(int, int)> recurrentes, int diasDePlazo)
        {
            _fijos              = fijos;
            _recurrentes        = recurrentes;
            DiasHabilesDePlazo  = diasDePlazo;
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

        public static async Task<CalendarioNoLaborable> CargarAsync(AppDbContext ctx)
        {
            var dias = await ctx.Holiday
                .Where(h => h.State && h.Active)
                .Select(h => new { h.HolidayDate, h.RecurringYearly })
                .ToListAsync();

            var diasDePlazo = await LeerDiasDePlazoAsync(ctx);

            var fijos       = new HashSet<DateOnly>();
            var recurrentes = new HashSet<(int, int)>();
            foreach (var d in dias)
            {
                if (d.RecurringYearly) recurrentes.Add((d.HolidayDate.Month, d.HolidayDate.Day));
                else                   fijos.Add(d.HolidayDate);
            }

            return new CalendarioNoLaborable(fijos, recurrentes, diasDePlazo);
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
