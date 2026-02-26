using Abril_Backend.Infrastructure.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Infrastructure.InternalServices
{
    public class SendGridEmailService : IEmailService
    {
        private readonly string _apiKey;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public SendGridEmailService(IConfiguration config)
        {
            _apiKey = config["SendGrid:ApiKeySendGrid"];
            _fromEmail = config["EmailSettings:FromEmail"];
            _fromName = config["EmailSettings:FromName"];
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
            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);

            var tos = to.Select(email => new EmailAddress(email)).ToList();

            var msg = MailHelper.CreateSingleEmailToMultipleRecipients(
                from,
                tos,
                subject,
                isHtml ? null : body,
                isHtml ? body : null,
                false
            );

            if (cc != null)
                msg.AddCcs(cc.Select(e => new EmailAddress(e)).ToList());

            if (bcc != null)
                msg.AddBccs(bcc.Select(e => new EmailAddress(e)).ToList());

            if (attachments != null)
            {
                foreach (var file in attachments)
                {
                    msg.AddAttachment(file.FileName, Convert.ToBase64String(file.Content), file.ContentType);
                }
            }

            await client.SendEmailAsync(msg);
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