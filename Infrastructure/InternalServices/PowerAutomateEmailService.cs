using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.DTOs;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class PowerAutomateEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly string _powerAutomateUrl;

        public PowerAutomateEmailService(IConfiguration config, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _powerAutomateUrl = config["PowerAutomate:WebhookUrl"];
        }

        public async Task SendAsync(
            List<string> to,
            string subject,
            string body,
            bool isHtml,
            List<string>? cc = null,
            List<string>? bcc = null,
            List<EmailAttachment>? attachments = null)
        {
            var payload = new
            {
                To = to,
                //To = "calvarez@abril.pe",
                Subject = subject,
                Body = body,
                //IsHtml = isHtml,
                //Cc = cc,
                //Bcc = bcc,
                /*Attachments = attachments?.Select(a => new
                {
                    a.FileName,
                    a.ContentType,
                    ContentBase64 = Convert.ToBase64String(a.Content)
                })*/
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_powerAutomateUrl, content);

            response.EnsureSuccessStatusCode(); // Lanza excepción si falla
        }

        public async Task SendAsync(
            string to,
            string subject,
            string body,
            bool isHtml,
            List<EmailAttachment>? attachments = null)
        {
            await SendAsync(new List<string> { to }, subject, body, isHtml, null, null, null);
        }
    }
}