using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Abril_Backend.Shared.Services.SharePoint.Services
{
    public class GraphSharePointService : IGraphSharePointService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public GraphSharePointService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<string?> UploadFileAsync(
            string accessToken,
            string folderPath,
            string fileName,
            Stream fileStream,
            string contentType = "application/octet-stream")
        {
            // TODO: Cuando el admin apruebe Files.ReadWrite.All, cambiar a SharePoint:
            // var siteId = _configuration["SharePoint:SiteId"];
            // var listId = _configuration["SharePoint:ListId"];
            // var url = $"https://graph.microsoft.com/v1.0/sites/{siteId}/lists/{listId}/drive/root:/{itemPath}:/content";

            var itemPath = $"{folderPath.Trim('/')}/{fileName}";
            var url = $"https://graph.microsoft.com/v1.0/me/drive/root:/{itemPath}:/content";

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(resolvedContentType.Split(';')[0].Trim());

            var response = await client.PutAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"OneDrive upload falló. Status: {(int)response.StatusCode} {response.StatusCode}. Error: {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("webUrl", out var webUrlProp))
                return webUrlProp.GetString();

            return null;
        }
    }
}
