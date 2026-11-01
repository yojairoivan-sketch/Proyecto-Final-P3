using Inventario.Core.ControlAcceso;
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

    /// <summary>Solo una orden en Borrador admite líneas, y cada producto va una sola vez. Si no se puede, nada cambia.</summary>
    public Resultado AgregarLinea(Producto producto, decimal cantidad, decimal costoUnitario, DateTimeOffset ahora)
    {
        if (!TransicionesOrdenCompra.AdmiteCambiosEnLineas(Estado))
            return Resultado.Falla(TipoFallo.Conflicto,
                $"Solo se agregan líneas a una orden en {EstadoOrdenCompra.Borrador}. Esta orden está {Estado}.");

        if (lineas.Any(linea => linea.Producto == producto))
            return Resultado.Falla(TipoFallo.Conflicto,
                $"{producto.Nombre} ya está en la orden. Cada producto va en una sola línea.");

        lineas.Add(LineaOrdenCompra.Crear(this, producto, cantidad, costoUnitario));
        Cambio(ahora);
        return Resultado.Ok($"Se agregó {producto.Nombre} a la orden.");
    }

    /// <summary>
    /// Intenta pasar la orden a <paramref name="hacia"/>. Lo resuelve únicamente <see cref="TransicionesOrdenCompra"/> (RD-04):
    /// si la transición no está permitida, la ejecuta un rol que no debe o no se cumple su condición, se rechaza
    /// y la orden queda como estaba (RF-NEG-04).
    /// </summary>
    public Resultado CambiarEstado(EstadoOrdenCompra hacia, Responsable quien, DateTimeOffset ahora) =>
        TransicionesOrdenCompra.Resolver(this, hacia, quien, ahora);

    // Solo la llama la tabla de transiciones, después de comprobar todo.
    internal void PasarA(EstadoOrdenCompra hacia, DateTimeOffset ahora)
    {
        Estado = hacia;
        Cambio(ahora);
    }

    private void Cambio(DateTimeOffset ahora)
    {
        ActualizadaEn = ahora;
    }

    public decimal Total => lineas.Sum(l => l.Subtotal);
}
