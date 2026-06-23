using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class SmtpStudentCredentialEmailSender(
    IOptions<StudentCredentialEmailOptions> options) : IStudentCredentialEmailSender
{
    private readonly StudentCredentialEmailOptions _options = options.Value;

    public async Task SendAsync(
        StudentCredentialEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = BuildMessage(request);
        using var client = BuildClient();

        await client.SendMailAsync(message).WaitAsync(cancellationToken);
    }

    private MailMessage BuildMessage(StudentCredentialEmailRequest request)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_options.SenderEmail, _options.SenderName),
            Subject = _options.Subject,
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

    private static string BuildBody(StudentCredentialEmailRequest request)
    {
        return $"""
            Hello {request.FullName},

            Your FPT UniRAG student account has been created.

            Email: {request.Email}
            Student code: {request.StudentCode}
            Temporary password: {request.TemporaryPassword}

            Sign in and change your password after the first login.

            Regards,
            FPT UniRAG
            """;
    }
}
