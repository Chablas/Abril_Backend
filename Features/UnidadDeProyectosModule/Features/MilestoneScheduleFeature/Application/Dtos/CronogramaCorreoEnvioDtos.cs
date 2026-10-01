using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos
{
    /// <summary>A quién sale un correo del cronograma, ya repartido en Para, CC y CCO.</summary>
    public class CronogramaCorreoEnvio
    {
        /// <summary>false = el correo está apagado o no queda nadie en el Para.</summary>
        public bool Enviar { get; init; }
        public List<string> Para { get; init; } = new();
        public List<string> Cc { get; init; } = new();
        public List<string> Cco { get; init; } = new();
    }

    /// <summary>
    /// La configuración de un correo para enviarlo: sus dos interruptores y sus destinatarios ya
    /// resueltos a direcciones. Se lee una vez y se arma para cada envío (el recordatorio sale una
    /// vez por residente, con el mismo resto de la lista).
    /// </summary>
    public class CronogramaCorreoListaEnvio
    {
        public bool Activo { get; init; } = true;
        public bool PrincipalActivo { get; init; } = true;
        public List<string> Para { get; init; } = new();
        public List<string> Cc { get; init; } = new();
        public List<string> Cco { get; init; } = new();

        /// <summary>
        /// Sin configuración que leer (falta el SQL, o falló la lectura): le llega solo al
        /// destinatario del sistema, si el correo tiene uno.
        /// </summary>
        public static CronogramaCorreoListaEnvio SoloPrincipal { get; } = new();

        /// <summary>
        /// Para = el principal (si está prendido) y los configurados como Para; después los CC y los
        /// CCO, sin repetir a nadie (gana el primero: Para, CC, CCO). Si el Para queda vacío, lo
        /// ocupan los CC, o si no hay, los CCO: un correo sale siempre con alguien en el Para.
        /// </summary>
        public CronogramaCorreoEnvio Armar(IEnumerable<string>? principal = null)
        {
            if (!Activo)
                return new CronogramaCorreoEnvio { Enviar = false };

            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var para = Tomar(vistos, (PrincipalActivo ? principal : null) ?? Enumerable.Empty<string>(), Para);
            var cc = Tomar(vistos, Cc);
            var cco = Tomar(vistos, Cco);

            if (para.Count == 0)
            {
                if (cc.Count > 0) (para, cc) = (cc, new List<string>());
                else (para, cco) = (cco, new List<string>());
            }

            return new CronogramaCorreoEnvio { Enviar = para.Count > 0, Para = para, Cc = cc, Cco = cco };
        }

        private static List<string> Tomar(HashSet<string> vistos, params IEnumerable<string>[] listas)
        {
            var resultado = new List<string>();
            foreach (var correo in listas.SelectMany(l => l))
            {
                if (string.IsNullOrWhiteSpace(correo)) continue;
                var limpio = correo.Trim();
                if (vistos.Add(limpio)) resultado.Add(limpio);
            }
            return resultado;
        }

        /// <summary>Reparte las filas (recepción, correo) que devuelve la base.</summary>
        public static CronogramaCorreoListaEnvio Desde(bool activo, bool principalActivo, IEnumerable<(string Recepcion, string Email)> filas)
        {
            var lista = filas.ToList();
            return new CronogramaCorreoListaEnvio
            {
                Activo = activo,
                PrincipalActivo = principalActivo,
                Para = lista.Where(f => f.Recepcion == CronogramaCorreoRecepciones.Para).Select(f => f.Email).ToList(),
                Cc = lista.Where(f => f.Recepcion == CronogramaCorreoRecepciones.Cc).Select(f => f.Email).ToList(),
                Cco = lista.Where(f => f.Recepcion == CronogramaCorreoRecepciones.Cco).Select(f => f.Email).ToList(),
            };
        }
    }
}
