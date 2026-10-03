using Inventario.Core.ControlAcceso.Usuarios;

namespace Inventario.Core.ControlAcceso.Pruebas;

/// <summary>
/// RF-CA-04, 08, 20 y 21. Quién puede llamar a cada operación lo decide el host (RF-CA-05);
/// aquí se prueban las reglas del dominio que aplican después.
/// </summary>
public sealed class AdministracionPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    public void ElListadoNoTieneCamposParaHashesNiTokens()
    {
        var campos = typeof(UsuarioListado).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(campos, c => c.Contains("Hash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(campos, c => c.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(campos, c => c.Contains("Contrasena", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ElListadoMuestraRolYEstado()
    {
        await entorno.CrearAdministradorAsync("admin@ejemplo.com");
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        await entorno.Cuentas().RegistrarAsync("Beto", "beto@ejemplo.com", "clave1234");

        var usuarios = await entorno.Usuarios().ListarAsync();

        Assert.Collection(usuarios,
            u => Assert.Equal((Rol.Administrador, "Activo"), (u.Rol, u.Estado)),
            u => Assert.Equal((Rol.Estandar, "Activo"), (u.Rol, u.Estado)),
            u => Assert.Equal((Rol.Estandar, "Pendiente de activación"), (u.Rol, u.Estado)));
    }

    [Fact]
    public async Task CambiaElRolYRechazaUnRolDesconocido()
    {
        await entorno.CrearAdministradorAsync("admin@ejemplo.com");
        var id = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");

        Assert.Equal(TipoFallo.Validacion, (await entorno.Usuarios().CambiarRolAsync(id, "Jefe")).Fallo);
        Assert.True((await entorno.Usuarios().CambiarRolAsync(id, "Administrador")).Exito);
        Assert.Equal(Rol.Administrador, entorno.BuscarUsuario("ana@ejemplo.com").Rol.Nombre);
    }

    [Fact]
    public async Task NoSeLeQuitaElRolAlUltimoAdministrador()
    {
        var admin = await entorno.CrearAdministradorAsync("admin@ejemplo.com");

        var resultado = await entorno.Usuarios().CambiarRolAsync(admin, "Estandar");

        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(Rol.Administrador, entorno.BuscarUsuario("admin@ejemplo.com").Rol.Nombre);
    }

    [Fact]
    public async Task UnAdministradorNoPuedeDesactivarseASiMismo()
    {
        var admin = await entorno.CrearAdministradorAsync("admin@ejemplo.com");

        var resultado = await entorno.Usuarios().DesactivarAsync(admin, admin);

        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.True(entorno.BuscarUsuario("admin@ejemplo.com").Activo);
    }

    [Fact]
    public async Task DesactivarCierraLasSesionesEImpideEntrarHastaReactivar()
    {
        var admin = await entorno.CrearAdministradorAsync("admin@ejemplo.com");
        var id = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        var token = await entorno.IniciarSesionAsync("ana@ejemplo.com");

        Assert.True((await entorno.Usuarios().DesactivarAsync(admin, id)).Exito);
        Assert.Null(await entorno.Sesiones().ValidarAsync(token));
        Assert.Equal(TipoFallo.CuentaInactiva, (await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234")).Fallo);

        Assert.True((await entorno.Usuarios().ReactivarAsync(id)).Exito);
        Assert.True((await entorno.Sesiones().IniciarAsync("ana@ejemplo.com", "clave1234")).Exito);
    }
}
