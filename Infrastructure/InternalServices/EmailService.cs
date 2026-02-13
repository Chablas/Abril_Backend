using Abril_Backend.Infrastructure.Interfaces;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }
        public async Task SendAsync(string to, string subject, string body)
        {
            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(to);

            using var smtp = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(
                    _settings.Username,
                    _settings.Password
                ),
                EnableSsl = _settings.EnableSsl,
                UseDefaultCredentials = false
            };

            await smtp.SendMailAsync(message);
        }
    }
}
