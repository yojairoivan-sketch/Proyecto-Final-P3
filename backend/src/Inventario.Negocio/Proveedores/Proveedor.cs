namespace Inventario.Negocio.Proveedores;

/// <summary>A quién se le compra la mercancía.</summary>
public sealed class Proveedor
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";
    public string? Telefono { get; private set; }
    public string? Correo { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }

    private Proveedor()
    {
    }

    public static Proveedor Crear(string nombre, string? telefono, string? correo, DateTimeOffset ahora) => new()
    {
        Nombre = nombre,
        Telefono = telefono,
        Correo = correo,
        CreadoEn = ahora,
    };
}
