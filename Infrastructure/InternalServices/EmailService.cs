using Abril_Backend.Infrastructure.Interfaces;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
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
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            foreach (var email in to.Distinct())
                message.To.Add(email);

            if (cc != null)
                foreach (var email in cc.Distinct())
                    message.CC.Add(email);

            if (bcc != null)
                foreach (var email in bcc.Distinct())
                    message.Bcc.Add(email);

            if (attachments != null && attachments.Any())
            {
                foreach (var file in attachments)
                {
                    var stream = new MemoryStream(file.Content);
                    var attachment = new Attachment(stream, file.FileName, file.ContentType);
                    message.Attachments.Add(attachment);
                }
            }

            using var smtp = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                EnableSsl = true,
                UseDefaultCredentials = false
            };

            await smtp.SendMailAsync(message);
        }

        public async Task SendAsync(
            string to,
            string subject,
            string body,
            bool isHtml,
            List<EmailAttachment>? attachments = null)
        {
            await SendAsync(new List<string> { to }, subject, body, isHtml, null, null, attachments);
        }
    }
}
