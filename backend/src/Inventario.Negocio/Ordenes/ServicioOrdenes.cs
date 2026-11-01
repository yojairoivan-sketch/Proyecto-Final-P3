using Inventario.Core.ControlAcceso;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Negocio.Ordenes;

public sealed record LineaVista(int Id, int ProductoId, string ProductoNombre, string Sku, decimal Cantidad, decimal CostoUnitario, decimal Subtotal);

public sealed record OrdenVista(
    int Id,
    int ProveedorId,
    string ProveedorNombre,
    EstadoOrdenCompra Estado,
    DateTimeOffset CreadaEn,
    DateTimeOffset ActualizadaEn,
    IReadOnlyList<LineaVista> Lineas,
    decimal Total);

/// <summary>
/// Funcionalidad 3: armar una orden de compra (crearla y agregarle líneas), y funcionalidad 4: su ciclo
/// (enviarla, recibirla o cancelarla). Quién puede hacer cada transición lo dice la máquina de estados
/// (<see cref="TransicionesOrdenCompra"/>), no este servicio.
/// </summary>
public interface IServicioOrdenes
{
    /// <summary>La orden nace en Borrador, sin líneas.</summary>
    Task<Resultado<OrdenVista>> CrearAsync(int? proveedorId, CancellationToken ct = default);

    Task<Resultado<OrdenVista>> AgregarLineaAsync(int ordenId, int? productoId, decimal? cantidad, decimal? costoUnitario, CancellationToken ct = default);

    Task<Resultado<OrdenVista>> ConsultarAsync(int id, CancellationToken ct = default);

    /// <summary>Todas las órdenes, las más nuevas primero, o solo las de un estado.</summary>
    Task<Resultado<IReadOnlyList<OrdenVista>>> ListarAsync(string? estado, CancellationToken ct = default);

    /// <summary>Intenta pasar la orden a otro estado. Si la máquina de estados lo rechaza, no se guarda nada.</summary>
    Task<Resultado<OrdenVista>> CambiarEstadoAsync(int id, string? hacia, CancellationToken ct = default);
}

internal sealed class ServicioOrdenes(InventarioDbContext db, IUsuarioActual usuarioActual, TimeProvider reloj) : IServicioOrdenes
{
    private static readonly string NombresDeEstados = string.Join(", ", Enum.GetNames<EstadoOrdenCompra>());

    public async Task<Resultado<OrdenVista>> CrearAsync(int? proveedorId, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<OrdenVista>();

        if (proveedorId is null)
            return Resultado<OrdenVista>.Falla(TipoFallo.Validacion, "Falta el proveedor (proveedorId).");
        var proveedor = await db.Proveedores.SingleOrDefaultAsync(p => p.Id == proveedorId, ct);
        if (proveedor is null)
            return Resultado<OrdenVista>.Falla(TipoFallo.Validacion, $"No existe un proveedor con id {proveedorId}.");

        var orden = OrdenDeCompra.Crear(proveedor, reloj.GetUtcNow());
        db.OrdenesDeCompra.Add(orden);
        await db.SaveChangesAsync(ct);
        return Resultado<OrdenVista>.Ok(Vista(orden), $"Orden de compra {orden.Id} creada en Borrador. Agrégale productos.");
    }

    public async Task<Resultado<OrdenVista>> AgregarLineaAsync(
        int ordenId, int? productoId, decimal? cantidad, decimal? costoUnitario, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<OrdenVista>();

        var errorProducto = productoId is null ? "Falta el producto (productoId)." : null;
        var errorCantidad = ValidacionNegocio.Numero(cantidad, "la cantidad", ValidacionNegocio.CantidadMaxima, admiteCero: false);
        var errorCosto = ValidacionNegocio.Numero(costoUnitario, "el costo unitario", ValidacionNegocio.CostoMaximo, admiteCero: true);
        if (ValidacionNegocio.Rechazo<OrdenVista>(errorProducto, errorCantidad, errorCosto) is { } rechazo)
            return rechazo;

        if (await CargarAsync(ordenId, ct) is not { } orden)
            return NoExiste();
        var producto = await db.Productos.SingleOrDefaultAsync(p => p.Id == productoId, ct);
        if (producto is null)
            return Resultado<OrdenVista>.Falla(TipoFallo.Validacion, $"No existe un producto con id {productoId}.");

        var agregada = orden.AgregarLinea(producto, cantidad!.Value, costoUnitario!.Value, reloj.GetUtcNow());
        if (!agregada.Exito)
            return Fallos.Como<OrdenVista>(agregada);

        return await Fallos.GuardarAsync<OrdenVista>(db, ct) ?? Resultado<OrdenVista>.Ok(Vista(orden), agregada.Mensaje);
    }

