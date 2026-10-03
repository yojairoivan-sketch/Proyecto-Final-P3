namespace Inventario.Core.ControlAcceso.Contrasenas;

/// <summary>
/// Código para definir una contraseña nueva: de un solo uso y con vencimiento (RF-CA-10).
/// Solo se guarda su hash.
/// </summary>
public sealed class CodigoRecuperacion
{
    public long Id { get; private set; }
    public int UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public string HashCodigo { get; private set; } = "";
    public DateTimeOffset EmitidoEn { get; private set; }
    public DateTimeOffset VenceEn { get; private set; }
    public bool Usado { get; private set; }
    public DateTimeOffset? UsadoEn { get; private set; }

    /// <summary>Lo marca una solicitud nueva: solo sirve el código más reciente.</summary>
    public DateTimeOffset? InvalidadoEn { get; private set; }

    private CodigoRecuperacion()
    {
    }

    internal static CodigoRecuperacion Emitir(Usuario usuario, string hashCodigo, DateTimeOffset ahora, TimeSpan vigencia) => new()
    {
        Usuario = usuario,
        HashCodigo = hashCodigo,
        EmitidoEn = ahora,
        VenceEn = ahora + vigencia,
    };
}
