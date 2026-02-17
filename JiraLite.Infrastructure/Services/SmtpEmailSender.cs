using JiraLite.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace JiraLite.Infrastructure.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;

        public SmtpEmailSender(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.UseSsl,

                // ✅ important for real SMTP servers
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            using var msg = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            msg.To.Add(toEmail);

            await client.SendMailAsync(msg);
        }
    }
}
