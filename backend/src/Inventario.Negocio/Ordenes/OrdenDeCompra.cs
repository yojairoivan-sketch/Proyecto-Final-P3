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
        if (!TransicionesOrdenCompra.AdmiteCambiosEnLineas(Estado))
            throw new InvalidOperationException($"Solo se agregan líneas a una orden en {EstadoOrdenCompra.Borrador}.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidad);
        ArgumentOutOfRangeException.ThrowIfNegative(costoUnitario);

        lineas.Add(LineaOrdenCompra.Crear(this, producto, cantidad, costoUnitario));
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Cambia el estado consultando únicamente <see cref="TransicionesOrdenCompra"/> (RD-04).
    /// Si la transición es prohibida, no existe, el rol no alcanza o no se cumple la condición,
    /// se rechaza y el estado no cambia (RF-NEG-04).
    /// </summary>
    public ResultadoTransicion CambiarEstado(EstadoOrdenCompra hacia, string rol, DateTimeOffset ahora)
    {
        if (TransicionesOrdenCompra.BuscarProhibida(Estado, hacia) is { } prohibida)
            return ResultadoTransicion.Rechazada(prohibida.Motivo);

        if (TransicionesOrdenCompra.EsTerminal(Estado))
            return ResultadoTransicion.Rechazada($"La orden está {Estado}, un estado terminal: ya no cambia de estado.");

        if (TransicionesOrdenCompra.Buscar(Estado, hacia) is not { } transicion)
            return ResultadoTransicion.Rechazada($"No se puede pasar una orden de {Estado} a {hacia}.");

        if (!transicion.Quien.Contains(rol))
            return ResultadoTransicion.Rechazada(
                $"Pasar una orden de {Estado} a {hacia} es solo para: {string.Join(" o ", transicion.Quien)}.");

        if (transicion.Validar?.Invoke(this) is { } motivo)
            return ResultadoTransicion.Rechazada(motivo);

        var desde = Estado;
        transicion.AlAplicar?.Invoke(this);
        Estado = hacia;
        ActualizadaEn = ahora;
        return ResultadoTransicion.Aplicada(desde, hacia);
    }

    public decimal Total => lineas.Sum(l => l.Subtotal);
}
