namespace Inventario.Core.ControlAcceso.Permisos;

/// <summary>
/// Un permiso que el usuario tiene además de lo que le da su rol: el nombre de una operación que puede ejecutar.
/// Lo otorga Gestión de permisos al aplicar una solicitud aprobada (RF-GP-05).
/// </summary>
public sealed class PermisoOtorgado
{
    public long Id { get; private set; }
    public int UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public string Permiso { get; private set; } = "";
    public DateTimeOffset OtorgadoEn { get; private set; }

    private PermisoOtorgado()
    {
    }

    internal static PermisoOtorgado Otorgar(int usuarioId, string permiso, DateTimeOffset ahora) => new()
    {
        UsuarioId = usuarioId,
        Permiso = permiso,
        OtorgadoEn = ahora,
    };
}
