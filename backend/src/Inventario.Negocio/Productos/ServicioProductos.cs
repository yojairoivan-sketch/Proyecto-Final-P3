using Inventario.Core.ControlAcceso;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventario.Negocio.Productos;

public sealed record ProductoVista(
    int Id,
    string Nombre,
    string Sku,
    string UnidadMedida,
    decimal PuntoReorden,
    decimal Existencia,
    bool StockBajo,
    DateTimeOffset CreadoEn);

/// <summary>
/// Funcionalidad 2: el catálogo de productos con su existencia, y funcionalidad 7: la alerta de stock bajo.
/// Quién puede llamar a cada operación lo decide el host (RF-CA-05). Un producto nace con existencia 0:
/// solo la cambian los movimientos (recibir una orden, una salida o un ajuste).
/// </summary>
public interface IServicioProductos
{
    Task<Resultado<ProductoVista>> RegistrarAsync(
        string? nombre, string? sku, string? unidadMedida, decimal? puntoReorden, CancellationToken ct = default);

    Task<Resultado<IReadOnlyList<ProductoVista>>> ListarAsync(CancellationToken ct = default);

    /// <summary>Los productos cuya existencia está por debajo de su punto de reorden: hay que pedirlos.</summary>
    Task<Resultado<IReadOnlyList<ProductoVista>>> ListarStockBajoAsync(CancellationToken ct = default);
}

internal sealed class ServicioProductos(InventarioDbContext db, IUsuarioActual usuarioActual, TimeProvider reloj) : IServicioProductos
{
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoSku = 50;
    public const int LargoMaximoUnidad = 20;

    public async Task<Resultado<ProductoVista>> RegistrarAsync(
        string? nombre, string? sku, string? unidadMedida, decimal? puntoReorden, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<ProductoVista>();

        var (nombreValido, errorNombre) = ValidacionNegocio.Texto(nombre, "el nombre", LargoMaximoNombre);
        var (skuValido, errorSku) = ValidacionNegocio.Texto(sku, "el SKU", LargoMaximoSku);
        if (skuValido?.Any(char.IsWhiteSpace) == true)
            errorSku = "El SKU no puede tener espacios.";
        var (unidadValida, errorUnidad) = ValidacionNegocio.Texto(unidadMedida, "la unidad de medida", LargoMaximoUnidad);
        var errorPunto = ValidacionNegocio.Numero(puntoReorden, "el punto de reorden", ValidacionNegocio.CantidadMaxima, admiteCero: true);
        if (ValidacionNegocio.Rechazo<ProductoVista>(errorNombre, errorSku, errorUnidad, errorPunto) is { } rechazo)
            return rechazo;

        skuValido = skuValido!.ToUpperInvariant();
        if (await db.Productos.AnyAsync(p => p.Sku == skuValido, ct))
            return SkuDuplicado(skuValido);

        var producto = Producto.Crear(nombreValido!, skuValido, unidadValida!, puntoReorden!.Value, reloj.GetUtcNow());
        db.Productos.Add(producto);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos registros simultáneos con el mismo SKU: el índice único decide.
            return SkuDuplicado(skuValido);
        }

        return Resultado<ProductoVista>.Ok(Vista(producto), $"Producto {producto.Nombre} registrado con existencia 0.");
    }

    public async Task<Resultado<IReadOnlyList<ProductoVista>>> ListarAsync(CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<IReadOnlyList<ProductoVista>>();

        return Resultado<IReadOnlyList<ProductoVista>>.Ok((await TodosAsync(ct)).Select(Vista).ToList());
    }

    public async Task<Resultado<IReadOnlyList<ProductoVista>>> ListarStockBajoAsync(CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<IReadOnlyList<ProductoVista>>();

        // Un negocio pequeño tiene pocos cientos de productos: se filtra en memoria con la misma regla de la entidad.
        return Resultado<IReadOnlyList<ProductoVista>>.Ok((await TodosAsync(ct)).Where(p => p.StockBajo).Select(Vista).ToList());
    }

    private Task<List<Producto>> TodosAsync(CancellationToken ct) =>
        db.Productos.AsNoTracking().OrderBy(p => p.Nombre).ThenBy(p => p.Id).ToListAsync(ct);

    private static Resultado<ProductoVista> SkuDuplicado(string sku) =>
        Resultado<ProductoVista>.Falla(TipoFallo.Conflicto, $"Ya existe un producto con el SKU {sku}.");

    internal static ProductoVista Vista(Producto p) =>
        new(p.Id, p.Nombre, p.Sku, p.UnidadMedida, p.PuntoReorden, p.Existencia, p.StockBajo, p.CreadoEn);
}
