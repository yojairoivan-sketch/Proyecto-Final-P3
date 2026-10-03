namespace Inventario.Core.ColaCorreos;

public enum EstadoCorreo
{
    Pendiente,
    Enviado,
}

/// <summary>
/// Un correo saliente. Nace Pendiente y solo el procesador de la cola lo pasa a Enviado.
/// </summary>
public sealed class CorreoEnCola
{
    public long Id { get; private set; }
    public string Destinatario { get; private set; } = "";
    public string Asunto { get; private set; } = "";
    public string Cuerpo { get; private set; } = "";
    public EstadoCorreo Estado { get; private set; }
    public int Intentos { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset? EnviadoEn { get; private set; }
    public string? UltimoError { get; private set; }

    private CorreoEnCola()
    {
    }

    internal static CorreoEnCola Nuevo(string destinatario, string asunto, string cuerpo, DateTimeOffset ahora) => new()
    {
        Destinatario = destinatario,
        Asunto = asunto,
        Cuerpo = cuerpo,
        Estado = EstadoCorreo.Pendiente,
        CreadoEn = ahora,
    };

    internal void MarcarEnviado(DateTimeOffset ahora)
    {
        Intentos++;
        Estado = EstadoCorreo.Enviado;
        EnviadoEn = ahora;
        UltimoError = null;
    }

    /// <summary>El correo sigue Pendiente. Los reintentos con límite y el estado Fallido llegan en la semana 11.</summary>
    internal void RegistrarFallo(string error)
    {
        Intentos++;
        UltimoError = error.Length > 1000 ? error[..1000] : error;
    }
}
