using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Productos;
using Inventario.Negocio.Proveedores;

namespace Inventario.Negocio.Pruebas;

/// <summary>
/// Funcionalidades 1, 2 y 7: proveedores, productos y la alerta de stock bajo, a través de sus servicios.
/// Lo guardado se lee con un contexto nuevo, para comprobar la base y no la memoria del servicio.
/// </summary>
public sealed class CatalogoPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task RegistrarUnProveedorLoGuardaConSusDatos()
    {
        // Preparar
        var servicio = entorno.Proveedores(Entorno.Carla);

        // Actuar
        var resultado = await servicio.RegistrarAsync("  Molinos del Norte ", "809-555-0101", "Ventas@Molinos.com");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardado = Assert.Single((await servicio.ListarAsync()).Valor!);
        Assert.Equal(("Molinos del Norte", "809-555-0101", "ventas@molinos.com"), (guardado.Nombre, guardado.Telefono, guardado.Correo));
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData(null, null, null)]
    [InlineData("   ", null, null)]
    [InlineData("Molinos", "llamar al gerente", null)]
    [InlineData("Molinos", null, "ventas@@molinos")]
    public async Task UnProveedorConDatosInvalidosNoSeRegistra(string? nombre, string? telefono, string? correo)
    {
        // Preparar
        var servicio = entorno.Proveedores(Entorno.Carla);

        // Actuar
        var resultado = await servicio.RegistrarAsync(nombre, telefono, correo);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(0, entorno.Contar<Proveedor>());
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task SinSesionNoSeRegistraNiSeListaNada()
    {
        // Preparar: Control de acceso diría que la petición no trae una sesión válida.
        var productos = entorno.Productos(usuario: null);

        // Actuar
        var registro = await productos.RegistrarAsync("Arroz", "ARR-1", "saco", 5);
        var lista = await productos.ListarAsync();

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, registro.Fallo);
        Assert.Equal(TipoFallo.NoAutenticado, lista.Fallo);
        Assert.Equal(0, entorno.Contar<Producto>());
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task UnProductoNuevoSeGuardaConExistenciaCeroYElSkuEnMayusculas()
    {
        // Preparar
        var servicio = entorno.Productos(Entorno.Carla);

        // Actuar
        var resultado = await servicio.RegistrarAsync("Arroz selecto", "arr-1", "saco", 5);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardado = entorno.LeerProducto(resultado.Valor!.Id);
        Assert.Equal(("ARR-1", 0m, 5m), (guardado.Sku, guardado.Existencia, guardado.PuntoReorden));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task NoSeRegistranDosProductosConElMismoSku()
    {
        // Preparar
        await entorno.CrearProductoAsync(sku: "ARR-1");

        // Actuar
        var resultado = await entorno.Productos(Entorno.Carla).RegistrarAsync("Otro arroz", "arr-1", "saco", 5);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(1, entorno.Contar<Producto>());
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData("", "ARR-1", "saco", "5")]
    [InlineData("Arroz", "ARR 1", "saco", "5")]
    [InlineData("Arroz", "ARR-1", "", "5")]
    [InlineData("Arroz", "ARR-1", "saco", "-1")]
    [InlineData("Arroz", "ARR-1", "saco", "2.555")]
    [InlineData("Arroz", "ARR-1", "saco", null)]
    public async Task UnProductoConDatosInvalidosNoSeRegistra(string? nombre, string? sku, string? unidad, string? puntoReorden)
    {
        // Preparar
        var servicio = entorno.Productos(Entorno.Carla);

        // Actuar
        var resultado = await servicio.RegistrarAsync(nombre, sku, unidad, Entorno.Numero(puntoReorden));

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(0, entorno.Contar<Producto>());
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task LaAlertaDeStockBajoListaSoloLosProductosPorDebajoDeSuPuntoDeReorden()
    {
        // Preparar: arroz con 3 (reorden 5), aceite con 12 (reorden 12) y azúcar con 0 (reorden 0).
        var arroz = await entorno.CrearProductoAsync("ARR-1", puntoReorden: 5, nombre: "Arroz");
        var aceite = await entorno.CrearProductoAsync("ACE-1", puntoReorden: 12, nombre: "Aceite");
        await entorno.CrearProductoAsync("AZU-1", puntoReorden: 0, nombre: "Azúcar");
        await entorno.DarExistenciaAsync(arroz, 3);
        await entorno.DarExistenciaAsync(aceite, 12);

        // Actuar
        var resultado = await entorno.Productos(Entorno.Ana).ListarStockBajoAsync();

        // Verificar
        var bajo = Assert.Single(resultado.Valor!);
        Assert.Equal(arroz, bajo.Id);
        Assert.True(bajo.StockBajo);
    }
}
