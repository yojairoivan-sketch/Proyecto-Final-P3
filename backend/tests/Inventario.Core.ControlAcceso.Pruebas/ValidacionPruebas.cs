using Inventario.Core.ControlAcceso.Seguridad;
using Inventario.Core.ControlAcceso.Validacion;

namespace Inventario.Core.ControlAcceso.Pruebas;

/// <summary>RF-CA-14: política mínima de contraseña.</summary>
public class PoliticaContrasenaPruebas
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab123")]
    [InlineData("soloLetras")]
    [InlineData("12345678")]
    public void RechazaLasQueNoCumplen(string? contrasena) =>
        Assert.NotNull(PoliticaContrasena.Validar(contrasena));

    [Fact]
    public void RechazaLasDeMasDe128Caracteres() =>
        Assert.NotNull(PoliticaContrasena.Validar(new string('a', 128) + "1"));

    [Theory]
    [InlineData("clave1234")]
    [InlineData("Contraseña2026")]
    public void AceptaLasQueCumplen(string contrasena) =>
        Assert.Null(PoliticaContrasena.Validar(contrasena));
}

/// <summary>RD-07: un dato vacío o mal formado da un mensaje controlado, nunca una excepción.</summary>
public class ValidacionEntradaPruebas
{
    [Fact]
    public void NormalizaElCorreo()
    {
        var (correo, error) = ValidacionEntrada.Correo("  Ana@Ejemplo.COM ");
        Assert.Null(error);
        Assert.Equal("ana@ejemplo.com", correo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("ana@@ejemplo")]
    [InlineData("sin-arroba")]
    [InlineData("ana@ejemplo")]
    [InlineData("con espacio@ejemplo.com")]
    public void RechazaCorreosVaciosOMalFormados(string? valor)
    {
        var (correo, error) = ValidacionEntrada.Correo(valor);
        Assert.Null(correo);
        Assert.NotNull(error);
    }

    [Fact]
    public void ExigeElNombre()
    {
        Assert.NotNull(ValidacionEntrada.Nombre("   ").Error);
        Assert.Equal("Ana", ValidacionEntrada.Nombre("  Ana ").Nombre);
    }
}

/// <summary>RF-CA-02 y RD-05: contraseñas con hash y sal; tokens guardados como SHA-256.</summary>
public class SecretosPruebas
{
    [Fact]
    public void ElHashNoContieneLaContrasena()
    {
        var hash = Secretos.HashDeContrasena("clave1234");
        Assert.DoesNotContain("clave1234", hash);
        Assert.True(Secretos.Verificar(hash, "clave1234").Correcta);
        Assert.False(Secretos.Verificar(hash, "clave12345").Correcta);
    }

    [Fact]
    public void LaMismaContrasenaDaHashesDistintos() =>
        Assert.NotEqual(Secretos.HashDeContrasena("clave1234"), Secretos.HashDeContrasena("clave1234"));

    [Fact]
    public void ElTokenSeGuardaComoSha256()
    {
        var token = Secretos.NuevoToken();
        var hash = Secretos.HashDeToken(token);
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.NotEqual(token, hash);
        Assert.Equal(hash, Secretos.HashDeToken(token));
        Assert.NotEqual(token, Secretos.NuevoToken());
    }
}
