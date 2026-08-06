namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>workers_obra_oficina_staff</c>. Se insertan con id
    /// explícito en <c>Migrations_Manual/workers_obra_oficina_staff.sql</c>, así
    /// que son idénticos en dev y prod y se pueden usar como constantes.
    ///
    /// Sustituyen a las comparaciones por texto (<c>obra_oficina == "Staff"</c>)
    /// que existían antes de normalizar la columna.
    /// </summary>
    public static class ObraOficinaStaffIds
    {
        public const int Obra = 1;
        /// <summary>Staff = personal de Oficina Técnica destacado en proyecto.</summary>
        public const int Staff = 2;
        public const int OficinaCentral = 3;

        /// <summary>
        /// Los dos valores que identifican personal "de escritorio" (con correo
        /// corporativo propio, Vida Ley, etc.), frente a <see cref="Obra"/>.
        /// </summary>
        public static readonly int[] StaffUOficinaCentral = { Staff, OficinaCentral };

        /// <summary>Nombre del catálogo a partir del id (para DTOs y correos).</summary>
        public static string? Nombre(int? id) => id switch
        {
            Obra => "Obra",
            Staff => "Staff",
            OficinaCentral => "Oficina Central",
            _ => null
        };

        /// <summary>
        /// Id a partir del nombre (tolerante a mayúsculas/espacios). Se usa en los
        /// pocos puntos que todavía reciben el valor como texto (importaciones,
        /// filtros que llegan por query string).
        /// </summary>
        public static int? DesdeNombre(string? nombre)
        {
            var n = nombre?.Trim();
            if (string.IsNullOrEmpty(n)) return null;
            if (string.Equals(n, "Obra", StringComparison.OrdinalIgnoreCase)) return Obra;
            if (string.Equals(n, "Staff", StringComparison.OrdinalIgnoreCase)) return Staff;
            if (string.Equals(n, "Oficina Central", StringComparison.OrdinalIgnoreCase)) return OficinaCentral;
            return null;
        }
    }
}
