using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Existencias;
using Inventario.Negocio.Ordenes;
using static Inventario.Negocio.Ordenes.EstadoOrdenCompra;

namespace Inventario.Negocio.Pruebas;

/// <summary>
/// Funcionalidades 3 y 4: armar una orden de compra y llevarla por su ciclo a través de IServicioOrdenes, como lo usa la API.
/// Comprueban que lo que decide la máquina de estados es lo que queda guardado, y que un rechazo no guarda nada.
/// </summary>
public sealed class OrdenesPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task UnaOrdenNuevaSeGuardaEnBorradorSinLineas()
    {
        // Preparar
        var proveedor = await entorno.CrearProveedorAsync();

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Ana).CrearAsync(proveedor);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardada = entorno.LeerOrden(resultado.Valor!.Id);
        Assert.Equal(Borrador, guardada.Estado);
        Assert.Empty(guardada.Lineas);
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData(null)]
    [InlineData(999)]
    public async Task UnaOrdenSinUnProveedorQueExistaNoSeCrea(int? proveedorId)
    {
        // Preparar
        var servicio = entorno.Ordenes(Entorno.Ana);

        // Actuar
        var resultado = await servicio.CrearAsync(proveedorId);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(0, entorno.Contar<OrdenDeCompra>());
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task AgregarUnaLineaLaGuardaConSuSubtotalYElTotalDeLaOrden()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        var orden = await entorno.CrearOrdenAsync(arroz, cantidad: 10, costo: 850);
        var aceite = await entorno.CrearProductoAsync("ACE-1", nombre: "Aceite");

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Ana).AgregarLineaAsync(orden, aceite, 24, 210.50m);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(new[] { 8500m, 5052m }, resultado.Valor!.Lineas.Select(l => l.Subtotal));
        Assert.Equal(13552m, resultado.Valor.Total);
        Assert.Equal(2, entorno.LeerOrden(orden).Lineas.Count);
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData(true, "0", "850")]
    [InlineData(true, "-3", "850")]
    [InlineData(true, "10", "-1")]
    [InlineData(true, "1.234", "850")]
    [InlineData(true, null, "850")]
    [InlineData(false, "10", "850")]
    public async Task UnaLineaConDatosInvalidosNoSeAgrega(bool conProducto, string? cantidad, string? costo)
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        var orden = (await entorno.Ordenes(Entorno.Ana).CrearAsync(await entorno.CrearProveedorAsync())).Valor!.Id;

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Ana)
            .AgregarLineaAsync(orden, conProducto ? arroz : null, Entorno.Numero(cantidad), Entorno.Numero(costo));

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Empty(entorno.LeerOrden(orden).Lineas);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task ElMismoProductoNoSeAgregaDosVecesAUnaOrden()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        var orden = await entorno.CrearOrdenAsync(arroz);

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Ana).AgregarLineaAsync(orden, arroz, 5, 850);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Single(entorno.LeerOrden(orden).Lineas);
    }

    [Fact]
    [Trait("Requisito", "RD-04")]
    public async Task UnaOrdenEnviadaNoAdmiteLineasNuevas()
    {
        // Preparar
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());
        await entorno.LlevarAsync(orden, Enviada);
        var aceite = await entorno.CrearProductoAsync("ACE-1", nombre: "Aceite");

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Carla).AgregarLineaAsync(orden, aceite, 24, 210);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Single(entorno.LeerOrden(orden).Lineas);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public async Task RecibirUnaOrdenGuardaJuntosElEstadoLaExistenciaYElMovimiento()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        var orden = await entorno.CrearOrdenAsync(arroz, cantidad: 10);
        await entorno.LlevarAsync(orden, Enviada);

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Ana).CambiarEstadoAsync(orden, "Recibida");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Recibida, entorno.LeerOrden(orden).Estado);
        Assert.Equal(10, entorno.LeerProducto(arroz).Existencia);
        var entrada = Assert.Single(entorno.LeerMovimientos(arroz));
        Assert.Equal((TipoMovimiento.Entrada, 10m, orden, Entorno.Ana.Id), (entrada.Tipo, entrada.Cantidad, entrada.OrdenDeCompraId!.Value, entrada.UsuarioId));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-04")]
    public async Task UnaTransicionProhibidaPorElServicioNoGuardaNada()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        var orden = await entorno.CrearOrdenAsync(arroz, cantidad: 10);
        await entorno.LlevarAsync(orden, Enviada, Recibida);
        var antes = entorno.LeerOrden(orden);

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Carla).CambiarEstadoAsync(orden, "Cancelada");

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        var despues = entorno.LeerOrden(orden);
        Assert.Equal((Recibida, antes.Version, antes.ActualizadaEn), (despues.Estado, despues.Version, despues.ActualizadaEn));
        Assert.Equal(10, entorno.LeerProducto(arroz).Existencia);
        Assert.Single(entorno.LeerMovimientos(arroz));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public async Task UnaOrdenSinLineasNoSeEnviaYSigueGuardadaEnBorrador()
    {
        // Preparar
        var orden = (await entorno.Ordenes(Entorno.Carla).CrearAsync(await entorno.CrearProveedorAsync())).Valor!.Id;

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Carla).CambiarEstadoAsync(orden, "Enviada");

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(Borrador, entorno.LeerOrden(orden).Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task UnEstandarNoEnviaUnaOrdenYNadaSeGuarda()
    {
        // Preparar
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Ana).CambiarEstadoAsync(orden, "Enviada");

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(Borrador, entorno.LeerOrden(orden).Estado);
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Volar")]
    [InlineData("2")]
    public async Task PasarAUnEstadoQueNoExisteSeRechazaSinTocarLaOrden(string? hacia)
    {
        // Preparar
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Carla).CambiarEstadoAsync(orden, hacia);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(Borrador, entorno.LeerOrden(orden).Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task LasOrdenesSeFiltranPorEstado()
    {
        // Preparar: una en Borrador y otra Enviada.
        var arroz = await entorno.CrearProductoAsync();
        await entorno.CrearOrdenAsync(arroz);
        var enviada = await entorno.CrearOrdenAsync(arroz);
        await entorno.LlevarAsync(enviada, Enviada);

        // Actuar
        var resultado = await entorno.Ordenes(Entorno.Ana).ListarAsync("enviada");

        // Verificar
        Assert.Equal(enviada, Assert.Single(resultado.Valor!).Id);
    }
}
