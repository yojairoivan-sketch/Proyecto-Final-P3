namespace Inventario.Core.ControlAcceso.Pruebas;

/// <summary>RF-CA-01, 04, 15, 16 y 17.</summary>
public sealed class RegistroYActivacionPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    public async Task ElUsuarioNaceEstandarEInactivoYElEnlaceSalePorLaCola()
    {
        var resultado = await entorno.Cuentas().RegistrarAsync("Ana", "Ana@Ejemplo.com", "clave1234");

        Assert.True(resultado.Exito);
        var usuario = entorno.BuscarUsuario("ana@ejemplo.com");
        Assert.False(usuario.Activo);
        Assert.Equal(Rol.Estandar, usuario.Rol.Nombre);
        var correo = Assert.Single(entorno.Cola.Correos);
        Assert.Equal("ana@ejemplo.com", correo.Destinatario);
        Assert.Contains("http://localhost:8080/activar?token=", correo.Cuerpo);
    }

    [Fact]
    public async Task RechazaUnCorreoYaRegistrado()
    {
        await entorno.Cuentas().RegistrarAsync("Ana", "ana@ejemplo.com", "clave1234");

        var segundo = await entorno.Cuentas().RegistrarAsync("Otra Ana", "ANA@ejemplo.com", "otra12345");

        Assert.False(segundo.Exito);
        Assert.Equal(TipoFallo.Conflicto, segundo.Fallo);
    }

    [Fact]
    public async Task UnaContrasenaInvalidaNoCreaLaCuentaNiEncola()
    {
        var resultado = await entorno.Cuentas().RegistrarAsync("Ana", "ana@ejemplo.com", "ab123");

        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Empty(entorno.NuevoContexto().Usuarios);
        Assert.Empty(entorno.Cola.Correos);
    }

    [Fact]
    public async Task ElEnlaceActivaLaCuentaUnaSolaVez()
    {
        await entorno.Cuentas().RegistrarAsync("Ana", "ana@ejemplo.com", "clave1234");
        var token = entorno.Cola.UltimoValor("ana@ejemplo.com", "token");

        var primera = await entorno.Cuentas().ActivarAsync(token);
        var segunda = await entorno.Cuentas().ActivarAsync(token);

        Assert.True(primera.Exito);
        Assert.False(segunda.Exito);
        Assert.True(entorno.BuscarUsuario("ana@ejemplo.com").Activo);
    }

    [Fact]
    public async Task UnEnlaceVencidoSeRechazaYLaCuentaNoCambia()
    {
        await entorno.Cuentas().RegistrarAsync("Ana", "ana@ejemplo.com", "clave1234");
        var token = entorno.Cola.UltimoValor("ana@ejemplo.com", "token");

        entorno.Reloj.Advance(TimeSpan.FromHours(25));
        var resultado = await entorno.Cuentas().ActivarAsync(token);

        Assert.False(resultado.Exito);
        Assert.Contains("venció", resultado.Mensaje);
        Assert.False(entorno.BuscarUsuario("ana@ejemplo.com").Activo);
    }

    [Fact]
    public async Task ElReenvioRespondeIgualEInvalidaElEnlaceAnterior()
    {
        await entorno.Cuentas().RegistrarAsync("Ana", "ana@ejemplo.com", "clave1234");
        var anterior = entorno.Cola.UltimoValor("ana@ejemplo.com", "token");

        var existente = await entorno.Cuentas().ReenviarActivacionAsync("ana@ejemplo.com");
        var inexistente = await entorno.Cuentas().ReenviarActivacionAsync("nadie@ejemplo.com");
        var nuevo = entorno.Cola.UltimoValor("ana@ejemplo.com", "token");

        Assert.Equal(existente.Mensaje, inexistente.Mensaje);
        Assert.True(existente.Exito && inexistente.Exito);
        Assert.False((await entorno.Cuentas().ActivarAsync(anterior)).Exito);
        Assert.True((await entorno.Cuentas().ActivarAsync(nuevo)).Exito);
    }
}
