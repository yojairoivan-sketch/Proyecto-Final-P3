using Inventario.Core.ControlAcceso;
using static Inventario.Negocio.Ordenes.EstadoOrdenCompra;

namespace Inventario.Negocio.Ordenes;

/// <summary>Una transición permitida: desde, hacia, quién la ejecuta y con qué condición.</summary>
/// <param name="Validar">Comprueba la condición; devuelve el motivo del rechazo o null si se cumple.</param>
/// <param name="AlAplicar">Efecto de la transición sobre la orden, si lo tiene.</param>
public sealed record Transicion(
    EstadoOrdenCompra Desde,
    EstadoOrdenCompra Hacia,
    IReadOnlyList<string> Quien,
    string Condicion,
    Func<OrdenDeCompra, string?>? Validar = null,
    Action<OrdenDeCompra>? AlAplicar = null);

/// <summary>Una transición prohibida de forma explícita, con el motivo que se le muestra al usuario.</summary>
public sealed record TransicionProhibida(EstadoOrdenCompra Desde, EstadoOrdenCompra Hacia, string Motivo);

/// <summary>
/// RD-04: el único punto del código donde se resuelven las transiciones de la orden de compra.
/// Agregar una transición nueva es agregar una línea a <see cref="Permitidas"/>; nadie más valida estados.
/// La tabla legible está en docs/maquina-de-estados.md.
/// </summary>
public static class TransicionesOrdenCompra
{
    private static readonly string[] SoloAdministrador = [Rol.Administrador];
    private static readonly string[] CualquierRol = [Rol.Administrador, Rol.Estandar];

    public static readonly IReadOnlyList<Transicion> Permitidas =
    [
        new(Borrador, Enviada, SoloAdministrador, "La orden tiene al menos una línea.",
            Validar: orden => orden.Lineas.Count == 0 ? "No se puede enviar una orden sin líneas." : null),
        new(Borrador, Cancelada, CualquierRol, "Ninguna."),
        new(Enviada, Recibida, CualquierRol, "Al recibirla, la cantidad de cada línea se suma a la existencia del producto.",
            AlAplicar: orden =>
            {
                foreach (var linea in orden.Lineas)
                    linea.Producto.RegistrarEntrada(linea.Cantidad);
            }),
        new(Enviada, Cancelada, SoloAdministrador, "Ninguna."),
    ];

    /// <summary>RF-NEG-04: transiciones prohibidas de forma explícita.</summary>
    public static readonly IReadOnlyList<TransicionProhibida> Prohibidas =
    [
        new(Recibida, Cancelada,
            "Una orden recibida no se puede cancelar: la mercancía ya entró al inventario. Corrígelo con un ajuste de stock."),
    ];

    /// <summary>RF-NEG-05: estados de los que no parte ninguna transición.</summary>
    public static IReadOnlyList<EstadoOrdenCompra> Terminales { get; } =
        Enum.GetValues<EstadoOrdenCompra>().Where(estado => Permitidas.All(t => t.Desde != estado)).ToList();

    public static bool EsTerminal(EstadoOrdenCompra estado) => Terminales.Contains(estado);

    /// <summary>Solo una orden en Borrador admite agregar o quitar líneas.</summary>
    public static bool AdmiteCambiosEnLineas(EstadoOrdenCompra estado) => estado == Borrador;

    public static Transicion? Buscar(EstadoOrdenCompra desde, EstadoOrdenCompra hacia) =>
        Permitidas.SingleOrDefault(t => t.Desde == desde && t.Hacia == hacia);

    public static TransicionProhibida? BuscarProhibida(EstadoOrdenCompra desde, EstadoOrdenCompra hacia) =>
        Prohibidas.SingleOrDefault(t => t.Desde == desde && t.Hacia == hacia);
}
