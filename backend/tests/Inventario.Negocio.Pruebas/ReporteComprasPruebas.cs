using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Reportes;
using static Inventario.Negocio.Ordenes.EstadoOrdenCompra;

namespace Inventario.Negocio.Pruebas;

/// <summary>Criterios R-02, R-05, R-06, R-07 y R-08: «¿Cuánto le compramos a cada proveedor en el período?» (RF-REP-02).</summary>
public sealed class ReporteComprasPruebas
{
    private static readonly DateTimeOffset Octubre = new(2026, 10, 31, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Noviembre = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Molinos: 3 órdenes recibidas (Carla 10 × 850 en octubre; Carla 2 × 850 y Ana 4 × 100 en noviembre).
    /// Lácteos: 1 orden recibida de Carla (5 × 60, noviembre). Y dos que no cuentan: una en Borrador y una Enviada.
    /// </summary>
    private static async Task<Entorno> ComprasAsync()
    {
        var entorno = new Entorno();
        var molinos = await entorno.CrearProveedorAsync("Molinos del Norte");
        var lacteos = await entorno.CrearProveedorAsync("Lácteos del Valle");
        var arroz = await entorno.CrearProductoAsync("ARR-1", nombre: "Arroz");
        var leche = await entorno.CrearProductoAsync("LEC-1", nombre: "Leche");

        entorno.Reloj.SetUtcNow(Octubre);
        await RecibirAsync(entorno, Entorno.Carla, molinos, arroz, 10, 850);
        entorno.Reloj.SetUtcNow(Noviembre);
        await RecibirAsync(entorno, Entorno.Carla, molinos, arroz, 2, 850);
        await RecibirAsync(entorno, Entorno.Ana, molinos, arroz, 4, 100);
        await RecibirAsync(entorno, Entorno.Carla, lacteos, leche, 5, 60);
        await CrearAsync(entorno, Entorno.Carla, molinos, arroz, 1, 999);
        await entorno.LlevarAsync(await CrearAsync(entorno, Entorno.Ana, lacteos, leche, 1, 999), Enviada);
        return entorno;
    }

    [Fact]
    [Trait("Requisito", "RF-REP-02")]
    public async Task ElAdministradorVeCuantoSeLeComproACadaProveedorSoloConLasOrdenesRecibidas()
    {
        // Preparar
        using var entorno = await ComprasAsync();

        // Actuar
        var reporte = (await entorno.ReporteCompras(Entorno.Carla).GenerarAsync("2026-10-01", "2026-11-30")).Valor!;

        // Verificar
        Assert.Equal(
            [("Molinos del Norte", 3, 10600m, 3533.33m), ("Lácteos del Valle", 1, 300m, 300m)],
            reporte.Proveedores.Select(p => (p.Proveedor, p.Ordenes, p.MontoTotal, p.PromedioPorOrden)));
        Assert.Equal((4, 10900m), (reporte.OrdenesRecibidas, reporte.MontoTotal));
        Assert.Contains("proveedor", reporte.Pregunta);
    }

    [Fact]
    [Trait("Requisito", "RF-REP-04")]
    public async Task UnEstandarSoloCuentaLasOrdenesQueElCreo()
    {
        // Preparar
        using var entorno = await ComprasAsync();

        // Actuar
        var reporte = (await entorno.ReporteCompras(Entorno.Ana).GenerarAsync("2026-10-01", "2026-11-30")).Valor!;

        // Verificar
        Assert.True(reporte.SoloLasMias);
        var fila = Assert.Single(reporte.Proveedores);
        Assert.Equal(("Molinos del Norte", 1, 400m), (fila.Proveedor, fila.Ordenes, fila.MontoTotal));
    }

    [Fact]
    [Trait("Requisito", "RF-REP-05")]
    public async Task DosRangosDistintosDanResultadosDistintosSobreLasMismasCompras()
    {
        // Preparar
        using var entorno = await ComprasAsync();

        // Actuar
        var octubre = (await entorno.ReporteCompras(Entorno.Carla).GenerarAsync("2026-10-01", "2026-10-31")).Valor!;
        var noviembre = (await entorno.ReporteCompras(Entorno.Carla).GenerarAsync("2026-11-01", "2026-11-30")).Valor!;

        // Verificar
        Assert.Equal((1, 8500m), (octubre.OrdenesRecibidas, octubre.MontoTotal));
        Assert.Equal((3, 2400m), (noviembre.OrdenesRecibidas, noviembre.MontoTotal));
    }

    [Fact]
    [Trait("Requisito", "RF-REP-05")]
    public async Task UnRangoSinComprasDevuelveCeroYNingunProveedor()
    {
        // Preparar
        using var entorno = await ComprasAsync();

        // Actuar
        var reporte = await entorno.ReporteCompras(Entorno.Carla).GenerarAsync("2026-09-01", "2026-09-30");

        // Verificar
        Assert.True(reporte.Exito, reporte.Mensaje);
        Assert.Equal((0, 0m), (reporte.Valor!.OrdenesRecibidas, reporte.Valor.MontoTotal));
        Assert.Empty(reporte.Valor.Proveedores);
    }

    [Fact]
    [Trait("Requisito", "RF-REP-05")]
    public async Task UnRangoInvalidoSeRechazaConMensaje()
    {
        // Preparar
        using var entorno = new Entorno();

        // Actuar
        var reporte = await entorno.ReporteCompras(Entorno.Carla).GenerarAsync("2026-11-30", "2026-11-01");

        // Verificar
        Assert.Equal(TipoFallo.Validacion, reporte.Fallo);
        Assert.Contains("posterior", reporte.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RD-06")]
    public async Task SinSesionNoHayReporteDeCompras()
    {
        // Preparar
        using var entorno = await ComprasAsync();

        // Actuar
        var reporte = await entorno.ReporteCompras(null).GenerarAsync("2026-10-01", "2026-11-30");

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, reporte.Fallo);
    }

    private static async Task<int> CrearAsync(Entorno entorno, UsuarioAutenticado quien, int proveedor, int producto, decimal cantidad, decimal costo)
    {
        var orden = await entorno.Ordenes(quien).CrearAsync(proveedor);
        Assert.True(orden.Exito, orden.Mensaje);
        var linea = await entorno.Ordenes(quien).AgregarLineaAsync(orden.Valor!.Id, producto, cantidad, costo);
        Assert.True(linea.Exito, linea.Mensaje);
        return orden.Valor.Id;
    }

    private static async Task RecibirAsync(Entorno entorno, UsuarioAutenticado quien, int proveedor, int producto, decimal cantidad, decimal costo) =>
        await entorno.LlevarAsync(await CrearAsync(entorno, quien, proveedor, producto, cantidad, costo), Enviada, Recibida);
}
