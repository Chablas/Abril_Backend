using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Abril_Backend.Features.Habilitacion.Application.Services
{
    public class SharePointHabService : ISharePointHabService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SharePointHabService> _logger;

        // keyed by libraryId (o "default" para el drive predeterminado del sitio)
        private readonly ConcurrentDictionary<string, string> _driveIdCache = new();
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

        public async Task<string?> GetDownloadUrlAsync(string archivoUrl, string? libraryContexto = null)
        {
            var trimmed = archivoUrl?.Trim() ?? string.Empty;
            if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("GetDownloadUrlAsync: URL absoluta detectada, devolviendo como está.");
                return trimmed;
            }

            var siteId = ResolverSiteId(libraryContexto ?? archivoUrl ?? string.Empty);
            if (string.IsNullOrWhiteSpace(siteId)) return null;

            var token = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return null;

            var libraryId = ResolverLibraryId(libraryContexto ?? archivoUrl ?? string.Empty);
            string? driveId;
            string path;

            if (libraryId != null)
            {
                driveId = await GetDriveIdAsync(siteId, token, libraryId);
                path = NormalizarPath(archivoUrl!);
            }
            else
            {
                // Extraer primer segmento como nombre de librería (ej: "PETSAbril2026/archivo.docx" → "PETSAbril2026")
                var normalizado = (archivoUrl ?? string.Empty).Trim().TrimStart('/');
                var slashIdx = normalizado.IndexOf('/');
                var libraryName = slashIdx > 0 ? normalizado[..slashIdx] : normalizado;
                var pathDentro   = slashIdx > 0 ? normalizado[(slashIdx + 1)..] : string.Empty;

                if (string.IsNullOrWhiteSpace(libraryName))
                {
                    _logger.LogWarning("GetDownloadUrlAsync: no se pudo extraer nombre de librería de '{Path}'", archivoUrl);
                    return null;
                }

                driveId = await GetDriveIdByNameAsync(siteId, token, libraryName);
                path = pathDentro;
            }

            if (string.IsNullOrWhiteSpace(driveId)) return null;
            if (string.IsNullOrWhiteSpace(path)) return null;

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
            var siteId = ResolverSiteId(contexto);
            if (string.IsNullOrWhiteSpace(siteId))
                throw new AbrilException("SharePoint no está configurado.", 500);

            var token = await GetAccessTokenAsync()
                ?? throw new AbrilException("No se pudo obtener token de Microsoft Graph.", 500);

            var libraryId = ResolverLibraryId(contexto);
            var driveId = await GetDriveIdAsync(siteId, token, libraryId)
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

        public async Task<string> SubirArchivoEnRutaAsync(Stream fileStream, string fileName, string libraryContexto, string carpetaPath)
        {
            var siteId = ResolverSiteId(libraryContexto);
            if (string.IsNullOrWhiteSpace(siteId))
                throw new AbrilException("SharePoint no está configurado.", 500);

            var token = await GetAccessTokenAsync()
                ?? throw new AbrilException("No se pudo obtener token de Microsoft Graph.", 500);

            var libraryId = ResolverLibraryId(libraryContexto);
            var driveId = await GetDriveIdAsync(siteId, token, libraryId)
                ?? throw new AbrilException("No se pudo resolver el drive de SharePoint.", 500);

            var fechaPrefix = DateTime.UtcNow.ToString("yyyyMMdd");
            var fileNameLimpio = SanitizarNombreArchivo(fileName);
            var path = $"{carpetaPath.Trim('/')}/{fechaPrefix}_{fileNameLimpio}";

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
                throw new AbrilException($"Error al subir archivo a SharePoint ({(int)response.StatusCode}).", 502);
            }

            return path;
        }

        private async Task<string?> GetDriveIdByNameAsync(string siteId, string token, string libraryName)
        {
            if (string.IsNullOrWhiteSpace(libraryName)) return null;

            var cacheKey = $"name:{libraryName}";
            if (_driveIdCache.TryGetValue(cacheKey, out var cached)) return cached;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Drives list GET falló ({Status}) para sitio {Site}", response.StatusCode, siteId);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty("value", out var drives)) return null;

            foreach (var drive in drives.EnumerateArray())
            {
                var name = drive.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.Equals(name, libraryName, StringComparison.OrdinalIgnoreCase))
                {
                    var driveId = drive.GetProperty("id").GetString()!;
                    _driveIdCache[cacheKey] = driveId;
                    _logger.LogInformation(
                        "SharePoint drive por nombre '{Name}' resuelto — id: {DriveId}",
                        libraryName, driveId);
                    return driveId;
                }
            }

            _logger.LogWarning("No se encontró drive con nombre '{Name}' en sitio {Site}", libraryName, siteId);
            return null;
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

        private string ResolverSiteId(string contexto)
        {
            var c = (contexto ?? string.Empty).ToLowerInvariant();
            if (c.Contains("interconsulta") || c.Contains("lectura-emo"))
                return _configuration["SharePoint:Sites:SSOMAApps:SiteId"]!;
            return _configuration["SharePoint:Sites:SSOMAApps:SiteId"]!;
        }

        private string? ResolverLibraryId(string contexto)
        {
            var c = (contexto ?? string.Empty).ToLowerInvariant();
            if (c.Contains("trabajadores"))  return _configuration["SharePoint:Sites:Habilitacion:TrabajadoresLibraryId"];
            if (c.Contains("empresas"))      return _configuration["SharePoint:Sites:Habilitacion:EmpresaLibraryId"];
            if (c.Contains("equipos"))       return _configuration["SharePoint:Sites:Habilitacion:EquiposLibraryId"];
            if (c.Contains("sctr"))          return _configuration["SharePoint:Sites:SSOMAApps:SctrLibraryId"];
            if (c.Contains("emo-aptitud"))   return _configuration["SharePoint:Sites:SSOMAApps:AptitudesLibraryId"];
            if (c.Contains("emo-completo"))  return _configuration["SharePoint:Sites:SSOMAApps:EMOSLibraryId"];
            if (c.Contains("interconsulta")) return _configuration["SharePoint:Sites:SSOMAApps:EmoInterconsultasLibraryId"];
            if (c.Contains("lectura-emo"))      return _configuration["SharePoint:Sites:SSOMAApps:LecturaEmosLibraryId"];
            if (c.Contains("paso-evidencias")) return _configuration["SharePoint:Sites:SSOMAApps:PasoEvidenciasLibraryId"];
            if (c.Contains("rac-pdf"))         return _configuration["SharePoint:Sites:SSOMAApps:RacPdfLibraryId"];
            if (c.Contains("rac-fotos"))       return _configuration["SharePoint:Sites:SSOMAApps:RacFotosLibraryId"];
            if (c.Contains("rac-firmas"))      return _configuration["SharePoint:Sites:SSOMAApps:RacFirmasLibraryId"];
            if (c.Contains("opt-firmas"))         return _configuration["SharePoint:Sites:SSOMAApps:OptFirmasLibraryId"];
            if (c.Contains("inspeccion-fotos")) return _configuration["SharePoint:Sites:SSOMAApps:InspeccionesLibraryId"];
            if (c.Contains("inspeccion-firmas")) return _configuration["SharePoint:Sites:SSOMAApps:InspeccionesLibraryId"];
            if (c.Contains("penalidad-pdf"))   return _configuration["SharePoint:Sites:SSOMAApps:PenalidadPdfLibraryId"];
            return null;
        }

        private async Task<string?> GetDriveIdAsync(string siteId, string token, string? libraryId = null)
        {
            var cacheKey = libraryId ?? "default";
            if (_driveIdCache.TryGetValue(cacheKey, out var cached)) return cached;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var url = string.IsNullOrWhiteSpace(libraryId)
                ? $"https://graph.microsoft.com/v1.0/sites/{siteId}/drive"
                : $"https://graph.microsoft.com/v1.0/sites/{siteId}/lists/{libraryId}/drive";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Drive GET falló ({Status}) para sitio {Site} | library {Library}",
                    response.StatusCode, siteId, libraryId ?? "(default)");
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var driveId     = doc.RootElement.GetProperty("id").GetString()!;
            var driveName   = doc.RootElement.TryGetProperty("name",   out var n) ? n.GetString() : "(sin nombre)";
            var driveWebUrl = doc.RootElement.TryGetProperty("webUrl", out var w) ? w.GetString() : "(sin webUrl)";
            _logger.LogInformation(
                "SharePoint drive resuelto — id: {DriveId} | name: {Name} | webUrl: {WebUrl} | library: {Library}",
                driveId, driveName, driveWebUrl, libraryId ?? "(default)");

            _driveIdCache[cacheKey] = driveId;
            return driveId;
        }
    }
}
