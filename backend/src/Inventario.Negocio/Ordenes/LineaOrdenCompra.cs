using Inventario.Negocio.Productos;

namespace Inventario.Negocio.Ordenes;

/// <summary>Un producto pedido dentro de una orden: cuánto y a qué costo.</summary>
public sealed class LineaOrdenCompra
{
    public int Id { get; private set; }
    public int OrdenDeCompraId { get; private set; }
    public OrdenDeCompra OrdenDeCompra { get; private set; } = null!;
    public int ProductoId { get; private set; }
    public Producto Producto { get; private set; } = null!;
    public decimal Cantidad { get; private set; }
    public decimal CostoUnitario { get; private set; }

    private LineaOrdenCompra()
    {
    }

    internal static LineaOrdenCompra Crear(OrdenDeCompra orden, Producto producto, decimal cantidad, decimal costoUnitario) => new()
    {
        OrdenDeCompra = orden,
        Producto = producto,
        Cantidad = cantidad,
        CostoUnitario = costoUnitario,
    };

    public decimal Subtotal => Cantidad * CostoUnitario;
}
