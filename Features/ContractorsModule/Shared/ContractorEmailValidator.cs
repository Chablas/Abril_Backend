using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Features.ContractorsModule.Shared
{
    /// <summary>
    /// Validación de correos compartida por las features del módulo de contratistas
    /// (registro y gestión).
    ///
    /// Regla: el correo solo puede contener letras y números, además del '@' y el '.'.
    /// No se permiten símbolos como ' &lt; &gt; + - _ etc., y no puede empezar con un símbolo
    /// (debe empezar con una letra o número). Debe tener exactamente un '@' y un dominio
    /// con al menos un punto.
    /// </summary>
    public static partial class ContractorEmailValidator
    {
        [GeneratedRegex(@"^[A-Za-z0-9]+(\.[A-Za-z0-9]+)*@[A-Za-z0-9]+(\.[A-Za-z0-9]+)+$")]
        private static partial Regex EmailRegex();

        /// <summary>Devuelve true si el correo cumple con la regla (solo letras, números, '@' y '.').</summary>
        public static bool IsValid(string? email)
        {
            return !string.IsNullOrWhiteSpace(email) && EmailRegex().IsMatch(email.Trim());
        }

        /// <summary>
        /// Valida cada correo de la lista. Lanza <see cref="AbrilException"/> (400) con un
        /// mensaje en español si alguno no cumple el formato.
        /// </summary>
        public static void ValidateOrThrow(IEnumerable<string?> emails)
        {
            foreach (var email in emails)
            {
                var trimmed = email?.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (!IsValid(trimmed))
                    throw new AbrilException(
                        $"El correo \"{trimmed}\" no es válido. Solo puede contener letras, números, \"@\" y \".\", y no puede empezar con un símbolo.",
                        400);
            }
        }
    }
}
