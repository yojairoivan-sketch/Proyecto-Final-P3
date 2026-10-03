namespace Inventario.Core.ControlAcceso;

/// <summary>Los dos roles del sistema (RF-CA-04). Se siembran en la migración y no cambian.</summary>
public sealed class Rol
{
    public const string Administrador = "Administrador";
    public const string Estandar = "Estándar";

    internal const int IdAdministrador = 1;
    internal const int IdEstandar = 2;

    public static readonly IReadOnlyList<string> Todos = [Administrador, Estandar];

    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";

    private Rol()
    {
    }

    internal Rol(int id, string nombre)
    {
        Id = id;
        Nombre = nombre;
    }

    internal static int IdDe(string nombre) => nombre switch
    {
        Administrador => IdAdministrador,
        Estandar => IdEstandar,
        _ => throw new ArgumentOutOfRangeException(nameof(nombre), nombre, "Rol desconocido."),
    };
}
