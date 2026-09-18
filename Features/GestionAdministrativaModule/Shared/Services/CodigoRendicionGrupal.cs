using System.Globalization;
using System.Text;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// El código de la rendición grupal: <c>CONS-&lt;SIGLA&gt;-AAAA-NNN</c> (CONS-GTH-2026-001), con
    /// correlativo propio por área y año. La sigla es la del área del consolidado
    /// (<c>area_item.abreviatura</c>), que se mantiene por base de datos; si un área no la tiene, se
    /// deduce de las iniciales de su nombre para que el código nunca salga sin área.
    /// </summary>
    public static class CodigoRendicionGrupal
    {
        public const string Serie = "CONS";

        /// <summary>Largo máximo de la sigla (el de la columna).</summary>
        private const int SiglaMaxLength = 10;

        /// <summary>Palabras que no aportan inicial: "Gestión DEL Talento Humano" → GTH.</summary>
        private static readonly HashSet<string> Conectores = new(StringComparer.OrdinalIgnoreCase)
        {
            "de", "del", "la", "las", "los", "el", "y", "e", "en", "para", "por", "a",
        };

        /// <summary>"CONS-GTH", o "CONS" solo si el consolidado no tiene área.</summary>
        public static string Prefijo(string? abreviatura, string? nombreArea)
        {
            var sigla = Sigla(abreviatura, nombreArea);
            return sigla.Length == 0 ? Serie : $"{Serie}-{sigla}";
        }

        /// <summary>"CONS-GTH-2026-": lo que comparten los códigos de un área en un año.</summary>
        public static string Raiz(string prefijo, int anio) => $"{prefijo}-{anio}-";

        /// <summary>"CONS-GTH-2026-001".</summary>
        public static string Armar(string prefijo, int anio, int numero) =>
            string.Create(CultureInfo.InvariantCulture, $"{Raiz(prefijo, anio)}{numero:D3}");

        private static string Sigla(string? abreviatura, string? nombreArea)
        {
            var propia = Limpiar(abreviatura);
            if (propia.Length > 0) return propia;

            var palabras = (nombreArea ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !Conectores.Contains(p))
                .Select(Limpiar)
                .Where(p => p.Length > 0)
                .ToList();

            return palabras.Count switch
            {
                0 => string.Empty,
                // Una sola palabra: sus primeras letras ("Legal" → LEGA) y no una sola inicial.
                1 => palabras[0][..Math.Min(4, palabras[0].Length)],
                _ => Recortar(string.Concat(palabras.Select(p => p[0]))),
            };
        }

        /// <summary>
        /// Mayúsculas, sin tildes y solo letras y números: la sigla va en el nombre de los archivos
        /// en SharePoint, que no acepta cualquier carácter.
        /// </summary>
        private static string Limpiar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            var sb = new StringBuilder();
            foreach (var ch in texto.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD))
                if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9') sb.Append(ch);

            return Recortar(sb.ToString());
        }

        private static string Recortar(string sigla) =>
            sigla.Length > SiglaMaxLength ? sigla[..SiglaMaxLength] : sigla;
    }
}
