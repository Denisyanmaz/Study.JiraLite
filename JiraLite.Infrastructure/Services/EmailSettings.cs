namespace JiraLite.Infrastructure.Services
{
    public class EmailSettings
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1025;

        public string FromEmail { get; set; } = "no-reply@jiralite.local";
        public string FromName { get; set; } = "JiraLite";

        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool UseSsl { get; set; } = false;
    }
}
