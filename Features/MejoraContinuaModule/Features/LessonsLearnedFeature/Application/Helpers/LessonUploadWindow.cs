using System;
using System.Collections.Generic;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Helpers
{
    /// <summary>
    /// Ventana de los últimos 5 días hábiles del mes para Lecciones Aprendidas:
    ///   • Ordinales 1–3 (los 3 más tempranos): ventana de SUBIDA — los usuarios
    ///     registran sus lecciones; el cron envía recordatorios.
    ///   • Ordinales 4–5 (los 2 últimos días hábiles): ventana de REVISIÓN de la
    ///     jefatura — NO se permite registrar nuevas lecciones.
    ///   • Ordinal 0: la fecha no cae en los últimos 5 días hábiles (subida libre).
    ///
    /// La lógica de <see cref="LastFiveBusinessDayOrdinal"/> está COPIADA (no
    /// compartida) de <c>ReminderService.LastFiveBusinessDayOrdinal</c> a propósito,
    /// para no acoplar el bloqueo de subida con el cron de recordatorios. Si cambia
    /// el criterio de días hábiles, hay que actualizar ambas copias.
    /// </summary>
    public static class LessonUploadWindow
    {
        /// <summary>
        /// true si <paramref name="date"/> cae en el 4.º o 5.º día hábil final del
        /// mes (ventana de revisión de la jefatura → subida bloqueada).
        /// </summary>
        public static bool IsReviewWindow(DateTime date)
        {
            var ordinal = LastFiveBusinessDayOrdinal(date);
            return ordinal == 4 || ordinal == 5;
        }

        /// <summary>
        /// Ordinal de <paramref name="date"/> dentro de los últimos 5 días hábiles
        /// del mes: 1 = el más temprano de los 5, 5 = el último día hábil. 0 si la
        /// fecha no cae en esa ventana. (Copiado de ReminderService; ignora feriados,
        /// igual que el cron.)
        /// </summary>
        public static int LastFiveBusinessDayOrdinal(DateTime date)
        {
            var year = date.Year;
            var month = date.Month;
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            // businessDays[0] = último hábil del mes; businessDays[4] = el más temprano de los 5.
            var businessDays = new List<DateTime>();
            for (var d = lastDay; d.Month == month; d = d.AddDays(-1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    businessDays.Add(d);
                if (businessDays.Count == 5) break;
            }

            var idx = businessDays.FindIndex(d => d.Date == date.Date);
            if (idx < 0) return 0;
            return businessDays.Count - idx; // idx 0 (último hábil) → 5 ; idx 4 → 1
        }
    }
}
