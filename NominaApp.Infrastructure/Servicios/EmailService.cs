using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace NominaApp.Infrastructure.Servicios;

public class EmailService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPass;
    private readonly bool _smtpEnableSsl;
    private readonly string _emailFrom;
    private readonly string _nombreFrom;

    public EmailService(IConfiguration config)
    {
        _smtpHost      = config["Smtp:Host"] ?? "";
        _smtpPort      = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        _smtpUser      = config["Smtp:User"] ?? "";
        _smtpPass      = config["Smtp:Pass"] ?? "";
        _smtpEnableSsl = bool.TryParse(config["Smtp:EnableSsl"], out var ssl) ? ssl : true;
        
        _emailFrom  = config["Smtp:EmailFrom"] ?? "nomina@nominaapp.mx";
        _nombreFrom = config["Smtp:NombreFrom"] ?? "NóminaApp";
    }

    public async Task<bool> EnviarAsync(string para, string nombrePara, string asunto, string htmlBody)
    {
        if (string.IsNullOrEmpty(_smtpHost) || _smtpHost == "smtp.tuserver.com")
        {
            Console.WriteLine($"[EMAIL SIMULADO] Para: {para} | Asunto: {asunto}");
            Console.WriteLine($"Contenido: {htmlBody}");
            return true;
        }

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = _smtpEnableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailFrom, _nombreFrom),
                Subject = asunto,
                Body = htmlBody,
                IsBodyHtml = true
            };
            
            mailMessage.To.Add(new MailAddress(para, nombrePara));

            await client.SendMailAsync(mailMessage);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL ERROR] {ex.Message}");
            return false;
        }
    }

    public async Task<bool> EnviarMultiplesAsync(List<(string email, string nombre)> destinatarios, string asunto, string htmlBody)
    {
        var tareas = destinatarios.Select(d => EnviarAsync(d.email, d.nombre, asunto, htmlBody));
        var resultados = await Task.WhenAll(tareas);
        return resultados.All(r => r);
    }
}