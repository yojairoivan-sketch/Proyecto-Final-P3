namespace Inventario.Core.ColaCorreos;

public enum EstadoCorreo
{
    Pendiente,
    Enviado,

    /// <summary>Falló el máximo de intentos (RF-NOT-10). No se vuelve a intentar, pero no se borra: queda su último error.</summary>
    Fallido,
}

/// <summary>
/// Un correo saliente. Nace Pendiente y solo el procesador de la cola lo pasa a Enviado o, agotados los intentos, a Fallido.
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
    public DateTimeOffset? UltimoIntentoEn { get; private set; }
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
        UltimoIntentoEn = ahora;
        UltimoError = null;
    }

    /// <summary>
    /// Anota el intento fallido y su error. Sigue Pendiente (se reintenta en otra pasada) hasta llegar a
    /// <paramref name="maximoIntentos"/>; entonces queda Fallido (RF-NOT-10).
    /// </summary>
    internal void RegistrarFallo(string error, DateTimeOffset ahora, int maximoIntentos)
    {
        Intentos++;
        UltimoIntentoEn = ahora;
        UltimoError = error.Length > 1000 ? error[..1000] : error;
        if (Intentos >= maximoIntentos)
            Estado = EstadoCorreo.Fallido;
    }
}
