using System.Globalization;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Reglas de presentación de una planilla de rendición, compartidas por las tres pantallas que
    /// la muestran: Mis Rendiciones (el trabajador), Gestión de Rendiciones (el revisor) y
    /// Reembolsos (Tesorería). Son las mismas planillas vistas desde distinto alcance, así que el
    /// estado y el periodo tienen que leerse igual en las tres.
    /// </summary>
    public static class PlanillaRendicionHelper
    {
        /// <summary>
        /// Estado del reembolso de una planilla a partir de los de sus salidas: gana el que más
        /// atención pide. Mientras una sola salida siga atrás la planilla no está cerrada, y decir
        /// lo contrario escondería trabajo pendiente.
        /// </summary>
        public static string ResumirEstadoReembolso(IEnumerable<int> estados)
        {
            var set = estados.ToHashSet();
            if (set.Contains(EstadosSalida.Reembolso.Rechazado)) return EstadosSalida.Reembolso.NombreRechazado;
            if (set.Contains(EstadosSalida.Reembolso.Pendiente)) return EstadosSalida.Reembolso.NombrePendiente;
            if (set.Contains(EstadosSalida.Reembolso.Aprobado))  return EstadosSalida.Reembolso.NombreAprobado;
            if (set.Contains(EstadosSalida.Reembolso.Firmado))   return EstadosSalida.Reembolso.NombreFirmado;
            return EstadosSalida.Reembolso.NombrePagado;
        }

        /// <summary>"Agosto 2026", o "Julio — Agosto 2026" cuando la planilla cruza meses.</summary>
        public static string EtiquetaPeriodo(DateOnly desde, DateOnly hasta)
        {
            if (desde.Year == hasta.Year && desde.Month == hasta.Month)
                return EtiquetaMes(desde.Year, desde.Month);

            var cultura  = CultureInfo.GetCultureInfo("es-PE");
            var mesDesde = Capitalizar(cultura.DateTimeFormat.GetMonthName(desde.Month), cultura);
            var mesHasta = Capitalizar(cultura.DateTimeFormat.GetMonthName(hasta.Month), cultura);
            return desde.Year == hasta.Year
                ? $"{mesDesde} — {mesHasta} {hasta.Year}"
                : $"{mesDesde} {desde.Year} — {mesHasta} {hasta.Year}";
        }

        /// <summary>"Agosto 2026" — con la primera letra en mayúscula, listo para imprimir.</summary>
        public static string EtiquetaMes(int anio, int mes)
        {
            var cultura = CultureInfo.GetCultureInfo("es-PE");
            return $"{Capitalizar(cultura.DateTimeFormat.GetMonthName(mes), cultura)} {anio}";
        }

        /// <summary>Número de planilla como se imprime en el PDF ("TI: 000123"). Null si no tiene.</summary>
        public static string? NumeroPlanilla(int? numero) =>
            numero.HasValue ? $"TI: {numero.Value:D6}" : null;

        private static string Capitalizar(string nombre, CultureInfo cultura) =>
            string.IsNullOrEmpty(nombre) ? nombre : $"{char.ToUpper(nombre[0], cultura)}{nombre[1..]}";
    }
}
