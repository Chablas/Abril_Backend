using System.Net;
using System.Net.Sockets;

namespace Abril_Backend.Shared.Services.Convivir.Configuration
{
    /// <summary>
    /// Sección <c>Convivir</c> del appsettings: la app móvil de propietarios (Convivir Abril).
    /// Los plazos son solo el valor con el que NACE cada fila: lo que manda después es la columna
    /// <c>expires_at</c> de <c>user_session</c> / <c>user_password_token</c>, así que una sesión o
    /// un enlace se alarga (o se corta) con un UPDATE, sin tocar código ni el token del teléfono.
    /// </summary>
    public class ConvivirOptions
    {
        /// <summary>
        /// Base pública del backend a la que apunta el enlace del correo
        /// (<c>{UrlPublica}/api/v1/convivir/auth/abrir?token=...</c>). Si falta, se usa
        /// <c>BackendSettings:PublicUrl</c>. En desarrollo va con la IP de la PC en la red (no
        /// localhost), porque el enlace se abre desde el teléfono: <c>http://{ip}:5236</c>.
        /// </summary>
        public string? UrlPublica { get; set; }

        /// <summary>
        /// Prefijo del deep link de la app. En la app instalada es el <c>scheme</c> del app.json
        /// (<c>abrilconvivir://</c>); probando con Expo Go es <c>exp://{ip}:8081/--/</c>.
        /// </summary>
        public string EnlaceApp { get; set; } = "abrilconvivir://";

        /// <summary>
        /// En desarrollo, <c>{ip}</c> en <see cref="UrlPublica"/> y <see cref="EnlaceApp"/> es la
        /// IP de la PC en la red, resuelta al arrancar: el router se la cambia cuando quiere y así
        /// no hay que tocar el appsettings. Producción no lo usa.
        /// </summary>
        public const string MarcadorIp = "{ip}";

        public static void ReemplazarIpDeLaPc(ConvivirOptions options)
        {
            if (options.UrlPublica?.Contains(MarcadorIp) != true && !options.EnlaceApp.Contains(MarcadorIp))
                return;

            var ip = IpDeLaPc();
            options.UrlPublica = options.UrlPublica?.Replace(MarcadorIp, ip);
            options.EnlaceApp = options.EnlaceApp.Replace(MarcadorIp, ip);
        }

        /// <summary>
        /// La IP de la interfaz por la que la PC sale a la red, la misma que anuncia Metro. Conectar
        /// un socket UDP no manda nada: solo elige la interfaz.
        /// </summary>
        private static string IpDeLaPc()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "localhost";
            }
            catch (SocketException)
            {
                return "localhost";
            }
        }

        /// <summary>Duración con la que nace una sesión de la app.</summary>
        public int SesionDias { get; set; } = 90;

        /// <summary>Vigencia del enlace de invitación (crear la contraseña por primera vez).</summary>
        public int InvitacionHoras { get; set; } = 168;

        /// <summary>Vigencia del enlace de «¿La olvidaste?».</summary>
        public int RecuperacionHoras { get; set; } = 1;
    }
}
