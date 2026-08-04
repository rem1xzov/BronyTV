using System.Net;
using System.Net.Mail;

namespace BronyTV.Infrastructure;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string email, string confirmationToken, CancellationToken cancellationToken = default);
}

public class EmailService : IEmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPassword;
    private readonly bool _smtpUseSsl;
    private readonly string _fromAddress;

    public EmailService(IConfiguration configuration)
    {
        _smtpHost = configuration["Email:SmtpHost"]
            ?? Environment.GetEnvironmentVariable("Email__SmtpHost")
            ?? "";
        _smtpPort = int.TryParse(
            configuration["Email:SmtpPort"]
            ?? Environment.GetEnvironmentVariable("Email__SmtpPort"),
            out var port)
            ? port
            : 587;
        _smtpUser = configuration["Email:SmtpUser"]
            ?? Environment.GetEnvironmentVariable("Email__SmtpUser")
            ?? "";
        _smtpPassword = configuration["Email:SmtpPassword"]
            ?? Environment.GetEnvironmentVariable("Email__SmtpPassword")
            ?? "";
        _smtpUseSsl = bool.TryParse(
            configuration["Email:SmtpUseSsl"]
            ?? Environment.GetEnvironmentVariable("Email__SmtpUseSsl"),
            out var useSsl)
            ? useSsl
            : true;
        _fromAddress = configuration["Email:FromAddress"]
            ?? Environment.GetEnvironmentVariable("Email__FromAddress")
            ?? (string.IsNullOrEmpty(_smtpUser) ? "no-reply@bronytv.ru" : _smtpUser);
    }

    public Task SendEmailConfirmationAsync(string email, string confirmationCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_smtpHost))
        {
            // SMTP is not configured; skip actual sending so the app still works in dev.
            return Task.CompletedTask;
        }

        var htmlBody = $"""
            <!DOCTYPE html>
            <html lang="ru">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
            <body style="margin:0;padding:0;background-color:#f7f4ef;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f7f4ef;padding:24px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" style="max-width:480px;background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.08);">
                      <tr>
                        <td style="background-color:#8e63db;padding:24px;text-align:center;">
                          <span style="font-size:36px;">🦄</span>
                          <h1 style="margin:8px 0 0;color:#ffffff;font-size:22px;">BronyTV</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:32px 28px;color:#3d3d3d;">
                          <h2 style="margin:0 0 12px;font-size:18px;">Код подтверждения</h2>
                          <p style="margin:0 0 16px;font-size:14px;line-height:1.6;">
                            Здравствуйте! Добро пожаловать на BronyTV. Введите этот 6-значный код в форме подтверждения, чтобы завершить регистрацию:
                          </p>
                          <p style="margin:0 0 8px;text-align:center;letter-spacing:8px;font-size:34px;font-weight:bold;color:#8e63db;">
                            {confirmationCode}
                          </p>
                          <p style="margin:20px 0 0;font-size:13px;color:#8a8a8a;line-height:1.5;">
                            Код действует ограниченное время и может быть использован только один раз. Если вы не запрашивали этот код, просто проигнорируйте данное письмо.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:16px 28px;background-color:#f2eefb;color:#8a8a8a;font-size:12px;text-align:center;">
                          © BronyTV. Это письмо отправлено автоматически, отвечать на него не нужно.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        using var smtp = new SmtpClient(_smtpHost, _smtpPort)
        {
            Credentials = new NetworkCredential(_smtpUser, _smtpPassword),
            EnableSsl = _smtpUseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var message = new MailMessage
        {
            From = new MailAddress(_fromAddress, "BronyTV"),
            Subject = "Код подтверждения email на BronyTV",
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email));

        try
        {
            return smtp.SendMailAsync(message, cancellationToken);
        }
        finally
        {
            message.Dispose();
        }
    }
}
