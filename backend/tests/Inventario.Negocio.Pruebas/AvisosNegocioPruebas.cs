using Inventario.Negocio.Ordenes;
using static Inventario.Negocio.Ordenes.EstadoOrdenCompra;

namespace Inventario.Negocio.Pruebas;

/// <summary>Criterio N-05 (RF-NOT-03, RF-NEG-07): el evento del negocio que notifica.</summary>
public sealed class AvisosNegocioPruebas
{
    [Fact]
    [Trait("Requisito", "RF-NOT-03")]
    public async Task RecibirUnaOrdenAvisaALosAdministradoresConLaOrdenYElProveedor()
    {
        // Preparar
        using var entorno = new Entorno();
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync(), cantidad: 10, costo: 850);
        await entorno.LlevarAsync(orden, Enviada);
        entorno.Notificador.Avisos.Clear();

        // Actuar
        await entorno.Ordenes(Entorno.Ana).CambiarEstadoAsync(orden, "Recibida");

        // Verificar
        var aviso = Assert.Single(entorno.Notificador.Avisos);
        Assert.Null(aviso.UsuarioId);
        Assert.Contains($"Orden de compra {orden} recibida", aviso.Titulo);
        Assert.Contains("Molinos del Norte", aviso.Mensaje);
        Assert.Contains("8500 DOP", aviso.Mensaje);
    }

    [Theory]
    [Trait("Requisito", "RF-NOT-03")]
    [InlineData(Enviada)]
    [InlineData(Cancelada)]
    public async Task LasOtrasTransicionesNoAvisan(EstadoOrdenCompra hacia)
    {
        // Preparar
        using var entorno = new Entorno();
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());
        entorno.Notificador.Avisos.Clear();

        // Actuar
        var cambio = await entorno.Ordenes(Entorno.Carla).CambiarEstadoAsync(orden, hacia.ToString());

        // Verificar
        Assert.True(cambio.Exito, cambio.Mensaje);
        Assert.Empty(entorno.Notificador.Avisos);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-03")]
    public async Task UnaRecepcionRechazadaNoAvisa()
    {
        // Preparar
        using var entorno = new Entorno();
        var orden = await entorno.CrearOrdenAsync(await entorno.CrearProductoAsync());
        entorno.Notificador.Avisos.Clear();

        // Actuar
        var cambio = await entorno.Ordenes(Entorno.Carla).CambiarEstadoAsync(orden, "Recibida");

        // Verificar
        Assert.False(cambio.Exito);
        Assert.Empty(entorno.Notificador.Avisos);
    }
}
