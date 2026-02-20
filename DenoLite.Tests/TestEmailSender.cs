using DenoLite.Application.Interfaces;

namespace DenoLite.Tests
{
    /// <summary>
    /// No-op email sender for tests. Doesn't actually send emails.
    /// </summary>
    public class TestEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            // In tests, we don't actually send emails
            // Just return success
            return Task.CompletedTask;
        }
    }
}
