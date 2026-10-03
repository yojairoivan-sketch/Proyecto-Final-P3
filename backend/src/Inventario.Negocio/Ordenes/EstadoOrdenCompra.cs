namespace Inventario.Negocio.Ordenes;

/// <summary>
/// RF-NEG-03: los estados de una orden de compra, declarados en este único lugar.
/// Qué transiciones existen entre ellos se declara en <see cref="TransicionesOrdenCompra"/>.
/// </summary>
public enum EstadoOrdenCompra
{
    /// <summary>Se está armando: se pueden agregar líneas.</summary>
    Borrador,

    /// <summary>Se le envió al proveedor; se espera la mercancía.</summary>
    Enviada,

    /// <summary>La mercancía llegó y entró al inventario. Terminal.</summary>
    Recibida,

    /// <summary>Se anuló antes de recibirla. Terminal.</summary>
    Cancelada,
}
