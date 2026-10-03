namespace Inventario.Negocio.Productos;

/// <summary>Un artículo del inventario, con su existencia actual y su punto de reorden.</summary>
public sealed class Producto
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";

    /// <summary>Código interno del artículo. Único.</summary>
    public string Sku { get; private set; } = "";

    /// <summary>Unidad, caja, kg, litro...</summary>
    public string UnidadMedida { get; private set; } = "";

    /// <summary>Por debajo de esta existencia el producto genera una alerta de stock bajo.</summary>
    public decimal PuntoReorden { get; private set; }

    public decimal Existencia { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }

    private Producto()
    {
    }

    public static Producto Crear(string nombre, string sku, string unidadMedida, decimal puntoReorden, DateTimeOffset ahora) => new()
    {
        Nombre = nombre,
        Sku = sku,
        UnidadMedida = unidadMedida,
        PuntoReorden = puntoReorden,
        CreadoEn = ahora,
    };

    public bool StockBajo => Existencia < PuntoReorden;

    internal void RegistrarEntrada(decimal cantidad) => Existencia += cantidad;
}
