using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Inventario.Core.ColaCorreos;

/// <summary>Interfaz requerida por el procesador: quien entrega físicamente el correo.</summary>
public interface ITransporteCorreo
{
    Task EnviarAsync(CorreoEnCola correo, CancellationToken ct);
}

/// <summary>Entrega por SMTP con MailKit.</summary>
internal sealed class TransporteSmtp(ConfiguracionSmtp configuracion) : ITransporteCorreo
{
    public async Task EnviarAsync(CorreoEnCola correo, CancellationToken ct)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress("Inventario", configuracion.Remitente));
        mensaje.To.Add(MailboxAddress.Parse(correo.Destinatario));
        mensaje.Subject = correo.Asunto;
        mensaje.Body = new TextPart("plain") { Text = correo.Cuerpo };

        var opciones = configuracion.Seguridad switch
        {
            SeguridadSmtp.StartTls => SecureSocketOptions.StartTls,
            SeguridadSmtp.SslAlConectar => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.None,
        };

        using var cliente = new SmtpClient { Timeout = 20_000 };
        await cliente.ConnectAsync(configuracion.Host, configuracion.Puerto, opciones, ct);
        if (configuracion.Usuario is not null)
            await cliente.AuthenticateAsync(configuracion.Usuario, configuracion.Contrasena ?? "", ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(quit: true, ct);
    }
}
