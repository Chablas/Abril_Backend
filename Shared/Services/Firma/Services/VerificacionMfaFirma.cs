using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Firma.Interfaces;
using Abril_Backend.Shared.Services.Graph.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Abril_Backend.Shared.Services.Firma.Services
{
    /// <inheritdoc cref="IVerificacionMfaFirma"/>
    /// <remarks>
    /// El tenant está en la licencia gratis con valores predeterminados de seguridad: no hay acceso
    /// condicional ni <c>acrs</c> que pruebe una MFA reciente. Lo que el token sí prueba:
    /// <list type="bullet">
    /// <item>Hubo un inicio de sesión interactivo hace menos de 10 minutos (<c>auth_time</c>, claim
    /// opcional configurado en la app) y el token se emitió enseguida (<c>iat</c>): uno renovado en
    /// silencio más tarde trae un <c>iat</c> nuevo y no sirve.</item>
    /// <item>La sesión incluye MFA (<c>amr</c>, que los tokens v1 traen siempre).</item>
    /// </list>
    /// Lo que NO prueba: que el frontend mandó el claims request que exige el segundo factor. Si un
    /// re-login con <c>prompt=login</c> solo con contraseña conserva el <c>mfa</c> de la sesión (está
    /// por probar), a quien sabe la contraseña del jefe le alcanza con ella. La marca <c>ngcmfa</c>
    /// no ayuda: Microsoft la pone en cualquier inicio de sesión aprobado con Authenticator, sin fecha.
    /// </remarks>
    public class VerificacionMfaFirma : IVerificacionMfaFirma
    {
        /// <summary>Ámbito que la app expone solo para esto (Exponer una API → <c>Firmar</c>).</summary>
        private const string Ambito = "Firmar";

        /// <summary>
        /// Cuánto puede tener el inicio de sesión de Microsoft. Cubre dibujar la firma cuando
        /// faltaba (el 409 reintenta con el mismo token) y aprobar varios seguidos; es el mismo
        /// margen que Microsoft da a una MFA antes de volver a pedirla. Se puede bajar con
        /// <c>FirmaMfa:VentanaMinutos</c>.
        /// </summary>
        private const int VentanaMinutosPorDefecto = 10;

        /// <summary>
        /// Cuánto puede pasar entre el inicio de sesión y la emisión del token: el popup lo emite
        /// segundos después de aprobar en Authenticator. <c>FirmaMfa:EmisionMaxMinutos</c>.
        /// </summary>
        private const int EmisionMaxMinutosPorDefecto = 5;

        /// <summary>Desfase de reloj tolerado entre Entra y el servidor.</summary>
        private static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(2);

        /// <summary>Espera máxima de Graph al buscar la cuenta (el default del HttpClient es 100 s).</summary>
        private static readonly TimeSpan EsperaGraph = TimeSpan.FromSeconds(10);

        // Las claves con que Entra firma los tokens se bajan una vez y se renuevan solas: el
        // ConfigurationManager es thread-safe y se comparte en todo el proceso (uno por tenant).
        private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> Metadatos = new();
        private static readonly JsonWebTokenHandler Handler = new();

        // Descarga forzada de claves cuando llega un kid desconocido. RequestRefresh() solo agenda
        // la renovación en segundo plano (el siguiente GetConfigurationAsync devuelve las mismas),
        // así que se bajan directo, como mucho una vez cada 5 minutos: un token con un kid inventado
        // no puede usarse para martillar login.microsoftonline.com.
        private static readonly TimeSpan PausaDescargaForzada = TimeSpan.FromMinutes(5);
        private static long _ultimaDescargaForzadaTicks;

        private readonly IConfiguration _configuration;
        private readonly IAuthRepository _authRepository;
        private readonly IGraphAppTokenProvider _graphToken;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<VerificacionMfaFirma> _logger;

        public VerificacionMfaFirma(
            IConfiguration configuration,
            IAuthRepository authRepository,
            IGraphAppTokenProvider graphToken,
            IHttpClientFactory httpClientFactory,
            ILogger<VerificacionMfaFirma> logger)
        {
            _configuration = configuration;
            _authRepository = authRepository;
            _graphToken = graphToken;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task VerificarAsync(string? token, int userId, string accion, CancellationToken ct = default)
        {
            // Válvula para una emergencia (p. ej. Microsoft cambia algo y nadie puede firmar): se
            // apaga en el appsettings del servidor sin desplegar. Por defecto se exige. El frontend
            // manda la firma igual cuando no pudo conseguir el token, así que también cubre eso.
            if (!_configuration.GetValue("FirmaMfa:Exigir", true))
            {
                _logger.LogWarning("Firma sin verificación de Microsoft (FirmaMfa:Exigir = false): usuario {UserId}, {Accion}",
                    userId, accion);
                return;
            }

            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Esta firma requiere la verificación de Microsoft.", 403);

            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("Faltan AzureAd:TenantId o AzureAd:ClientId en la configuración.");

            var jwt = await ValidarFirmaDeMicrosoftAsync(token, tenantId, clientId, userId, accion, ct);

            string? Texto(string claim) => jwt.TryGetPayloadValue<string>(claim, out var v) ? v : null;

            // Emitido para este tenant, pedido por la intranet y para el ámbito de firmar: un token
            // de Graph o de otra app no sirve aunque sea de la misma persona.
            var cliente = Texto("appid") ?? Texto("azp");
            var ambitos = (Texto("scp") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!string.Equals(Texto("tid"), tenantId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(cliente, clientId, StringComparison.OrdinalIgnoreCase)
                || !ambitos.Contains(Ambito))
            {
                _logger.LogWarning("Verificación de Microsoft rechazada al firmar: tid {Tid}, cliente {Cliente}, scp {Scp}; usuario {UserId}, {Accion}",
                    Texto("tid"), cliente, Texto("scp"), userId, accion);
                throw new AbrilException("La verificación de Microsoft no es válida. Vuelve a intentarlo.", 403);
            }

            // La misma persona que tiene la sesión de la intranet. El login la reconoce por el correo
            // (Graph mail ?? UPN), así que se compara igual.
            var usuario = await _authRepository.GetUserByIdAsync(userId)
                ?? throw new AbrilException("Usuario no encontrado.", 403);
            var cuenta = Texto("upn") ?? Texto("unique_name") ?? Texto("preferred_username");
            if (!MismoCorreo(cuenta, usuario.Email)
                && !await CuentaTieneCorreoAsync(Texto("oid"), usuario.Email, ct))
            {
                _logger.LogWarning("Verificación de Microsoft con otra cuenta al firmar: {Cuenta} en la sesión de {Email}; {Accion}",
                    cuenta, usuario.Email, accion);
                throw new AbrilException("La verificación de Microsoft se hizo con otra cuenta.", 403);
            }

            // Que la sesión incluye el segundo factor. Sin ningún amr el token no es v1 (manifiesto
            // con requestedAccessTokenVersion 2): falla cerrado, no firma.
            var amr = jwt.Claims.Where(c => c.Type == "amr").Select(c => c.Value).ToList();
            if (!amr.Contains("mfa"))
            {
                if (amr.Count == 0)
                    _logger.LogError("El token de firma no trae amr (¿token v2?). Revisar requestedAccessTokenVersion de la app; {Accion}", accion);
                else
                    _logger.LogWarning("Firma sin MFA en el token: amr {Amr}; usuario {UserId}, {Accion}", string.Join(",", amr), userId, accion);
                throw new AbrilException("La verificación de Microsoft no incluyó el segundo factor (Authenticator). Vuelve a intentarlo.", 403);
            }

            // Y que fue recién: auth_time es cuándo se inició sesión. Un token renovado en silencio
            // conserva el auth_time del login de la mañana.
            if (!jwt.TryGetPayloadValue<long>("auth_time", out var authTime))
            {
                _logger.LogError("El token de firma no trae auth_time: falta el claim opcional en Configuración de token de la app; {Accion}", accion);
                throw new AbrilException("No se pudo comprobar la verificación de Microsoft.", 403);
            }

            var inicio = DateTimeOffset.FromUnixTimeSeconds(authTime);
            var edad = DateTimeOffset.UtcNow - inicio;
            var ventana = TimeSpan.FromMinutes(_configuration.GetValue("FirmaMfa:VentanaMinutos", VentanaMinutosPorDefecto));
            if (edad > ventana || edad < -Tolerancia)
            {
                _logger.LogInformation("Verificación de Microsoft vencida al firmar: inicio {Inicio:o} ({Edad} min); usuario {UserId}, {Accion}",
                    inicio, Math.Round(edad.TotalMinutes, 1), userId, accion);
                throw new AbrilException("La verificación de Microsoft venció. Vuelve a firmar.", 403);
            }

            // El popup emite el token enseguida del inicio de sesión. Si se emitió bastante después,
            // salió en silencio de una sesión que otro inició (p. ej. el login de la intranet o de
            // Outlook del jefe hace 8 minutos): no es la verificación de esta firma. Las dos horas
            // vienen de Entra, así que el reloj del servidor no cuenta.
            var emision = jwt.TryGetPayloadValue<long>("iat", out var iat) ? DateTimeOffset.FromUnixTimeSeconds(iat) : (DateTimeOffset?)null;
            var emisionMax = TimeSpan.FromMinutes(_configuration.GetValue("FirmaMfa:EmisionMaxMinutos", EmisionMaxMinutosPorDefecto));
            if (emision is null || emision.Value - inicio > emisionMax + Tolerancia)
            {
                _logger.LogWarning("Token de firma emitido lejos del inicio de sesión: inicio {Inicio:o}, emitido {Emision:o}; usuario {UserId}, {Accion}",
                    inicio, emision, userId, accion);
                throw new AbrilException("La verificación de Microsoft venció. Vuelve a firmar.", 403);
            }

            _logger.LogInformation("Firma con verificación de Microsoft: usuario {UserId}, cuenta {Cuenta}, inicio {Inicio:o}, emitido {Emision:o}, amr {Amr}, token {Uti}; {Accion}",
                userId, cuenta, inicio, emision, string.Join(",", amr), Texto("uti"), accion);
        }

        /// <summary>Firma, emisor, audiencia y vigencia del token contra las claves públicas de Entra.</summary>
        private async Task<JsonWebToken> ValidarFirmaDeMicrosoftAsync(
            string token, string tenantId, string clientId, int userId, string accion, CancellationToken ct)
        {
            // Metadatos v1: los tokens de la app son v1 (iss sts.windows.net). Las claves son las
            // mismas que las de v2, así que también validan un v2 si algún día se cambia.
            var url = $"https://login.microsoftonline.com/{tenantId}/.well-known/openid-configuration";
            var metadatos = Metadatos.GetOrAdd(tenantId, _ => new ConfigurationManager<OpenIdConnectConfiguration>(
                url, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = true }));

            var claves = (await metadatos.GetConfigurationAsync(ct)).SigningKeys;
            var resultado = await ValidarConAsync(token, tenantId, clientId, claves);

            // Entra rota sus claves: si el kid no está entre las que tenemos, se bajan de nuevo.
            if (!resultado.IsValid && resultado.Exception is SecurityTokenSignatureKeyNotFoundException && TomarDescargaForzada())
            {
                metadatos.RequestRefresh();
                var frescas = await OpenIdConnectConfigurationRetriever.GetAsync(
                    url, new HttpDocumentRetriever { RequireHttps = true }, ct);
                resultado = await ValidarConAsync(token, tenantId, clientId, frescas.SigningKeys);
            }

            if (resultado.IsValid && resultado.SecurityToken is JsonWebToken jwt)
                return jwt;

            _logger.LogWarning(resultado.Exception, "Verificación de Microsoft inválida al firmar: usuario {UserId}, {Accion}", userId, accion);
            throw new AbrilException(resultado.Exception is SecurityTokenExpiredException
                ? "La verificación de Microsoft venció. Vuelve a firmar."
                : "La verificación de Microsoft no es válida. Vuelve a intentarlo.", 403);
        }

        private static Task<TokenValidationResult> ValidarConAsync(
            string token, string tenantId, string clientId, IEnumerable<SecurityKey> claves) =>
            Handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidIssuers = new[]
                {
                    $"https://sts.windows.net/{tenantId}/",
                    $"https://login.microsoftonline.com/{tenantId}/v2.0",
                },
                ValidAudiences = new[] { $"api://{clientId}", clientId },
                IssuerSigningKeys = claves,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ClockSkew = Tolerancia,
            });

        /// <summary>True si toca bajar las claves a la fuerza (y reserva el turno).</summary>
        private static bool TomarDescargaForzada()
        {
            var ahora = DateTime.UtcNow.Ticks;
            var ultima = Interlocked.Read(ref _ultimaDescargaForzadaTicks);
            return ahora - ultima >= PausaDescargaForzada.Ticks
                && Interlocked.CompareExchange(ref _ultimaDescargaForzadaTicks, ahora, ultima) == ultima;
        }

        private static bool MismoCorreo(string? a, string? b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
            && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Para quien tiene el correo distinto del UPN (el login guardó el mail): se busca la cuenta
        /// del token en Graph con el token de aplicación y se compara su mail y su UPN. «Otra cuenta»
        /// solo si Graph la encontró con otros datos (o no existe); si Graph no responde es una falla
        /// del sistema, no del usuario, y sale como 503.
        /// </summary>
        private async Task<bool> CuentaTieneCorreoAsync(string? oid, string email, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(oid) || !Guid.TryParse(oid, out _)) return false;

            using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
            espera.CancelAfter(EsperaGraph);
            try
            {
                var appToken = await _graphToken.GetTokenAsync(espera.Token);
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://graph.microsoft.com/v1.0/users/{oid}?$select=mail,userPrincipalName");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);

                using var response = await _httpClientFactory.CreateClient().SendAsync(request, espera.Token);
                if (response.StatusCode == HttpStatusCode.NotFound) return false;
                response.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(espera.Token));
                string? Campo(string nombre) =>
                    doc.RootElement.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                return MismoCorreo(Campo("mail"), email) || MismoCorreo(Campo("userPrincipalName"), email);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "No se pudo buscar en Graph la cuenta {Oid} para comparar con {Email}", oid, email);
                throw new AbrilException("No se pudo comprobar tu cuenta con Microsoft. Vuelve a intentarlo en unos minutos.", 503);
            }
        }
    }
}
