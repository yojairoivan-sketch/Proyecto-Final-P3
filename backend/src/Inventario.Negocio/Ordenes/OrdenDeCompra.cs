using Inventario.Negocio.Productos;
using Inventario.Negocio.Proveedores;

namespace Inventario.Negocio.Ordenes;

/// <summary>Entidad central del dominio: un pedido de mercancía a un proveedor.</summary>
public sealed class OrdenDeCompra
{
    private readonly List<LineaOrdenCompra> lineas = [];

    public int Id { get; private set; }
    public int ProveedorId { get; private set; }
    public Proveedor Proveedor { get; private set; } = null!;

    /// <summary>Atributo de estado de la entidad central. Nace en Borrador.</summary>
    public EstadoOrdenCompra Estado { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }
    public IReadOnlyList<LineaOrdenCompra> Lineas => lineas;

    private OrdenDeCompra()
    {
    }

    public static OrdenDeCompra Crear(Proveedor proveedor, DateTimeOffset ahora) => new()
    {
        Proveedor = proveedor,
        Estado = EstadoOrdenCompra.Borrador,
        CreadaEn = ahora,
        ActualizadaEn = ahora,
    };

    public void AgregarLinea(Producto producto, decimal cantidad, decimal costoUnitario, DateTimeOffset ahora)
    {
        lineas.Add(LineaOrdenCompra.Crear(this, producto, cantidad, costoUnitario));
        ActualizadaEn = ahora;
    }

    public decimal Total => lineas.Sum(l => l.Subtotal);
}
