namespace Inventario.Core.ControlAcceso.Sesiones;

/// <summary>
/// Credencial de sesión (RF-CA-03): un token opaco que el cliente manda como «Bearer».
/// En la base solo queda su hash; cerrar sesión la revoca (RF-CA-18).
/// </summary>
public sealed class Sesion
{
    public long Id { get; private set; }
    public int UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public string HashToken { get; private set; } = "";
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public DateTimeOffset? RevocadaEn { get; private set; }

    private Sesion()
    {
    }

    internal static Sesion Abrir(Usuario usuario, string hashToken, DateTimeOffset ahora, TimeSpan vigencia) => new()
    {
        Usuario = usuario,
        HashToken = hashToken,
        CreadaEn = ahora,
        VenceEn = ahora + vigencia,
    };
}
