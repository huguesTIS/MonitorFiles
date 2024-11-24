namespace Watch2sftp.Core.services;


public class EmailService
{
    private readonly SmtpClient _smtpClient;

    public EmailService(string smtpHost, int smtpPort, string username, string password)
    {
        _smtpClient = new SmtpClient(smtpHost, smtpPort)
        {
            // Credentials = new NetworkCredential(username, password),
            // EnableSsl = true
        };
    }

    public void SendEmail(string to, string subject, string body)
    {
        var mailMessage = new MailMessage("noreply@yourdomain.com", to, subject, body);
        _smtpClient.Send(mailMessage);
    }
}

