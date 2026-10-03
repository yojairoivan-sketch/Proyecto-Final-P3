namespace Inventario.Core.ControlAcceso.Pruebas;

/// <summary>RF-CA-09 a 13 y 22.</summary>
public sealed class ContrasenasPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    public async Task LaRecuperacionRespondeIgualExistaONoElCorreo()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        var correosAntes = entorno.Cola.Correos.Count;

        var existente = await entorno.Contrasenas().SolicitarRecuperacionAsync("ana@ejemplo.com");
        var inexistente = await entorno.Contrasenas().SolicitarRecuperacionAsync("nadie@ejemplo.com");

        Assert.Equal(existente.Mensaje, inexistente.Mensaje);
        Assert.True(existente.Exito && inexistente.Exito);
        Assert.Equal(correosAntes + 1, entorno.Cola.Correos.Count);
    }

    [Fact]
    public async Task ElCodigoCambiaLaContrasenaUnaSolaVezYCierraLasSesiones()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        var sesionAnterior = await entorno.IniciarSesionAsync("ana@ejemplo.com");
        await entorno.Contrasenas().SolicitarRecuperacionAsync("ana@ejemplo.com");
        var codigo = entorno.Cola.UltimoValor("ana@ejemplo.com", "codigo");

        var restablecido = await entorno.Contrasenas().RestablecerAsync(codigo, "nueva12345");
        var reutilizado = await entorno.Contrasenas().RestablecerAsync(codigo, "otra12345");

        Assert.True(restablecido.Exito);
        Assert.False(reutilizado.Exito);
        Assert.Null(await entorno.Sesiones().ValidarAsync(sesionAnterior));
        Assert.False((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234")).Exito);
        Assert.True((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "nueva12345")).Exito);
    }

    [Fact]
    public async Task UnCodigoVencidoNoCambiaLaContrasena()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        await entorno.Contrasenas().SolicitarRecuperacionAsync("ana@ejemplo.com");
        var codigo = entorno.Cola.UltimoValor("ana@ejemplo.com", "codigo");

        entorno.Reloj.Advance(TimeSpan.FromMinutes(31));
        var resultado = await entorno.Contrasenas().RestablecerAsync(codigo, "nueva12345");

        Assert.False(resultado.Exito);
        Assert.True((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234")).Exito);
    }

    [Fact]
    public async Task LaContrasenaNuevaPasaPorLaPolitica()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        await entorno.Contrasenas().SolicitarRecuperacionAsync("ana@ejemplo.com");
        var codigo = entorno.Cola.UltimoValor("ana@ejemplo.com", "codigo");

        Assert.Equal(TipoFallo.Validacion, (await entorno.Contrasenas().RestablecerAsync(codigo, "corta1")).Fallo);
        Assert.True((await entorno.Contrasenas().RestablecerAsync(codigo, "nueva12345")).Exito);
    }

    [Fact]
    public async Task ElCambioConSesionExigeLaContrasenaActual()
    {
        var id = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");

        var incorrecta = await entorno.Contrasenas().CambiarAsync(id, "equivocada1", "nueva12345");

        Assert.Equal(TipoFallo.Validacion, incorrecta.Fallo);
        Assert.True((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234")).Exito);
    }

    [Fact]
    public async Task ElCambioConSesionCierraTodasLasSesiones()
    {
        var id = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        var token = await entorno.IniciarSesionAsync("ana@ejemplo.com");

        Assert.True((await entorno.Contrasenas().CambiarAsync(id, "clave1234", "nueva12345")).Exito);

        Assert.Null(await entorno.Sesiones().ValidarAsync(token));
        Assert.True((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "nueva12345")).Exito);
    }

    [Fact]
    public async Task ElRestablecimientoForzadoInvalidaLaContrasenaYEncolaUnCodigo()
    {
        var id = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        var token = await entorno.IniciarSesionAsync("ana@ejemplo.com");

        Assert.True((await entorno.Contrasenas().ForzarRestablecimientoAsync(id)).Exito);

        Assert.Null(await entorno.Sesiones().ValidarAsync(token));
        Assert.False((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234")).Exito);
        var codigo = entorno.Cola.UltimoValor("ana@ejemplo.com", "codigo");
        Assert.True((await entorno.Contrasenas().RestablecerAsync(codigo, "nueva12345")).Exito);
        Assert.True((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "nueva12345")).Exito);
    }
}
