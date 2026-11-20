namespace Inventario.Core.Notificaciones;

/// <summary>
/// Un aviso del sistema para un usuario, en su bandeja interna (RF-NOT-06). Nace sin leer; solo su destinatario
/// la puede marcar como leída (RF-NOT-05).
/// </summary>
public sealed class Notificacion
{
    public const int LargoMaximoTitulo = 200;
    public const int LargoMaximoMensaje = 1000;

    public long Id { get; private set; }
    public int UsuarioId { get; private set; }
    public string Titulo { get; private set; } = "";
    public string Mensaje { get; private set; } = "";
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset? LeidaEn { get; private set; }

    public bool Leida => LeidaEn is not null;

    private Notificacion()
    {
    }

    internal static Notificacion Nueva(int usuarioId, string titulo, string mensaje, DateTimeOffset ahora) => new()
    {
        UsuarioId = usuarioId,
        Titulo = Recortar(titulo, LargoMaximoTitulo),
        Mensaje = Recortar(mensaje, LargoMaximoMensaje),
        CreadaEn = ahora,
    };

    /// <summary>Marcarla otra vez no cambia la fecha de la primera lectura.</summary>
    internal void MarcarLeida(DateTimeOffset ahora) => LeidaEn ??= ahora;

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}

/// <summary>Cómo quiere recibir sus avisos un usuario. Si no tiene fila, el correo está activo (RF-NOT-06).</summary>
public sealed class PreferenciaNotificacion
{
    public int UsuarioId { get; private set; }
    public bool CorreoActivo { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }

    private PreferenciaNotificacion()
    {
    }

    internal static PreferenciaNotificacion De(int usuarioId, bool correoActivo, DateTimeOffset ahora) => new()
    {
        UsuarioId = usuarioId,
        CorreoActivo = correoActivo,
        ActualizadaEn = ahora,
    };

    internal void Cambiar(bool correoActivo, DateTimeOffset ahora)
    {
        CorreoActivo = correoActivo;
        ActualizadaEn = ahora;
    }
}
