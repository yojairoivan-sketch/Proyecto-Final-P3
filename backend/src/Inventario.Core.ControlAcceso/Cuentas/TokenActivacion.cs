namespace Inventario.Core.ControlAcceso.Cuentas;

/// <summary>
/// Enlace de activación: de un solo uso y con vencimiento (RF-CA-16). Solo se guarda el hash del token.
/// </summary>
public sealed class TokenActivacion
{
    public long Id { get; private set; }
    public int UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public string HashCodigo { get; private set; } = "";
    public DateTimeOffset EmitidoEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public bool Usado { get; private set; }
    public DateTimeOffset? UsadoEn { get; private set; }

    /// <summary>Lo marca un reenvío: el enlace anterior deja de servir (RF-CA-17).</summary>
    public DateTimeOffset? InvalidadoEn { get; private set; }

    private TokenActivacion()
    {
    }

    internal static TokenActivacion Emitir(Usuario usuario, string hashCodigo, DateTimeOffset ahora, TimeSpan vigencia) => new()
    {
        Usuario = usuario,
        HashCodigo = hashCodigo,
        EmitidoEn = ahora,
        VenceEn = ahora + vigencia,
    };
}
