using Inventario.Core.ControlAcceso;
using static Inventario.Negocio.Ordenes.EstadoOrdenCompra;

namespace Inventario.Negocio.Ordenes;

/// <summary>Una transición permitida: desde, hacia, qué roles la ejecutan y con qué condición.</summary>
/// <param name="Validar">Comprueba la condición; devuelve el motivo del rechazo o null si se cumple.</param>
/// <param name="AlAplicar">Efecto de la transición sobre la orden, si lo tiene.</param>
public sealed record Transicion(
    EstadoOrdenCompra Desde,
    EstadoOrdenCompra Hacia,
    IReadOnlyList<string> Quien,
    string Condicion,
    Func<OrdenDeCompra, string?>? Validar = null,
    Action<OrdenDeCompra, Responsable, DateTimeOffset>? AlAplicar = null);

/// <summary>Una transición prohibida de forma explícita, con el motivo que se le muestra al usuario.</summary>
public sealed record TransicionProhibida(EstadoOrdenCompra Desde, EstadoOrdenCompra Hacia, string Motivo);

/// <summary>
/// RD-04: el único punto del código donde se resuelven las transiciones de la orden de compra.
/// Agregar una transición nueva es agregar una línea a <see cref="Permitidas"/>; nadie más valida estados.
/// Lo que no está en Permitidas está prohibido. La tabla legible está en docs/maquina-de-estados.md.
/// Esta máquina no comparte código con la de Gestión de permisos (RF-NEG-09).
/// </summary>
public static class TransicionesOrdenCompra
{
    private static readonly string[] SoloAdministrador = [Rol.Administrador];
    private static readonly string[] CualquierRol = [Rol.Administrador, Rol.Estandar];

    public static readonly IReadOnlyList<Transicion> Permitidas =
    [
        new(Borrador, Enviada, SoloAdministrador, "La orden tiene al menos una línea.",
            Validar: null),
        new(Borrador, Cancelada, CualquierRol, "Ninguna."),
        new(Enviada, Recibida, CualquierRol, "Ninguna. Al recibirla, la cantidad de cada línea se suma a la existencia del producto.",
            AlAplicar: (orden, quien, ahora) =>
            {
                foreach (var linea in orden.Lineas)
                    linea.Producto.RegistrarEntrada(linea.Cantidad, orden, quien, ahora);
            }),
        new(Enviada, Cancelada, SoloAdministrador, "Ninguna."),
    ];

    /// <summary>
    /// RF-NEG-04: transiciones prohibidas de forma explícita. No están en <see cref="Permitidas"/>, así que nunca se ejecutan;
    /// esta lista dice por qué, para que el rechazo lo explique.
    /// </summary>
    public static readonly IReadOnlyList<TransicionProhibida> Prohibidas =
    [
        new(Enviada, Borrador,
            "Una orden enviada no vuelve a Borrador: el proveedor ya la tiene. Si hay que cambiarla, cancélala y haz otra."),
        new(Recibida, Cancelada,
            "Una orden recibida no se puede cancelar: la mercancía ya entró al inventario. Corrígelo con un ajuste de existencia."),
        new(Borrador, Recibida,
            "No se recibe una orden que no se envió: primero hay que enviársela al proveedor."),
    ];

    /// <summary>RF-NEG-05: estados de los que no parte ninguna transición.</summary>
    public static IReadOnlyList<EstadoOrdenCompra> Terminales { get; } =
        Enum.GetValues<EstadoOrdenCompra>().Where(estado => Permitidas.All(t => t.Desde != estado)).ToList();

    public static bool EsTerminal(EstadoOrdenCompra estado) => Terminales.Contains(estado);

    /// <summary>Solo una orden en Borrador admite agregar líneas.</summary>
    public static bool AdmiteCambiosEnLineas(EstadoOrdenCompra estado) => estado == Borrador;

    public static Transicion? Buscar(EstadoOrdenCompra desde, EstadoOrdenCompra hacia) =>
        Permitidas.SingleOrDefault(t => t.Desde == desde && t.Hacia == hacia);

    public static TransicionProhibida? BuscarProhibida(EstadoOrdenCompra desde, EstadoOrdenCompra hacia) =>
        Prohibidas.SingleOrDefault(t => t.Desde == desde && t.Hacia == hacia);

    /// <summary>
    /// Intenta llevar la orden a <paramref name="hacia"/>. Si la transición no está permitida, la ejecuta un rol que no debe
    /// o no se cumple su condición, se rechaza y la orden no cambia. Si pasa, aplica su efecto y cambia el estado.
    /// </summary>
    internal static Resultado Resolver(OrdenDeCompra orden, EstadoOrdenCompra hacia, Responsable quien, DateTimeOffset ahora)
    {
        var desde = orden.Estado;

        if (Buscar(desde, hacia) is not { } transicion)
            return Resultado.Falla(TipoFallo.Conflicto, $"{PorQueNoSePuede(desde, hacia)} La orden sigue {desde}.");

        if (!transicion.Quien.Contains(quien.Rol))
            return Resultado.Falla(TipoFallo.Prohibido,
                $"Pasar una orden de {desde} a {hacia} es solo para: {string.Join(" o ", transicion.Quien)}. " +
                $"Tu rol es {quien.Rol}. La orden sigue {desde}.");

        if (transicion.Validar?.Invoke(orden) is { } motivo)
            return Resultado.Falla(TipoFallo.Conflicto, $"{motivo} La orden sigue {desde}.");

        transicion.AlAplicar?.Invoke(orden, quien, ahora);
        orden.PasarA(hacia, ahora);
        return Resultado.Ok($"La orden pasó de {desde} a {hacia}.");
    }

    private static string PorQueNoSePuede(EstadoOrdenCompra desde, EstadoOrdenCompra hacia) =>
        BuscarProhibida(desde, hacia)?.Motivo
        ?? (EsTerminal(desde) ? $"La orden está {desde}, un estado terminal: ya no cambia de estado."
            : desde == hacia ? $"La orden ya está {desde}."
            : $"No se puede pasar una orden de {desde} a {hacia}.");
}
