using Inventario.Core.ControlAcceso;
using Inventario.Core.Reportes;
using Inventario.Negocio.Ordenes;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Negocio.Reportes;

/// <summary>Lo comprado a un proveedor en el período: cuántas órdenes se recibieron y cuánto sumaron.</summary>
public sealed record FilaComprasProveedor(int ProveedorId, string Proveedor, int Ordenes, decimal MontoTotal, decimal PromedioPorOrden);

public sealed record ReporteComprasVista(
    string Pregunta,
    DateOnly Desde,
    DateOnly Hasta,
    bool SoloLasMias,
    int OrdenesRecibidas,
    decimal MontoTotal,
    IReadOnlyList<FilaComprasProveedor> Proveedores);

/// <summary>
/// RF-REP-02 (RF-NEG-08): «¿Cuánto le compramos a cada proveedor en el período?». Suma las órdenes que se recibieron entre
/// las dos fechas, por proveedor. Un Estándar solo cuenta las órdenes que él creó (RF-REP-04).
/// </summary>
public interface IReporteCompras
{
    Task<Resultado<ReporteComprasVista>> GenerarAsync(string? desde, string? hasta, CancellationToken ct = default);
}

internal sealed class ReporteCompras(InventarioDbContext db, IUsuarioActual usuarioActual) : IReporteCompras
{
    public const string Pregunta = "¿Cuánto le compramos a cada proveedor en el período? (órdenes recibidas, en DOP)";

    public async Task<Resultado<ReporteComprasVista>> GenerarAsync(string? desde, string? hasta, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return Fallos.SinSesion<ReporteComprasVista>();

        var leido = RangoFechas.Leer(desde, hasta);
        if (!leido.Exito)
            return Fallos.Como<ReporteComprasVista>(leido);
        var rango = leido.Valor!;

        var lineas = db.Set<LineaOrdenCompra>().AsNoTracking()
            .Where(l => l.OrdenDeCompra.Estado == EstadoOrdenCompra.Recibida
                && l.OrdenDeCompra.RecibidaEn >= rango.Inicio
                && l.OrdenDeCompra.RecibidaEn < rango.FinExclusivo);
        if (!usuario.EsAdministrador)
            lineas = lineas.Where(l => l.OrdenDeCompra.CreadaPorId == usuario.Id);

        // GROUP BY proveedor en la base, con COUNT(DISTINCT orden) y SUM(cantidad * costo): nunca se traen las órdenes (RF-REP-03).
        var grupos = await lineas
            .GroupBy(l => new { l.OrdenDeCompra.ProveedorId, l.OrdenDeCompra.Proveedor.Nombre })
            .Select(g => new
            {
                g.Key.ProveedorId,
                g.Key.Nombre,
                Ordenes = g.Select(l => l.OrdenDeCompraId).Distinct().Count(),
                Monto = g.Sum(l => l.Cantidad * l.CostoUnitario),
            })
            .ToListAsync(ct);

        var filas = grupos
            .Select(g => new FilaComprasProveedor(g.ProveedorId, g.Nombre, g.Ordenes, Limpio(g.Monto), Limpio(Math.Round(g.Monto / g.Ordenes, 2))))
            .OrderByDescending(f => f.MontoTotal)
            .ThenBy(f => f.Proveedor)
            .ToList();

        return Resultado<ReporteComprasVista>.Ok(new ReporteComprasVista(
            Pregunta, rango.Desde, rango.Hasta, !usuario.EsAdministrador,
            filas.Sum(f => f.Ordenes), filas.Sum(f => f.MontoTotal), filas));
    }

    /// <summary>Sin ceros de más (8500.0000 → 8500).</summary>
    private static decimal Limpio(decimal valor) => valor / 1.0000000000000000000000000000m;
}
