using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class SmtpTeacherCredentialEmailSender(
    IOptions<StudentCredentialEmailOptions> options) : ITeacherCredentialEmailSender
{
    private readonly StudentCredentialEmailOptions _options = options.Value;

    public async Task SendAsync(
        TeacherCredentialEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        using var message = BuildMessage(request);
        using var client = BuildClient();

        await client.SendMailAsync(message).WaitAsync(cancellationToken);
    }

    private MailMessage BuildMessage(TeacherCredentialEmailRequest request)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_options.SenderEmail, _options.SenderName),
            Subject = "Your FPT UniRAG teacher account",
            Body = BuildBody(request),
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(request.Email, request.FullName));
        return message;
    }

    private SmtpClient BuildClient()
    {
        var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            UseDefaultCredentials = _options.UseDefaultCredentials
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(
                _options.Username,
                _options.Password ?? string.Empty);
        }

        return client;
    }

    private static string BuildBody(TeacherCredentialEmailRequest request)
    {
        return $"""
            Hello {request.FullName},

            Your FPT UniRAG teacher account has been created.

            Email: {request.Email}
            Temporary password: {request.TemporaryPassword}

            Sign in and change your password after the first login.

            Regards,
            FPT UniRAG
            """;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("StudentCredentialEmail:Host must be configured before sending teacher credentials.");
        }

        if (string.IsNullOrWhiteSpace(_options.SenderEmail))
        {
            throw new InvalidOperationException("StudentCredentialEmail:SenderEmail must be configured before sending teacher credentials.");
        }
    }
}
