using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Abril_Backend.Shared.Services.SharePoint.Services
{
    public class GraphSharePointService : IGraphSharePointService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        // Cache: el drive ID de una biblioteca no cambia durante la vida de la app.
        private static readonly Dictionary<string, string> _driveIdCache = new(StringComparer.OrdinalIgnoreCase);
        private static string? _cachedSiteId;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public GraphSharePointService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        // ── OneDrive personal (delegado) ─────────────────────────────────────

        public async Task<string?> UploadToOneDriveAsync(
            string accessToken,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream")
        {
            var itemPath = $"{folderPath.Trim('/')}/{fileName}";
            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{itemPath}:/content";

            return await UploadStreamAsync(accessToken, url, fileStream, contentType);
        }

        // ── SharePoint biblioteca compartida (aplicación) ────────────────────

        public async Task<string?> UploadToSharePointLibraryAsync(
            string libraryName,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream")
        {
            var token   = await GetAppTokenAsync();
            var siteId  = await EnsureSiteIdAsync(token);
            var driveId = await EnsureLibraryDriveAsync(token, siteId, libraryName);

            var fullPath = $"{folderPath.Trim('/')}/{fileName}";
            // Graph crea subcarpetas automáticamente al subir con ruta completa
            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{EscapePath(fullPath)}:/content";

            return await UploadStreamAsync(token, url, fileStream, contentType);
        }

        // ── Helpers compartidos ──────────────────────────────────────────────

        private async Task<string?> UploadStreamAsync(
            string token,
            string uploadUrl,
            Stream fileStream,
            string contentType)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resolved = ResolveContentType(contentType);
            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(resolved.Split(';')[0].Trim());

            var response = await client.PutAsync(uploadUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Upload falló [{(int)response.StatusCode}]: {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("webUrl", out var webUrl)
                ? webUrl.GetString()
                : null;
        }

        private async Task<string> EnsureSiteIdAsync(string token)
        {
            if (_cachedSiteId is not null) return _cachedSiteId;

            await _lock.WaitAsync();
            try
            {
                if (_cachedSiteId is not null) return _cachedSiteId;

                var hostname = _configuration["SharePoint:Hostname"] ?? "abrilinmob.sharepoint.com";
                var sitePath = _configuration["SharePoint:SitePath"] ?? "/sites/CostosyPresupuestos";

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await client.GetAsync(
                    $"https://graph.microsoft.com/v1.0/sites/{hostname}:{sitePath}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                _cachedSiteId = doc.RootElement.GetProperty("id").GetString()
                    ?? throw new InvalidOperationException("No se pudo obtener el site ID de SharePoint.");

                return _cachedSiteId;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<string> EnsureLibraryDriveAsync(string token, string siteId, string libraryName)
        {
            if (_driveIdCache.TryGetValue(libraryName, out var cached)) return cached;

            await _lock.WaitAsync();
            try
            {
                if (_driveIdCache.TryGetValue(libraryName, out cached)) return cached;

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Buscar entre las bibliotecas existentes del sitio
                var drivesResponse = await client.GetAsync(
                    $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives");
                drivesResponse.EnsureSuccessStatusCode();

                var drivesJson = await drivesResponse.Content.ReadAsStringAsync();
                using var drivesDoc = JsonDocument.Parse(drivesJson);

                foreach (var drive in drivesDoc.RootElement.GetProperty("value").EnumerateArray())
                {
                    if (drive.GetProperty("name").GetString() == libraryName)
                    {
                        var foundId = drive.GetProperty("id").GetString()!;
                        _driveIdCache[libraryName] = foundId;
                        return foundId;
                    }
                }

                // La biblioteca no existe — lanzar error descriptivo.
                // Créela manualmente en SharePoint antes de usar este endpoint:
                //   https://abrilinmob.sharepoint.com/sites/CostosyPresupuestos
                //   → Nuevo → Biblioteca de documentos → nombre exacto: libraryName
                throw new InvalidOperationException(
                    $"La biblioteca '{libraryName}' no existe en el sitio de SharePoint. " +
                    $"Créela manualmente desde el sitio antes de subir archivos.");
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<string> GetAppTokenAsync()
        {
            var tenantId     = _configuration["AzureAd:TenantId"]     ?? throw new InvalidOperationException("AzureAd:TenantId no configurado.");
            var clientId     = _configuration["AzureAd:ClientId"]     ?? throw new InvalidOperationException("AzureAd:ClientId no configurado.");
            var clientSecret = _configuration["AzureAd:ClientSecret"] ?? throw new InvalidOperationException("AzureAd:ClientSecret no configurado.");

            var client = _httpClientFactory.CreateClient();
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "client_credentials",
                ["client_id"]     = clientId,
                ["client_secret"] = clientSecret,
                ["scope"]         = "https://graph.microsoft.com/.default",
            });

            var response = await client.PostAsync(
                $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token", body);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("No se pudo obtener el token de aplicación.");
        }

        private static string EscapePath(string path)
            => string.Join("/", path.Split('/').Select(Uri.EscapeDataString));

        private static string ResolveContentType(string contentType)
        {
            if (!string.IsNullOrWhiteSpace(contentType) && contentType != "application/octet-stream")
                return contentType;
            return "application/octet-stream";
        }
    }
}
