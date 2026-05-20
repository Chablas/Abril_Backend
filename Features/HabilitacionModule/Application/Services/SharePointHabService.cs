using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Abril_Backend.Features.Habilitacion.Application.Services
{
    public class SharePointHabService : ISharePointHabService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SharePointHabService> _logger;

        private string? _cachedDriveId;
        private string? _cachedToken;
        private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

        private readonly Lazy<HttpClient> _noRedirectClient = new(() =>
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }));

        public SharePointHabService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<SharePointHabService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> GetDownloadUrlAsync(string archivoUrl)
        {
            var trimmed = archivoUrl?.Trim() ?? string.Empty;
            if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("GetDownloadUrlAsync: URL absoluta detectada, devolviendo como está.");
                return trimmed;
            }

            var siteId = _configuration["SharePoint:SiteId"];
            if (string.IsNullOrWhiteSpace(siteId)) return null;

            var token = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return null;

            var driveId = await GetDriveIdAsync(siteId, token);
            if (string.IsNullOrWhiteSpace(driveId)) return null;

            var path = NormalizarPath(archivoUrl!);
            var encoded = Uri.EscapeDataString(path).Replace("%2F", "/");
            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encoded}:/content";

            var client = _noRedirectClient.Value;
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Found ||
                response.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
            {
                var location = response.Headers.Location?.ToString();
                if (!string.IsNullOrWhiteSpace(location))
                    return location;

                _logger.LogWarning("SharePoint /content devolvió redirect sin Location para {Path}", path);
                return null;
            }

            _logger.LogWarning("SharePoint /content GET falló ({Status}) para {Path}", response.StatusCode, path);
            return null;
        }

        public async Task<string> SubirArchivoAsync(Stream fileStream, string fileName, string contexto)
        {
            var siteId = _configuration["SharePoint:SiteId"];
            if (string.IsNullOrWhiteSpace(siteId))
                throw new AbrilException("SharePoint no está configurado.", 500);

            var token = await GetAccessTokenAsync()
                ?? throw new AbrilException("No se pudo obtener token de Microsoft Graph.", 500);

            var driveId = await GetDriveIdAsync(siteId, token)
                ?? throw new AbrilException("No se pudo resolver el drive de SharePoint.", 500);

            var contextoLimpio = (contexto ?? string.Empty).Trim().Trim('/');
            if (contextoLimpio.StartsWith("habilitacion/", StringComparison.OrdinalIgnoreCase))
                contextoLimpio = contextoLimpio["habilitacion/".Length..].TrimStart('/');
            var fechaPrefix = DateTime.UtcNow.ToString("yyyyMMdd");
            var fileNameLimpio = SanitizarNombreArchivo(fileName);

            var path = string.IsNullOrEmpty(contextoLimpio)
                ? $"habilitacion/{fechaPrefix}_{fileNameLimpio}"
                : $"habilitacion/{contextoLimpio}/{fechaPrefix}_{fileNameLimpio}";

            var encoded = Uri.EscapeDataString(path).Replace("%2F", "/");
            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{encoded}:/content";

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await client.PutAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("SharePoint upload falló ({Status}) para {Path}: {Body}",
                    response.StatusCode, path, body);
                throw new AbrilException(
                    $"Error al subir archivo a SharePoint ({(int)response.StatusCode}).",
                    502);
            }

            return path;
        }

        private static string NormalizarPath(string rawPath)
        {
            var path = rawPath.Trim().TrimStart('/');
            const string prefix = "habilitacion/";
            while (path.StartsWith(prefix + prefix, StringComparison.OrdinalIgnoreCase))
                path = path[prefix.Length..];
            return path;
        }

        private static string SanitizarNombreArchivo(string fileName)
        {
            var nombre = Path.GetFileName(fileName ?? string.Empty);
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            nombre = nombre.Replace(' ', '_');
            return string.IsNullOrWhiteSpace(nombre) ? "archivo" : nombre;
        }

        private async Task<string?> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
                return _cachedToken;

            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            var secret = _configuration["AzureAd:ClientSecret"];

            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
                return null;

            var client = _httpClientFactory.CreateClient();
            var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", secret),
                new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default"),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
            });

            var response = await client.PostAsync(tokenUrl, form);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token Graph falló ({Status})", response.StatusCode);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var token = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 3600;

            _cachedToken = token;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return token;
        }

        private async Task<string?> GetDriveIdAsync(string siteId, string token)
        {
            if (!string.IsNullOrEmpty(_cachedDriveId)) return _cachedDriveId;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drive";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Drive GET falló ({Status}) para sitio {Site}", response.StatusCode, siteId);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            _cachedDriveId = doc.RootElement.GetProperty("id").GetString();

            var driveName   = doc.RootElement.TryGetProperty("name",   out var n) ? n.GetString() : "(sin nombre)";
            var driveWebUrl = doc.RootElement.TryGetProperty("webUrl", out var w) ? w.GetString() : "(sin webUrl)";
            _logger.LogInformation("SharePoint drive resuelto — id: {DriveId} | name: {Name} | webUrl: {WebUrl}",
                _cachedDriveId, driveName, driveWebUrl);

            return _cachedDriveId;
        }
    }
}
