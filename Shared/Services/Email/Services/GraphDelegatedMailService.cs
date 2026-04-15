using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Abril_Backend.Shared.Services.Email.Dtos;
using Abril_Backend.Shared.Services.Email.Interfaces;

namespace Abril_Backend.Shared.Services.Email.Services
{
    public class GraphDelegatedMailService : IDelegatedMailService
    {
        private readonly HttpClient _httpClient;

        public GraphDelegatedMailService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task SendAsync(
            string graphAccessToken,
            List<string> to,
            string subject,
            string body,
            bool isHtml,
            List<string>? cc = null,
            List<string>? bcc = null,
            List<MailAttachmentDto>? attachments = null)
        {
            var payload = new
            {
                message = new
                {
                    subject,
                    body = new
                    {
                        contentType = isHtml ? "HTML" : "Text",
                        content = body
                    },
                    toRecipients = to.Select(address => new
                    {
                        emailAddress = new { address }
                    }),
                    ccRecipients = (cc ?? new List<string>()).Select(address => new
                    {
                        emailAddress = new { address }
                    }),
                    bccRecipients = (bcc ?? new List<string>()).Select(address => new
                    {
                        emailAddress = new { address }
                    }),
                    attachments = (attachments ?? new List<MailAttachmentDto>()).Select(a => new
                    {
                        @odata_type = "#microsoft.graph.fileAttachment",
                        name = a.FileName,
                        contentType = a.ContentType,
                        contentBytes = Convert.ToBase64String(a.Content)
                    })
                },
                saveToSentItems = true
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Serializar manualmente para manejar @odata.type
            var json = BuildPayloadJson(to, subject, body, isHtml, cc, bcc, attachments);

            var request = new HttpRequestMessage(HttpMethod.Post, "v1.0/me/sendMail")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", graphAccessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private static string BuildPayloadJson(
            List<string> to,
            string subject,
            string body,
            bool isHtml,
            List<string>? cc,
            List<string>? bcc,
            List<MailAttachmentDto>? attachments)
        {
            var sb = new StringBuilder();
            sb.Append("{\"message\":{");
            sb.Append($"\"subject\":{JsonSerializer.Serialize(subject)},");
            sb.Append($"\"body\":{{\"contentType\":\"{(isHtml ? "HTML" : "Text")}\",\"content\":{JsonSerializer.Serialize(body)}}},");

            sb.Append("\"toRecipients\":[");
            sb.Append(string.Join(",", to.Select(a => $"{{\"emailAddress\":{{\"address\":{JsonSerializer.Serialize(a)}}}}}")));
            sb.Append("],");

            sb.Append("\"ccRecipients\":[");
            sb.Append(string.Join(",", (cc ?? new()).Select(a => $"{{\"emailAddress\":{{\"address\":{JsonSerializer.Serialize(a)}}}}}")));
            sb.Append("],");

            sb.Append("\"bccRecipients\":[");
            sb.Append(string.Join(",", (bcc ?? new()).Select(a => $"{{\"emailAddress\":{{\"address\":{JsonSerializer.Serialize(a)}}}}}")));
            sb.Append("],");

            sb.Append("\"attachments\":[");
            if (attachments != null && attachments.Count > 0)
            {
                sb.Append(string.Join(",", attachments.Select(a =>
                    $"{{\"@odata.type\":\"#microsoft.graph.fileAttachment\"," +
                    $"\"name\":{JsonSerializer.Serialize(a.FileName)}," +
                    $"\"contentType\":{JsonSerializer.Serialize(a.ContentType)}," +
                    $"\"contentBytes\":{JsonSerializer.Serialize(Convert.ToBase64String(a.Content))}}}"
                )));
            }
            sb.Append("]");

            sb.Append("},\"saveToSentItems\":true}");
            return sb.ToString();
        }
    }
}