    public async Task<Resultado<OrdenVista>> ConsultarAsync(int id, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<OrdenVista>();

        return await CargarAsync(id, ct, soloLectura: true) is { } orden ? Resultado<OrdenVista>.Ok(Vista(orden)) : NoExiste();
    }

    public async Task<Resultado<IReadOnlyList<OrdenVista>>> ListarAsync(string? estado, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<IReadOnlyList<OrdenVista>>();

        var consulta = Completa(db.OrdenesDeCompra.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (Leer(estado) is not { } filtro)
                return Resultado<IReadOnlyList<OrdenVista>>.Falla(TipoFallo.Validacion,
                    $"El estado «{estado.Trim()}» no existe. Los estados son: {NombresDeEstados}.");
            consulta = consulta.Where(o => o.Estado == filtro);
        }

        var ordenes = await consulta.OrderByDescending(o => o.Id).ToListAsync(ct);
        return Resultado<IReadOnlyList<OrdenVista>>.Ok(ordenes.Select(Vista).ToList());
    }

    public async Task<Resultado<OrdenVista>> CambiarEstadoAsync(int id, string? hacia, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return Fallos.SinSesion<OrdenVista>();

        if (Leer(hacia) is not { } destino)
            return Resultado<OrdenVista>.Falla(TipoFallo.Validacion,
                $"Indica a qué estado pasa la orden (hacia): {NombresDeEstados}.");
        if (await CargarAsync(id, ct) is not { } orden)
            return NoExiste();

        var cambio = orden.CambiarEstado(destino, Responsable.De(usuario), reloj.GetUtcNow());
        if (!cambio.Exito)
            return Fallos.Como<OrdenVista>(cambio);

        // El estado, la existencia de cada producto y sus movimientos se guardan juntos, o nada.
        return await Fallos.GuardarAsync<OrdenVista>(db, ct) ?? Resultado<OrdenVista>.Ok(Vista(orden), cambio.Mensaje);
    }

    private Task<OrdenDeCompra?> CargarAsync(int id, CancellationToken ct, bool soloLectura = false)
    {
        var ordenes = soloLectura ? db.OrdenesDeCompra.AsNoTracking() : db.OrdenesDeCompra;
        return Completa(ordenes).SingleOrDefaultAsync(o => o.Id == id, ct);
    }

    private static IQueryable<OrdenDeCompra> Completa(IQueryable<OrdenDeCompra> ordenes) =>
        ordenes.Include(o => o.Proveedor).Include(o => o.Lineas).ThenInclude(l => l.Producto);

    /// <summary>El nombre de un estado, sin importar mayúsculas. Un número no cuenta como estado.</summary>
    private static EstadoOrdenCompra? Leer(string? texto) =>
        texto?.Trim() is { Length: > 0 } limpio && limpio.All(char.IsLetter)
            && Enum.TryParse<EstadoOrdenCompra>(limpio, ignoreCase: true, out var estado)
            ? estado
            : null;

    private static Resultado<OrdenVista> NoExiste() => Fallos.NoExiste<OrdenVista>("La orden de compra");

    private static OrdenVista Vista(OrdenDeCompra o) => new(
        o.Id, o.Proveedor.Id, o.Proveedor.Nombre, o.Estado, o.CreadaEn, o.ActualizadaEn,
        o.Lineas.Select(l => new LineaVista(l.Id, l.Producto.Id, l.Producto.Nombre, l.Producto.Sku, l.Cantidad, l.CostoUnitario, l.Subtotal)).ToList(),
        o.Total);
}
