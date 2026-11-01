using Inventario.Negocio.Ordenes;
using Inventario.Negocio.Productos;

namespace Inventario.Negocio.Existencias;

public enum TipoMovimiento
{
    /// <summary>Entró mercancía al recibir una orden de compra.</summary>
    Entrada,

    /// <summary>Salió mercancía: una venta, una merma, un consumo.</summary>
    Salida,

    /// <summary>Corrección después de un conteo físico.</summary>
    Ajuste,
}

/// <summary>
/// Un cambio en la existencia de un producto: cuánto, por qué, quién y cuándo. Nunca se edita ni se borra;
/// la existencia actual del producto es el resultado de todos sus movimientos.
/// </summary>
public sealed class MovimientoExistencia
{
    public const int LargoMaximoMotivo = 200;

    public long Id { get; private set; }
    public int ProductoId { get; private set; }
    public Producto Producto { get; private set; } = null!;
    public TipoMovimiento Tipo { get; private set; }

    /// <summary>Cuánto cambió la existencia: positivo si entró mercancía, negativo si salió.</summary>
    public decimal Cantidad { get; private set; }

    /// <summary>La existencia del producto justo después del movimiento.</summary>
    public decimal ExistenciaResultante { get; private set; }

    public string Motivo { get; private set; } = "";

    /// <summary>La orden de compra que lo originó, si fue una entrada.</summary>
    public int? OrdenDeCompraId { get; private set; }
    public OrdenDeCompra? OrdenDeCompra { get; private set; }

    /// <summary>Id del usuario en Control de acceso; el nombre se copia para mostrarlo sin leer sus tablas.</summary>
    public int UsuarioId { get; private set; }
    public string UsuarioNombre { get; private set; } = "";

    public DateTimeOffset RegistradoEn { get; private set; }

    private MovimientoExistencia()
    {
    }

    internal static MovimientoExistencia Registrar(
        Producto producto, TipoMovimiento tipo, decimal cantidad, string motivo, OrdenDeCompra? orden, Responsable quien, DateTimeOffset ahora) => new()
    {
        Producto = producto,
        Tipo = tipo,
        Cantidad = cantidad,
        ExistenciaResultante = producto.Existencia,
        Motivo = motivo,
        OrdenDeCompra = orden,
        UsuarioId = quien.UsuarioId,
        UsuarioNombre = quien.Nombre,
        RegistradoEn = ahora,
    };
}
