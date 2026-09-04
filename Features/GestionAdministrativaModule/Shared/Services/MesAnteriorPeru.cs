namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Rango del mes anterior al actual, en hora de Perú. Lo usan las dos pantallas de salidas para
    /// la acción "rendir el mes anterior", de ahí que viva en el Shared del módulo.
    /// </summary>
    public static class MesAnteriorPeru
    {
        /// <summary>
        /// Primer y último día del mes anterior al actual, en hora de Perú (UTC-5). El servidor
        /// corre en UTC: el día 1 de mes, antes de las 05:00 UTC, en Lima todavía es el mes
        /// anterior y el rango calculado con la hora del servidor saldría corrido un mes entero.
        /// </summary>
        public static (DateOnly Desde, DateOnly Hasta) Rango()
        {
            var hoy              = HoyPeru();
            var primeroMesActual = new DateOnly(hoy.Year, hoy.Month, 1);
            return (primeroMesActual.AddMonths(-1), primeroMesActual.AddDays(-1));
        }

        /// <summary>
        /// Primer y último día de un mes cualquiera. Lo usa el desplegable "Mes a rendir", que ya
        /// no está atado al mes anterior: el usuario elige el periodo y este método lo traduce al
        /// rango de <c>fecha_salida</c> con el que filtra el repositorio.
        /// </summary>
        public static (DateOnly Desde, DateOnly Hasta) RangoDe(int anio, int mes)
        {
            var primero = new DateOnly(anio, mes, 1);
            return (primero, primero.AddMonths(1).AddDays(-1));
        }

        /// <summary>Hoy en hora de Perú (UTC-5). El servidor corre en UTC.</summary>
        public static DateOnly HoyPeru() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
    }
}
