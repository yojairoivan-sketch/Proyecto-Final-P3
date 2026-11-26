using Inventario.Core.Auditoria;

namespace Inventario.Core.ControlAcceso.Pruebas;

/// <summary>
/// RF-AUD-04 y RF-AUD-05: cada cambio de rol, desactivación, reactivación y restablecimiento de contraseña deja su registro
/// en el historial, con todos sus datos (RF-AUD-01). El registro se lee de la misma base, con un contexto nuevo.
/// </summary>
public sealed class AuditoriaUsuariosPruebas : IDisposable
{
    private const string ClaveNueva = "nueva12345";

    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    // ---------- RF-AUD-04: cambio de rol, desactivación y reactivación ----------

    [Fact]
    [Trait("Requisito", "RF-AUD-04")]
    public async Task CambiarElRolDejaQuienLoCambioCuandoYElRolAnteriorYElNuevo()
    {
        // Preparar
        var jefa = await JefaAsync();
        var ana = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Usuarios().CambiarRolAsync(ana, "Administrador");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = Assert.Single(entorno.Historial());
        DeLaJefa(registro, jefa);
        Assert.Equal("CambiarRol", registro.Accion);
        Assert.Equal("Usuario", registro.Entidad);
        Assert.Equal(ana.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal("""{"rol":"Estándar"}""", registro.ValorAnterior);
        Assert.Equal("""{"rol":"Administrador"}""", registro.ValorNuevo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-04")]
    public async Task DesactivarDejaElEstadoAnteriorElNuevoYCuantasSesionesSeCerraron()
    {
        // Preparar
        var jefa = await JefaAsync();
        var ana = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        await entorno.IniciarSesionAsync("ana@ejemplo.com");
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Usuarios().DesactivarAsync(jefa, ana);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = Assert.Single(entorno.Historial());
        DeLaJefa(registro, jefa);
        Assert.Equal("DesactivarUsuario", registro.Accion);
        Assert.Equal("Usuario", registro.Entidad);
        Assert.Equal(ana.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal("""{"estado":"Activo"}""", registro.ValorAnterior);
        Assert.Equal("""{"estado":"Desactivado","sesionesCerradas":1}""", registro.ValorNuevo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-04")]
    public async Task ReactivarDejaElEstadoAnteriorYElNuevo()
    {
        // Preparar
        var jefa = await JefaAsync();
        var ana = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        Assert.True((await entorno.Usuarios().DesactivarAsync(jefa, ana)).Exito);
        entorno.Reloj.Advance(TimeSpan.FromHours(2));
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Usuarios().ReactivarAsync(ana);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var historial = entorno.Historial();
        Assert.Equal(new[] { "ReactivarUsuario", "DesactivarUsuario" }, historial.Select(r => r.Accion));
        var registro = historial[0];
        DeLaJefa(registro, jefa);
        Assert.Equal("Usuario", registro.Entidad);
        Assert.Equal(ana.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal("""{"estado":"Desactivado"}""", registro.ValorAnterior);
        Assert.Equal("""{"estado":"Activo"}""", registro.ValorNuevo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-04")]
    public async Task UnCambioQueNoOcurreNoDejaRegistro()
    {
        // Preparar: la jefa es la única Administradora activa, y Ana ya es Estándar.
        var jefa = await JefaAsync();
        var ana = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");

        // Actuar
        var ultimaAdministradora = await entorno.Usuarios().CambiarRolAsync(jefa, "Estandar");
        var mismoRol = await entorno.Usuarios().CambiarRolAsync(ana, "Estándar");
        var aSiMisma = await entorno.Usuarios().DesactivarAsync(jefa, jefa);

        // Verificar: ni un registro de un cambio que no pasó.
        Assert.False(ultimaAdministradora.Exito);
        Assert.True(mismoRol.Exito);
        Assert.False(aSiMisma.Exito);
        Assert.Empty(entorno.Historial());
    }

    // ---------- RF-AUD-05: restablecimiento de contraseña, sin guardar la contraseña ni el código ----------

    [Fact]
    [Trait("Requisito", "RF-AUD-05")]
    public async Task RestablecerConUnCodigoPedidoPorElPropioUsuarioDiceQueLoInicioElSinGuardarLaContrasenaNiElCodigo()
    {
        // Preparar: Ana pidió el código sin sesión (olvidó su contraseña) y tenía una sesión abierta en otro equipo.
        var ana = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        await entorno.IniciarSesionAsync("ana@ejemplo.com");
        await entorno.Contrasenas().SolicitarRecuperacionAsync("ana@ejemplo.com");
        var codigo = entorno.Cola.UltimoValor("ana@ejemplo.com", "codigo");
        entorno.Actual.Usuario = null;
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Contrasenas().RestablecerAsync(codigo, ClaveNueva);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = Assert.Single(entorno.Historial());
        Assert.Equal(ana, registro.UsuarioId);
        Assert.Equal("ana@ejemplo.com", registro.UsuarioCorreo);
        Assert.Equal("Estándar", registro.UsuarioRol);
        Assert.Equal("RestablecerContrasena", registro.Accion);
        Assert.Equal("Usuario", registro.Entidad);
        Assert.Equal(ana.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal("""{"contrasena":"la anterior (no se guarda)"}""", registro.ValorAnterior);
        Assert.Equal("""{"contrasena":"una nueva (no se guarda)","iniciadoPor":"PropioUsuario","sesionesCerradas":1}""", registro.ValorNuevo);
        SinSecretos(registro, codigo, "clave1234", ClaveNueva, entorno.BuscarUsuario("ana@ejemplo.com").HashContrasena);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-05")]
    public async Task ForzarElRestablecimientoDiceQueLoInicioUnAdministradorSinGuardarLaContrasenaNiElCodigo()
    {
        // Preparar
        var jefa = await JefaAsync();
        var ana = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        await entorno.IniciarSesionAsync("ana@ejemplo.com");
        var hashAnterior = entorno.BuscarUsuario("ana@ejemplo.com").HashContrasena;
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Contrasenas().ForzarRestablecimientoAsync(ana);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = Assert.Single(entorno.Historial());
        DeLaJefa(registro, jefa);
        Assert.Equal("ForzarRestablecimiento", registro.Accion);
        Assert.Equal("Usuario", registro.Entidad);
        Assert.Equal(ana.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal("""{"contrasena":"la anterior (no se guarda)"}""", registro.ValorAnterior);
        Assert.Equal(
            """{"contrasena":"anulada hasta que el usuario defina otra con el código que recibe por correo","iniciadoPor":"Administrador","sesionesCerradas":1}""",
            registro.ValorNuevo);
        var codigo = entorno.Cola.UltimoValor("ana@ejemplo.com", "codigo");
        SinSecretos(registro, codigo, "clave1234", hashAnterior, entorno.BuscarUsuario("ana@ejemplo.com").HashContrasena);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-05")]
    public async Task DefinirLaContrasenaConElCodigoDeUnRestablecimientoForzadoDiceQueLoInicioUnAdministrador()
    {
        // Preparar: la jefa forzó el restablecimiento y Ana usa el código que le llegó.
        await JefaAsync();
        var ana = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        Assert.True((await entorno.Contrasenas().ForzarRestablecimientoAsync(ana)).Exito);
        var codigo = entorno.Cola.UltimoValor("ana@ejemplo.com", "codigo");
        entorno.Actual.Usuario = null;

        // Actuar
        var resultado = await entorno.Contrasenas().RestablecerAsync(codigo, ClaveNueva);

        // Verificar: actúa Ana, pero lo inició la Administradora.
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = entorno.Historial()[0];
        Assert.Equal(ana, registro.UsuarioId);
        Assert.Equal("Estándar", registro.UsuarioRol);
        Assert.Equal("RestablecerContrasena", registro.Accion);
        Assert.Equal(ana.ToString(), registro.EntidadId);
        Assert.Equal("""{"contrasena":"una nueva (no se guarda)","iniciadoPor":"Administrador","sesionesCerradas":0}""", registro.ValorNuevo);
        SinSecretos(registro, codigo, ClaveNueva, entorno.BuscarUsuario("ana@ejemplo.com").HashContrasena);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-05")]
    public async Task CambiarLaContrasenaPropiaDiceQueLoInicioElPropioUsuario()
    {
        // Preparar
        var ana = await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        await entorno.IniciarSesionAsync("ana@ejemplo.com");
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Contrasenas().CambiarAsync(ana, "clave1234", ClaveNueva);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = Assert.Single(entorno.Historial());
        Assert.Equal(ana, registro.UsuarioId);
        Assert.Equal("ana@ejemplo.com", registro.UsuarioCorreo);
        Assert.Equal("Estándar", registro.UsuarioRol);
        Assert.Equal("CambiarContrasena", registro.Accion);
        Assert.Equal("Usuario", registro.Entidad);
        Assert.Equal(ana.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal("""{"contrasena":"la anterior (no se guarda)"}""", registro.ValorAnterior);
        Assert.Equal("""{"contrasena":"una nueva (no se guarda)","iniciadoPor":"PropioUsuario","sesionesCerradas":1}""", registro.ValorNuevo);
        SinSecretos(registro, "clave1234", ClaveNueva, entorno.BuscarUsuario("ana@ejemplo.com").HashContrasena);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-05")]
    public async Task UnRestablecimientoRechazadoNoDejaRegistro()
    {
        // Preparar
        await entorno.CrearUsuarioActivoAsync("ana@ejemplo.com");
        await entorno.Contrasenas().SolicitarRecuperacionAsync("ana@ejemplo.com");
        var codigo = entorno.Cola.UltimoValor("ana@ejemplo.com", "codigo");

        // Actuar: una contraseña que no cumple la política y un código inventado.
        var corta = await entorno.Contrasenas().RestablecerAsync(codigo, "corta1");
        var inventado = await entorno.Contrasenas().RestablecerAsync("codigo-inventado", ClaveNueva);

        // Verificar
        Assert.False(corta.Exito);
        Assert.False(inventado.Exito);
        Assert.Empty(entorno.Historial());
    }

    /// <summary>Crea a la Administradora «jefa» y la pone como usuario de la petición. Devuelve su id.</summary>
    private async Task<int> JefaAsync()
    {
        var id = await entorno.CrearAdministradorAsync("jefa@ejemplo.com");
        entorno.Actual.Usuario = new UsuarioAutenticado(id, "Jefa", "jefa@ejemplo.com", Rol.Administrador, SesionId: 1);
        return id;
    }

    private static void DeLaJefa(RegistroAuditoria registro, int jefa)
    {
        Assert.Equal(jefa, registro.UsuarioId);
        Assert.Equal("jefa@ejemplo.com", registro.UsuarioCorreo);
        Assert.Equal("Administrador", registro.UsuarioRol);
    }

    /// <summary>RF-AUD-05: ninguno de estos textos aparece en ningún campo del registro.</summary>
    private static void SinSecretos(RegistroAuditoria registro, params string[] secretos)
    {
        var todo = string.Join("|", registro.UsuarioCorreo, registro.UsuarioRol, registro.Accion, registro.Entidad,
            registro.EntidadId, registro.ValorAnterior, registro.ValorNuevo);
        foreach (var secreto in secretos)
            Assert.DoesNotContain(secreto, todo);
    }
}
