using Inventario.Negocio.Existencias;
using Inventario.Negocio.Ordenes;

namespace Inventario.Negocio.Productos;

/// <summary>Un artículo del inventario, con su existencia actual y su punto de reorden.</summary>
public sealed class Producto
{
    private readonly List<MovimientoExistencia> movimientos = [];

    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";

    /// <summary>Código interno del artículo. Único.</summary>
    public string Sku { get; private set; } = "";

    /// <summary>Unidad, caja, kg, litro...</summary>
    public string UnidadMedida { get; private set; } = "";

    /// <summary>Por debajo de esta existencia el producto genera una alerta de stock bajo.</summary>
    public decimal PuntoReorden { get; private set; }

    /// <summary>Solo cambia con un movimiento: entrada, salida o ajuste.</summary>
    public decimal Existencia { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Sube con cada movimiento. Si dos usuarios mueven la existencia a la vez, el segundo en guardar falla.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<MovimientoExistencia> Movimientos => movimientos;

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

    /// <summary>Suma lo recibido en una orden de compra. Lo aplica la transición Enviada → Recibida.</summary>
    internal void RegistrarEntrada(decimal cantidad, OrdenDeCompra orden, Responsable quien, DateTimeOffset ahora) =>
        Mover(TipoMovimiento.Entrada, cantidad, $"Recepción de la orden de compra {orden.Id}", orden, quien, ahora);

    /// <summary>Resta lo que salió. La existencia nunca queda negativa: si no alcanza, no cambia nada y devuelve el motivo.</summary>
    internal string? RegistrarSalida(decimal cantidad, string motivo, Responsable quien, DateTimeOffset ahora)
    {
        if (cantidad > Existencia)
            return $"No hay suficiente existencia de {Nombre}: hay {Existencia:0.##} y quieres sacar {cantidad:0.##}. La existencia no cambió.";

        Mover(TipoMovimiento.Salida, -cantidad, motivo, orden: null, quien, ahora);
        return null;
    }

    /// <summary>Deja la existencia en lo que se contó. Si ya era eso, no hay nada que ajustar.</summary>
    internal string? Ajustar(decimal existenciaContada, string motivo, Responsable quien, DateTimeOffset ahora)
    {
        if (existenciaContada == Existencia)
            return $"La existencia de {Nombre} ya es {Existencia:0.##}: no hay nada que ajustar.";

        Mover(TipoMovimiento.Ajuste, existenciaContada - Existencia, motivo, orden: null, quien, ahora);
        return null;
    }

    private void Mover(TipoMovimiento tipo, decimal cambio, string motivo, OrdenDeCompra? orden, Responsable quien, DateTimeOffset ahora)
    {
        Existencia += cambio;
        Version++;
        movimientos.Add(MovimientoExistencia.Registrar(this, tipo, cambio, motivo, orden, quien, ahora));
    }
}
