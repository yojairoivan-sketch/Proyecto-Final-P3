namespace Inventario.Core.ControlAcceso.Pruebas;

/// <summary>RF-CA-03, 07, 18 y 19.</summary>
public sealed class SesionPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    public async Task AntesDeActivarSeRechazaConUnMensajeClaro()
    {
        await entorno.Cuentas().RegistrarAsync("Ana", "ana@ejemplo.com", "clave1234");

        var resultado = await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234");

        Assert.Equal(TipoFallo.CuentaInactiva, resultado.Fallo);
        Assert.Contains("no está activa", resultado.Mensaje);
    }

    [Fact]
    public async Task LosRechazosNoRevelanQueDatoFallo()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");

        var contrasenaMala = await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "mala12345");
        var correoInexistente = await entorno.Sesiones().IniciarAsync("nadie@ejemplo.com", "mala12345");

        Assert.Equal(TipoFallo.NoAutenticado, contrasenaMala.Fallo);
        Assert.Equal(contrasenaMala.Fallo, correoInexistente.Fallo);
        Assert.Equal(contrasenaMala.Mensaje, correoInexistente.Mensaje);
    }

    [Fact]
    public async Task LaCredencialIdentificaAlUsuarioYCerrarlaLaInvalida()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        var token = await entorno.IniciarSesionAsync("ana@ejemplo.com");

        var yo = await entorno.Sesiones().ValidarAsync(token);
        Assert.NotNull(yo);
        Assert.Equal("ana@ejemplo.com", yo.Correo);
        Assert.Equal(Rol.Estandar, yo.Rol);

        await entorno.Sesiones().CerrarAsync(yo.SesionId);
        Assert.Null(await entorno.Sesiones().ValidarAsync(token));
    }

    [Fact]
    public async Task LaSesionVence()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        var token = await entorno.IniciarSesionAsync("ana@ejemplo.com");

        entorno.Reloj.Advance(entorno.Opciones.VigenciaSesion + TimeSpan.FromMinutes(1));

        Assert.Null(await entorno.Sesiones().ValidarAsync(token));
    }

    [Fact]
    public async Task ElQuintoFalloBloqueaAunqueLuegoLaContrasenaSeaCorrecta()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        for (var i = 0; i < 5; i++)
            await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "mala12345");

        var sexto = await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234");
        Assert.Equal(TipoFallo.Bloqueado, sexto.Fallo);

        entorno.Reloj.Advance(TimeSpan.FromMinutes(15));
        var despues = await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234");
        Assert.True(despues.Exito);
        Assert.Equal(0, entorno.BuscarUsuario("ana@ejemplo.com").IntentosFallidos);
    }

    [Fact]
    public async Task UnInicioCorrectoPoneElContadorEnCero()
    {
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");

        for (var ronda = 0; ronda < 2; ronda++)
        {
            for (var i = 0; i < 4; i++)
                await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "mala12345");
            Assert.True((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234")).Exito);
        }
    }
}
