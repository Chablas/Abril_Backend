using Abril_Backend.Shared.Services.Graph.Dtos;
using Abril_Backend.Shared.Services.Graph.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Abril_Backend.Shared.Services.Graph.Services
{
    public class GraphUserService : IGraphUserService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private const int ChunkSize = 15; // límite del operador 'in' en Graph API

        public GraphUserService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<Dictionary<string, GraphUserProfileDto>> GetUsersByEmailsAsync(List<string> emails)
        {
            if (emails == null || emails.Count == 0)
                return new Dictionary<string, GraphUserProfileDto>();

            var appToken = await GetAppTokenAsync();
            if (string.IsNullOrEmpty(appToken))
            {
                Console.WriteLine("[GraphUserService] No se pudo obtener el token de aplicación.");
                return new Dictionary<string, GraphUserProfileDto>();
            }

            var result = new Dictionary<string, GraphUserProfileDto>(StringComparer.OrdinalIgnoreCase);

            var chunks = emails
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Chunk(ChunkSize);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appToken);
            // Requerido por Graph para filtros avanzados con 'in'
            client.DefaultRequestHeaders.Add("ConsistencyLevel", "eventual");

            foreach (var chunk in chunks)
            {
                var filterValues = string.Join(",", chunk.Select(e => $"'{e}'"));
                var url = $"https://graph.microsoft.com/v1.0/users" +
                          $"?$filter=mail in ({filterValues})" +
                          $"&$select=displayName,mail,jobTitle,mobilePhone,businessPhones" +
                          $"&$count=true";

                Console.WriteLine($"[GraphUserService] URL: {url}");

                try
                {
                    var response = await client.GetAsync(url);
                    var json = await response.Content.ReadAsStringAsync();

                    Console.WriteLine($"[GraphUserService] Status: {(int)response.StatusCode} {response.StatusCode}");
                    Console.WriteLine($"[GraphUserService] Response: {json}");

                    if (!response.IsSuccessStatusCode) continue;

                    using var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty("value", out var valueArray)) continue;

                    foreach (var user in valueArray.EnumerateArray())
                    {
                        var mail = user.TryGetProperty("mail", out var mailProp) ? mailProp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(mail)) continue;

                        var displayName = user.TryGetProperty("displayName", out var dnProp) ? dnProp.GetString() : null;
                        var jobTitle = user.TryGetProperty("jobTitle", out var jtProp) ? jtProp.GetString() : null;

                        // Teléfono: mobilePhone primero, luego primer businessPhone
                        string? phone = null;
                        if (user.TryGetProperty("mobilePhone", out var mobileProp) && mobileProp.ValueKind != JsonValueKind.Null)
                            phone = mobileProp.GetString();

                        if (string.IsNullOrWhiteSpace(phone) &&
                            user.TryGetProperty("businessPhones", out var bpProp) &&
                            bpProp.ValueKind == JsonValueKind.Array)
                        {
                            var firstPhone = bpProp.EnumerateArray().FirstOrDefault();
                            if (firstPhone.ValueKind == JsonValueKind.String)
                                phone = firstPhone.GetString();
                        }

                        result[mail] = new GraphUserProfileDto
                        {
                            Mail = mail,
                            DisplayName = displayName,
                            JobTitle = jobTitle,
                            Phone = phone
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GraphUserService] EXCEPCIÓN en chunk: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Obtiene un token de acceso usando el flujo client_credentials (permiso de aplicación).
        /// Requiere AzureAd:TenantId, AzureAd:ClientId y AzureAd:ClientSecret en la configuración.
        /// </summary>
        private async Task<string?> GetAppTokenAsync()
        {
            var tenantId = _configuration["AzureAd:TenantId"];
            var clientId = _configuration["AzureAd:ClientId"];
            var clientSecret = _configuration["AzureAd:ClientSecret"];

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                Console.WriteLine("[GraphUserService] Faltan AzureAd:TenantId, AzureAd:ClientId o AzureAd:ClientSecret en la configuración.");
                return null;
            }

            var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

            var body = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default"),
            });

            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(tokenUrl, body);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GraphUserService] Error al obtener token de app: {response.StatusCode} - {json}");
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("access_token", out var tokenProp)
                    ? tokenProp.GetString()
                    : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GraphUserService] EXCEPCIÓN al obtener token de app: {ex.Message}");
                return null;
            }
        }
    }
}
