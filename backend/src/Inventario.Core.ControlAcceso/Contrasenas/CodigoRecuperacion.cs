namespace Inventario.Core.ControlAcceso.Contrasenas;

/// <summary>Quién pidió el código: el propio usuario (olvidó su contraseña) o un Administrador (la forzó).</summary>
public enum OrigenCodigo
{
    PropioUsuario,
    Administrador,
}

/// <summary>
/// Código para definir una contraseña nueva: de un solo uso y con vencimiento (RF-CA-10).
/// Solo se guarda su hash.
/// </summary>
public sealed class CodigoRecuperacion
{
    /// <summary>
    /// RF-AUD-05: quién inició el restablecimiento. Así, cuando el usuario define la contraseña con un código que pidió un
    /// Administrador, el historial dice que lo inició el Administrador.
    /// </summary>
    public OrigenCodigo Origen { get; private set; }

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

    internal static CodigoRecuperacion Emitir(
        Usuario usuario, string hashCodigo, DateTimeOffset ahora, TimeSpan vigencia, OrigenCodigo origen) => new()
    {
        Usuario = usuario,
        Origen = origen,
        HashCodigo = hashCodigo,
        EmitidoEn = ahora,
        VenceEn = ahora + vigencia,
    };
}
