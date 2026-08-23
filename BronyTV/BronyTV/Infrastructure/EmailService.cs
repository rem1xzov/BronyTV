using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BronyTV.Infrastructure;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(string email, string confirmationToken, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string email, string resetCode, CancellationToken cancellationToken = default);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPassword;
    private readonly bool _smtpUseSsl;
    private readonly string _fromAddress;
    private readonly string _senderName;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _logger = logger;
        _smtpHost = ReadSetting(configuration, "Email:SmtpHost") ?? "";
        _smtpPort = int.TryParse(ReadSetting(configuration, "Email:SmtpPort"), out var port) ? port : 587;
        _smtpUser = FirstNonEmpty(
            ReadSetting(configuration, "Email:SmtpUser"),
            ReadSetting(configuration, "Email:SmtpUsername"),
            ReadSetting(configuration, "Email:SenderEmail"));
        _smtpPassword = ReadSetting(configuration, "Email:SmtpPassword") ?? "";
        _smtpUseSsl = bool.TryParse(
            FirstNonEmpty(
                ReadSetting(configuration, "Email:SmtpUseSsl"),
                ReadSetting(configuration, "Email:EnableSsl")),
            out var useSsl)
            ? useSsl
            : true;
        _fromAddress = FirstNonEmpty(
            ReadSetting(configuration, "Email:FromAddress"),
            ReadSetting(configuration, "Email:SenderEmail"));
        _senderName = FirstNonEmpty(ReadSetting(configuration, "Email:SenderName"), "BronyTV");

        if (string.IsNullOrEmpty(_fromAddress))
        {
            _fromAddress = string.IsNullOrEmpty(_smtpUser) ? "no-reply@bronytv.ru" : _smtpUser;
        }
    }

    public async Task SendEmailConfirmationAsync(
        string email,
        string confirmationCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost)
            || string.IsNullOrWhiteSpace(_smtpUser)
            || string.IsNullOrWhiteSpace(_smtpPassword))
        {
            _logger.LogError("[EmailService] SMTP не настроен: письмо с обязательным кодом не может быть отправлено.");
            throw new InvalidOperationException("SMTP is not fully configured.");
        }

        _logger.LogInformation(
            "[EmailService] Отправка кода на {Email} через {Host}:{Port}...",
            email,
            _smtpHost,
            _smtpPort);

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

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_senderName, _fromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Код подтверждения email на BronyTV";
        message.Body = new BodyBuilder
        {
            TextBody = $"Код подтверждения BronyTV: {confirmationCode}. Код действует ограниченное время.",
            HtmlBody = htmlBody
        }.ToMessageBody();

        try
        {
            using var smtp = new MailKit.Net.Smtp.SmtpClient
            {
                Timeout = 30_000
            };
            var socketOptions = _smtpUseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await smtp.ConnectAsync(_smtpHost, _smtpPort, socketOptions, cancellationToken);
            await smtp.AuthenticateAsync(_smtpUser, _smtpPassword, cancellationToken);
            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("[EmailService] Письмо успешно отправлено на {Email}", email);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[EmailService] ОШИБКА отправки письма на {Email}: {Message}", email, ex.Message);
            throw;
        }
    }

        public async Task SendPasswordResetAsync(
        string email,
        string resetCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost)
            || string.IsNullOrWhiteSpace(_smtpUser)
            || string.IsNullOrWhiteSpace(_smtpPassword))
        {
            _logger.LogError("[EmailService] SMTP не настроен: письмо сброса пароля не может быть отправлено.");
            throw new InvalidOperationException("SMTP is not fully configured.");
        }

        _logger.LogInformation(
            "[EmailService] Отправка кода сброса пароля на {Email} через {Host}:{Port}...",
            email,
            _smtpHost,
            _smtpPort);

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
                          <h2 style="margin:0 0 12px;font-size:18px;">Восстановление пароля</h2>
                          <p style="margin:0 0 16px;font-size:14px;line-height:1.6;">
                            Здравствуйте! Мы получили запрос на восстановление пароля для вашего аккаунта на BronyTV. Введите этот 6-значный код, чтобы задать новый пароль:
                          </p>
                          <p style="margin:0 0 8px;text-align:center;letter-spacing:8px;font-size:34px;font-weight:bold;color:#8e63db;">
                            {resetCode}
                          </p>
                          <p style="margin:20px 0 0;font-size:13px;color:#8a8a8a;line-height:1.5;">
                            Код действует ограниченное время и может быть использован только один раз. Если вы не запрашивали сброс пароля, просто проигнорируйте данное письмо.
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

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_senderName, _fromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Восстановление пароля на BronyTV";
        message.Body = new BodyBuilder
        {
            TextBody = $"Код восстановления пароля BronyTV: {resetCode}. Код действует ограниченное время.",
            HtmlBody = htmlBody
        }.ToMessageBody();

        await SendAsync(message, email, cancellationToken);
    }

    private async Task SendAsync(MimeMessage message, string recipient, CancellationToken cancellationToken)
    {
        try
        {
            using var smtp = new MailKit.Net.Smtp.SmtpClient
            {
                Timeout = 30_000
            };
            var socketOptions = _smtpUseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await smtp.ConnectAsync(_smtpHost, _smtpPort, socketOptions, cancellationToken);
            await smtp.AuthenticateAsync(_smtpUser, _smtpPassword, cancellationToken);
            await smtp.SendAsync(message, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("[EmailService] Письмо успешно отправлено на {Email}", recipient);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[EmailService] ОШИБКА отправки письма на {Email}: {Message}", recipient, ex.Message);
            throw;
        }
    }

    private static string? ReadSetting(IConfiguration configuration, string key)
    {
        var envKey = key.Replace(":", "__");
        return configuration[key]
            ?? Environment.GetEnvironmentVariable(envKey);
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
}
