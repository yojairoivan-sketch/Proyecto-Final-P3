namespace Inventario.Core.ColaCorreos;

public enum SeguridadSmtp
{
    Ninguna,
    StartTls,
    SslAlConectar,
}

/// <summary>
/// Datos del servidor SMTP. Salen solo de variables de entorno (RF-NOT-13, RD-10); nunca del repositorio.
/// </summary>
public sealed record ConfiguracionSmtp(
    string Host,
    int Puerto,
    SeguridadSmtp Seguridad,
    string? Usuario,
    string? Contrasena,
    string Remitente)
{
    public static ConfiguracionSmtp DesdeVariables(Func<string, string?> leer)
    {
        string Obligatoria(string nombre)
        {
            var valor = leer(nombre);
            return string.IsNullOrWhiteSpace(valor)
                ? throw new InvalidOperationException($"Falta la variable de entorno {nombre}. Revisa tu archivo .env.")
                : valor.Trim();
        }

        var host = Obligatoria("SMTP_HOST");
        var remitente = Obligatoria("SMTP_REMITENTE");

        if (!int.TryParse(Obligatoria("SMTP_PUERTO"), out var puerto) || puerto is < 1 or > 65535)
            throw new InvalidOperationException("SMTP_PUERTO debe ser un número de puerto válido (por ejemplo 587).");

        if (!Enum.TryParse<SeguridadSmtp>(Obligatoria("SMTP_SEGURIDAD"), ignoreCase: true, out var seguridad))
            throw new InvalidOperationException("SMTP_SEGURIDAD debe ser Ninguna, StartTls o SslAlConectar.");

        var usuario = leer("SMTP_USUARIO");
        var contrasena = leer("SMTP_CONTRASENA");

        return new ConfiguracionSmtp(
            host,
            puerto,
            seguridad,
            string.IsNullOrWhiteSpace(usuario) ? null : usuario.Trim(),
            string.IsNullOrEmpty(contrasena) ? null : contrasena,
            remitente);
    }

    /// <summary>Para los mensajes del enviador. Nunca incluye la contraseña.</summary>
    public override string ToString() =>
        $"{Host}:{Puerto} ({Seguridad}){(Usuario is null ? "" : $" como {Usuario}")}";
}
