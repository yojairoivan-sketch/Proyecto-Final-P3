using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Ordenes;

namespace Inventario.Negocio.Pruebas;

/// <summary>
/// Funcionalidad 8: cotizar una orden de compra en otra moneda con la tasa de la API externa (RD-10).
/// La API se reemplaza por <see cref="TasasFalsas"/>: aquí se prueba lo que hace el negocio con la tasa y con sus fallos.
/// </summary>
public sealed class CotizacionesPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    [Trait("Requisito", "RD-10")]
    public async Task CotizaElTotalDeLaOrdenConLaTasaDeLaApi()
    {
        // Preparar: 10 sacos a 850 DOP = 8500 DOP; la API dice 1 DOP = 0.0165 USD.
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync(), cantidad: 10, costo: 850);
        entorno.Tasas.Responder(0.0165m);

        // Actuar
        var resultado = await entorno.Cotizaciones(Entorno.Ana).CotizarOrdenAsync(orden, "USD");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var cotizacion = resultado.Valor!;
        Assert.Equal(8500m, cotizacion.Total);
        Assert.Equal("DOP", cotizacion.MonedaOrden);
        Assert.Equal("USD", cotizacion.Moneda);
        Assert.Equal(0.0165m, cotizacion.Tasa);
        Assert.Equal(140.25m, cotizacion.TotalConvertido);
        Assert.Equal(TasasFalsas.Publicada, cotizacion.TasaActualizadaEn);
        Assert.Equal(("DOP", "USD"), Assert.Single(entorno.Tasas.Consultas));
    }

    [Fact]
    [Trait("Requisito", "RD-10")]
    public async Task ElTotalConvertidoSeRedondeaADosDecimales()
    {
        // Preparar: 3 × 333.33 = 999.99 DOP × 0.01652 = 16.5198348 USD.
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync(), cantidad: 3, costo: 333.33m);
        entorno.Tasas.Responder(0.01652m);

        // Actuar
        var resultado = await entorno.Cotizaciones(Entorno.Ana).CotizarOrdenAsync(orden, "usd");

        // Verificar: el código se acepta en minúsculas y el total sale redondeado.
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal("USD", resultado.Valor!.Moneda);
        Assert.Equal(16.52m, resultado.Valor.TotalConvertido);
    }

    [Fact]
    [Trait("Requisito", "RD-10")]
    public async Task CotizarNoCambiaLaOrden()
    {
        // Preparar
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());
        var antes = entorno.LeerOrden(orden);
        entorno.Tasas.Responder(0.0165m);

        // Actuar
        var resultado = await entorno.Cotizaciones(Entorno.Ana).CotizarOrdenAsync(orden, "USD");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var despues = entorno.LeerOrden(orden);
        Assert.Equal(EstadoOrdenCompra.Borrador, despues.Estado);
        Assert.Equal(antes.Version, despues.Version);
        Assert.Equal(antes.Total, despues.Total);
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("DOLAR")]
    [InlineData("U$D")]
    [InlineData("DOP")]
    public async Task UnaMonedaInvalidaSeRechazaSinConsultarLaApi(string? moneda)
    {
        // Preparar
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());

        // Actuar
        var resultado = await entorno.Cotizaciones(Entorno.Ana).CotizarOrdenAsync(orden, moneda);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Empty(entorno.Tasas.Consultas);
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task UnaOrdenQueNoExisteSeRechazaSinConsultarLaApi()
    {
        // Preparar
        var servicio = entorno.Cotizaciones(Entorno.Ana);

        // Actuar
        var resultado = await servicio.CotizarOrdenAsync(999, "USD");

        // Verificar
        Assert.Equal(TipoFallo.NoEncontrado, resultado.Fallo);
        Assert.Empty(entorno.Tasas.Consultas);
    }

    [Fact]
    [Trait("Requisito", "RD-08")]
    public async Task SiLaApiNoEstaDisponibleLaCotizacionSeRechazaConSuMensaje()
    {
        // Preparar
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());
        entorno.Tasas.Fallar(TipoFallo.NoDisponible, "El servicio de tasas de cambio no respondió.");

        // Actuar
        var resultado = await entorno.Cotizaciones(Entorno.Ana).CotizarOrdenAsync(orden, "USD");

        // Verificar
        Assert.Equal(TipoFallo.NoDisponible, resultado.Fallo);
        Assert.Equal("El servicio de tasas de cambio no respondió.", resultado.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RD-08")]
    public async Task SiLaApiNoEstaDisponibleElRestoDelNegocioSigueFuncionando()
    {
        // Preparar
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());
        entorno.Tasas.Fallar(TipoFallo.NoDisponible, "Falta la clave.");
        Assert.False((await entorno.Cotizaciones(Entorno.Carla).CotizarOrdenAsync(orden, "USD")).Exito);

        // Actuar
        var enviada = await entorno.Ordenes(Entorno.Carla).CambiarEstadoAsync(orden, "Enviada");

        // Verificar
        Assert.True(enviada.Exito, enviada.Mensaje);
        Assert.Equal(EstadoOrdenCompra.Enviada, entorno.LeerOrden(orden).Estado);
    }

    [Fact]
    public async Task SinSesionNoSeCotiza()
    {
        // Preparar
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());
        entorno.Tasas.Responder(0.0165m);

        // Actuar
        var resultado = await entorno.Cotizaciones(null).CotizarOrdenAsync(orden, "USD");

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, resultado.Fallo);
        Assert.Empty(entorno.Tasas.Consultas);
    }
}
