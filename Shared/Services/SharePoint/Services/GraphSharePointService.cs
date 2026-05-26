using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.SharePoint.Dtos;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Options;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Abril_Backend.Shared.Services.SharePoint.Services
{
    public class GraphSharePointService : IGraphSharePointService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        // Cache por sitio (siteId) y por (siteId|libraryName). Los IDs no cambian en vida de la app.
        private static readonly ConcurrentDictionary<string, string> _siteIdCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, string> _driveIdCache = new(StringComparer.OrdinalIgnoreCase);
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

            var result = await UploadStreamAsync(accessToken, url, fileStream, contentType);
            return result?.WebUrl;
        }

        // ── SharePoint biblioteca compartida (aplicación) ────────────────────

        public async Task<SharePointUploadResultDto?> UploadToSharePointLibraryAsync(
            SharePointSiteRef site,
            string libraryName,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream")
        {
            var token   = await GetAppTokenAsync();
            var siteId  = await EnsureSiteIdAsync(token, site);
            var driveId = await EnsureLibraryDriveAsync(token, siteId, libraryName);

            var fullPath = $"{folderPath.Trim('/')}/{fileName}";
            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{EscapePath(fullPath)}:/content";

            return await UploadStreamAsync(token, url, fileStream, contentType);
        }

        // ── Helpers compartidos ──────────────────────────────────────────────

        private async Task<SharePointUploadResultDto?> UploadStreamAsync(
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
                if ((int)response.StatusCode == 423)
                {
                    throw new AbrilException(
                        "No se puede guardar el archivo porque ya existe un archivo con el mismo nombre que está abierto o en uso. " +
                        "Por favor, cierre la pestaña o descarga del archivo en uso e intente de nuevo.",
                        StatusCodes.Status409Conflict);
                }

                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Upload falló [{(int)response.StatusCode}]: {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var webUrl = doc.RootElement.TryGetProperty("webUrl", out var webUrlProp)
                ? webUrlProp.GetString()
                : null;

            var itemId = doc.RootElement.TryGetProperty("id", out var idProp)
                ? idProp.GetString()
                : null;

            return new SharePointUploadResultDto { WebUrl = webUrl, ItemId = itemId };
        }

        private async Task<string> EnsureSiteIdAsync(string token, SharePointSiteRef site)
        {
            if (_siteIdCache.TryGetValue(site.CacheKey, out var cached)) return cached;

            await _lock.WaitAsync();
            try
            {
                if (_siteIdCache.TryGetValue(site.CacheKey, out cached)) return cached;

                var sitePath = "/" + site.SitePath.Trim('/');

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await client.GetAsync(
                    $"https://graph.microsoft.com/v1.0/sites/{site.Hostname}:{sitePath}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var siteId = doc.RootElement.GetProperty("id").GetString()
                    ?? throw new InvalidOperationException("No se pudo obtener el site ID de SharePoint.");

                _siteIdCache[site.CacheKey] = siteId;
                return siteId;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<string> EnsureLibraryDriveAsync(string token, string siteId, string libraryName)
        {
            var cacheKey = $"{siteId}|{libraryName}";
            if (_driveIdCache.TryGetValue(cacheKey, out var cached)) return cached;

            await _lock.WaitAsync();
            try
            {
                if (_driveIdCache.TryGetValue(cacheKey, out cached)) return cached;

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string driveId;

                // Si libraryName es un GUID, resolvemos directamente por list ID (más eficiente).
                if (Guid.TryParse(libraryName, out _))
                {
                    var driveResponse = await client.GetAsync(
                        $"https://graph.microsoft.com/v1.0/sites/{siteId}/lists/{libraryName}/drive");
                    driveResponse.EnsureSuccessStatusCode();

                    var driveJson = await driveResponse.Content.ReadAsStringAsync();
                    using var driveDoc = JsonDocument.Parse(driveJson);
                    driveId = driveDoc.RootElement.GetProperty("id").GetString()
                        ?? throw new InvalidOperationException(
                            $"No se pudo obtener el drive ID de la biblioteca con GUID '{libraryName}'.");
                }
                else
                {
                    var drivesResponse = await client.GetAsync(
                        $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives");
                    drivesResponse.EnsureSuccessStatusCode();

                    var drivesJson = await drivesResponse.Content.ReadAsStringAsync();
                    using var drivesDoc = JsonDocument.Parse(drivesJson);

                    string? found = null;
                    foreach (var drive in drivesDoc.RootElement.GetProperty("value").EnumerateArray())
                    {
                        if (drive.GetProperty("name").GetString() == libraryName)
                        {
                            found = drive.GetProperty("id").GetString()!;
                            break;
                        }
                    }

                    if (found is null)
                        throw new InvalidOperationException(
                            $"La biblioteca '{libraryName}' no existe en el sitio de SharePoint. " +
                            $"Créela manualmente desde el sitio antes de subir archivos.");

                    driveId = found;
                }

                _driveIdCache[cacheKey] = driveId;
                return driveId;
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

        public async Task<byte[]> DownloadFromSharePointAsync(SharePointSiteRef site, string webUrl)
        {
            var token  = await GetAppTokenAsync();
            var siteId = await EnsureSiteIdAsync(token, site);

            // La webUrl tiene el formato:
            // https://{hostname}/{sitePath}/{libraryName}/{ruta/al/archivo.pdf}
            var sitePath = site.SitePath.Trim('/');
            var baseUrl  = $"https://{site.Hostname}/{sitePath}/";

            if (!webUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"La URL del archivo no pertenece al sitio SharePoint indicado ({baseUrl}): {webUrl}");

            var afterBase       = Uri.UnescapeDataString(webUrl.Substring(baseUrl.Length));
            var firstSlash      = afterBase.IndexOf('/');
            if (firstSlash < 0)
                throw new InvalidOperationException($"No se pudo extraer la ruta del archivo: {webUrl}");

            var libraryName      = afterBase[..firstSlash];
            var pathWithinDrive  = afterBase[(firstSlash + 1)..];

            var driveId = await EnsureLibraryDriveAsync(token, siteId, libraryName);

            var escapedPath  = EscapePath(pathWithinDrive);
            var downloadUrl  = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{escapedPath}:/content";

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync(downloadUrl);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"No se pudo descargar el archivo de SharePoint [{(int)response.StatusCode}]: {error}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<byte[]> DownloadAsPdfFromSharePointAsync(SharePointSiteRef site, string libraryName, string itemId)
        {
            var token   = await GetAppTokenAsync();
            var siteId  = await EnsureSiteIdAsync(token, site);
            var driveId = await EnsureLibraryDriveAsync(token, siteId, libraryName);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Graph devuelve 406 ("InputFormatNotSupported") si se pide ?format=pdf sobre un PDF.
            // Primero inspeccionamos el item: si ya es PDF se descarga tal cual; si no, se solicita
            // la conversión a PDF.
            var metaUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/items/{itemId}?$select=name,file";
            var metaResp = await client.GetAsync(metaUrl);
            if (!metaResp.IsSuccessStatusCode)
            {
                var metaErr = await metaResp.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"No se pudo leer metadatos del archivo en SharePoint [{(int)metaResp.StatusCode}]: {metaErr}");
            }

            using var metaJson = JsonDocument.Parse(await metaResp.Content.ReadAsStringAsync());
            var fileName = metaJson.RootElement.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var mimeType = metaJson.RootElement.TryGetProperty("file", out var fileEl)
                           && fileEl.TryGetProperty("mimeType", out var mimeEl)
                ? mimeEl.GetString()
                : null;

            var alreadyPdf =
                (fileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ?? false) ||
                string.Equals(mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);

            var downloadUrl = alreadyPdf
                ? $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/items/{itemId}/content"
                : $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/items/{itemId}/content?format=pdf";

            var response = await client.GetAsync(downloadUrl);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"No se pudo convertir el archivo a PDF [{(int)response.StatusCode}]: {error}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<Dictionary<string, byte[]>> DownloadMultipleAsPdfFromSharePointAsync(
            SharePointSiteRef site,
            string libraryName,
            IReadOnlyList<(string ItemId, bool AlreadyPdf)> items)
        {
            var result = new Dictionary<string, byte[]>();
            if (items.Count == 0) return result;

            var token   = await GetAppTokenAsync();
            var siteId  = await EnsureSiteIdAsync(token, site);
            var driveId = await EnsureLibraryDriveAsync(token, siteId, libraryName);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Graph $batch acepta hasta 20 sub-requests por llamada. Para >20 archivos se trocea.
            const int BatchSize = 20;
            for (int offset = 0; offset < items.Count; offset += BatchSize)
            {
                var slice = items.Skip(offset).Take(BatchSize).ToList();

                // Construir el cuerpo del batch. El "id" de cada sub-request es el itemId mismo
                // para luego correlacionar fácilmente la respuesta.
                var requests = slice.Select(it => new
                {
                    id     = it.ItemId,
                    method = "GET",
                    url    = it.AlreadyPdf
                        ? $"/sites/{siteId}/drives/{driveId}/items/{it.ItemId}/content"
                        : $"/sites/{siteId}/drives/{driveId}/items/{it.ItemId}/content?format=pdf",
                }).ToArray();

                var batchBody = JsonSerializer.Serialize(new { requests });
                using var content = new StringContent(batchBody, Encoding.UTF8, "application/json");

                var batchResp = await client.PostAsync("https://graph.microsoft.com/v1.0/$batch", content);
                if (!batchResp.IsSuccessStatusCode)
                {
                    var err = await batchResp.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        $"Falló la descarga en batch desde SharePoint [{(int)batchResp.StatusCode}]: {err}");
                }

                using var doc = JsonDocument.Parse(await batchResp.Content.ReadAsStringAsync());

                // Para sub-responses que son 302 (Graph redirige a una URL pre-firmada del CDN)
                // hacemos los GETs en paralelo para no perder tiempo.
                var redirectFetches = new List<Task>();

                foreach (var sub in doc.RootElement.GetProperty("responses").EnumerateArray())
                {
                    var id     = sub.GetProperty("id").GetString()!;
                    var status = sub.GetProperty("status").GetInt32();

                    if (status >= 200 && status < 300)
                    {
                        // Cuerpo binario viene base64 dentro del campo "body"
                        if (sub.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String)
                        {
                            result[id] = Convert.FromBase64String(body.GetString()!);
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                $"Respuesta de batch para item {id} no contiene cuerpo binario.");
                        }
                    }
                    else if (status == 301 || status == 302)
                    {
                        // Seguir el redirect a la URL pre-firmada
                        var location = sub.GetProperty("headers").GetProperty("Location").GetString()!;
                        var capturedId = id;
                        redirectFetches.Add(Task.Run(async () =>
                        {
                            using var redirectClient = _httpClientFactory.CreateClient();
                            var redirectResp = await redirectClient.GetAsync(location);
                            redirectResp.EnsureSuccessStatusCode();
                            var bytes = await redirectResp.Content.ReadAsByteArrayAsync();
                            lock (result) { result[capturedId] = bytes; }
                        }));
                    }
                    else
                    {
                        var errPayload = sub.TryGetProperty("body", out var errBody)
                            ? errBody.ToString()
                            : "(sin cuerpo)";
                        throw new InvalidOperationException(
                            $"Sub-request {id} falló con status {status}: {errPayload}");
                    }
                }

                if (redirectFetches.Count > 0)
                    await Task.WhenAll(redirectFetches);
            }

            return result;
        }

        public async Task<(byte[] Content, string? ContentType)> DownloadFromOneDriveByItemIdAsync(string driveId, string itemId)
        {
            var token = await GetAppTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{itemId}/content";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"No se pudo descargar el archivo de OneDrive [{(int)response.StatusCode}]: {error}");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType;
            return (bytes, contentType);
        }

        public async Task<List<OneDriveFolderItemDto>> GetFolderChildrenAsync(
            string driveId,
            string folderPath,
            IEnumerable<string>? excludedFolderNames = null)
        {
            var token = await GetAppTokenAsync();
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var escapedPath = EscapePath(folderPath.Trim('/'));
            var url = $"https://graph.microsoft.com/v1.0/drives/{driveId}/root:/{escapedPath}:/children?$select=id,name,folder,file";

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"No se pudo listar la carpeta de OneDrive [{(int)response.StatusCode}]: {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var excluded = excludedFolderNames?
                .Select(s => s.ToUpperInvariant())
                .ToHashSet() ?? [];

            var items = new List<OneDriveFolderItemDto>();
            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                var name = item.GetProperty("name").GetString()!;
                var isFolder = item.TryGetProperty("folder", out _);

                if (isFolder && excluded.Contains(name.ToUpperInvariant()))
                    continue;

                items.Add(new OneDriveFolderItemDto
                {
                    Id = item.GetProperty("id").GetString()!,
                    Name = name,
                    IsFolder = isFolder
                });
            }

            return items;
        }

        public async Task<(string Id, string Name)?> FindContractorFolderAsync(SharePointSiteRef site, string libraryName, string ruc)
        {
            var token   = await GetAppTokenAsync();
            var siteId  = await EnsureSiteIdAsync(token, site);
            var driveId = await EnsureLibraryDriveAsync(token, siteId, libraryName);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root/children" +
                      "?$select=id,name,folder&$top=500";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var prefix = ruc + " - ";
            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                if (!item.TryGetProperty("folder", out _)) continue;

                var name = item.GetProperty("name").GetString() ?? string.Empty;
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var id = item.GetProperty("id").GetString()!;
                    return (id, name);
                }
            }

            return null;
        }

        public async Task RenameFolderInLibraryAsync(SharePointSiteRef site, string libraryName, string folderId, string newName)
        {
            var token   = await GetAppTokenAsync();
            var siteId  = await EnsureSiteIdAsync(token, site);
            var driveId = await EnsureLibraryDriveAsync(token, siteId, libraryName);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url     = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/items/{folderId}";
            var payload = JsonSerializer.Serialize(new { name = newName });
            var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PatchAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"No se pudo renombrar la carpeta en SharePoint [{(int)response.StatusCode}]: {error}");
            }
        }

        public async Task DeleteFromSharePointLibraryAsync(SharePointSiteRef site, string libraryName, string itemId)
        {
            var token   = await GetAppTokenAsync();
            var siteId  = await EnsureSiteIdAsync(token, site);
            var driveId = await EnsureLibraryDriveAsync(token, siteId, libraryName);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/items/{itemId}";
            var response = await client.DeleteAsync(url);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"No se pudo eliminar el archivo de SharePoint [{(int)response.StatusCode}]: {error}");
            }
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
