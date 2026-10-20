using Inventario.Core.ControlAcceso;
using Inventario.Core.GestionPermisos.Solicitudes;

namespace Inventario.Core.GestionPermisos.Pruebas;

/// <summary>
/// RF-GP-01 a RF-GP-07 a través de IServicioSolicitudes, como los usa la API, pero con Control de acceso
/// reemplazado por dobles: el usuario actual lo decide cada prueba y los permisos otorgados quedan anotados.
/// Lo guardado se lee con un contexto nuevo, para comprobar la base y no la memoria del servicio.
/// </summary>
public sealed class SolicitudesPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    public async Task UnEstandarCreaUnaSolicitudQueNaceEnPendiente()
    {
        // Preparar
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).CrearAsync("ListarUsuarios", "Hago el conteo mensual.");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardada = entorno.Leer(resultado.Valor!.Id);
        Assert.Equal(EstadoSolicitud.Pendiente, guardada.Estado);
        Assert.Equal(Entorno.Ana.Id, guardada.SolicitanteId);
        Assert.Equal("ListarUsuarios", guardada.Permiso);
        Assert.Equal(ahora, guardada.CreadaEn);
        Assert.Null(guardada.ResueltaEn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Volar")]
    [InlineData("CambiarRol")]
    public async Task CrearConUnPermisoQueNoSePuedePedirSeRechazaSinGuardarNada(string? permiso)
    {
        // Preparar: el catálogo del entorno solo tiene ListarUsuarios y ReactivarUsuario.

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).CrearAsync(permiso, justificacion: null);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(0, entorno.ContarSolicitudes());
    }

    [Fact]
    public async Task NoSeCreaUnaSegundaSolicitudDelMismoPermisoMientrasLaPrimeraNoSeResuelve()
    {
        // Preparar
        await entorno.CrearSolicitudAsync(Entorno.Ana, "ListarUsuarios");

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).CrearAsync("listarusuarios", justificacion: null);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(1, entorno.ContarSolicitudes());
    }

    [Fact]
    public async Task ElAdministradorVeSoloLasSolicitudesPendientes()
    {
        // Preparar
        var deAna = await entorno.CrearSolicitudAsync(Entorno.Ana);
        var deBeto = await entorno.CrearSolicitudAsync(Entorno.Beto);
        await entorno.Como(Entorno.Carla).RechazarAsync(deBeto, "Ya no hace falta.");

        // Actuar
        var pendientes = await entorno.Como(Entorno.Carla).ListarPendientesAsync();

        // Verificar
        var unica = Assert.Single(pendientes);
        Assert.Equal(deAna, unica.Id);
    }

    [Fact]
    public async Task UnEstandarSoloVeSusPropiasSolicitudes()
    {
        // Preparar
        var deAna = await entorno.CrearSolicitudAsync(Entorno.Ana);
        await entorno.CrearSolicitudAsync(Entorno.Beto);

        // Actuar
        var mias = await entorno.Como(Entorno.Ana).ListarMiasAsync();

        // Verificar
        var unica = Assert.Single(mias.Valor!);
        Assert.Equal(deAna, unica.Id);
    }

    [Fact]
    public async Task UnEstandarNoPuedeConsultarLaSolicitudDeOtroUsuario()
    {
        // Preparar
        var deBeto = await entorno.CrearSolicitudAsync(Entorno.Beto);

        // Actuar
        var resultado = await entorno.Como(Entorno.Ana).ConsultarAsync(deBeto);

        // Verificar
        Assert.Equal(TipoFallo.NoEncontrado, resultado.Fallo);
        Assert.Null(resultado.Valor);
    }

    [Fact]
    public async Task AprobarDejaGuardadoQuienAproboYCuando()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);
        entorno.Reloj.Advance(TimeSpan.FromHours(2));
        var momento = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).AprobarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardada = entorno.Leer(id);
        Assert.Equal(EstadoSolicitud.Aplicada, guardada.Estado);
        Assert.Equal(Entorno.Carla.Id, guardada.ResueltaPorId);
        Assert.Equal("Carla", guardada.ResueltaPorNombre);
        Assert.Equal(momento, guardada.ResueltaEn);
    }

    [Fact]
    public async Task RechazarConMotivoDejaGuardadosElEstadoYElMotivo()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).RechazarAsync(id, "Pídeselo a tu supervisor primero.");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardada = entorno.Leer(id);
        Assert.Equal(EstadoSolicitud.Rechazada, guardada.Estado);
        Assert.Equal("Pídeselo a tu supervisor primero.", guardada.MotivoRechazo);
        Assert.Equal(Entorno.Carla.Id, guardada.ResueltaPorId);
    }

    [Fact]
    public async Task RechazarSinMotivoNoCambiaLaSolicitudGuardada()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).RechazarAsync(id, motivo: null);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        var guardada = entorno.Leer(id);
        Assert.Equal(EstadoSolicitud.Pendiente, guardada.Estado);
        Assert.Null(guardada.ResueltaPorId);
    }

    [Fact]
    public async Task AlAprobarElSistemaOtorgaElPermisoAlSolicitanteSinOtroPaso()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana, "ListarUsuarios");

        // Actuar
        await entorno.Como(Entorno.Carla).AprobarAsync(id);

        // Verificar
        Assert.Equal([(Entorno.Ana.Id, "ListarUsuarios")], entorno.Otorgamiento.Otorgados);
        var guardada = entorno.Leer(id);
        Assert.Equal(EstadoSolicitud.Aplicada, guardada.Estado);
        Assert.NotNull(guardada.AplicadaEn);
    }

    [Fact]
    public async Task AlRechazarNoSeOtorgaNingunPermiso()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);

        // Actuar
        await entorno.Como(Entorno.Carla).RechazarAsync(id, "No.");

        // Verificar
        Assert.Empty(entorno.Otorgamiento.Otorgados);
    }

    [Fact]
    public async Task SiOtorgarFallaLaSolicitudQuedaAprobadaSinAplicar()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);
        entorno.Otorgamiento.FallarLaProxima = true;

        // Actuar
        var error = await Record.ExceptionAsync(() => entorno.Como(Entorno.Carla).AprobarAsync(id));

        // Verificar
        Assert.IsType<InvalidOperationException>(error);
        var guardada = entorno.Leer(id);
        Assert.Equal(EstadoSolicitud.Aprobada, guardada.Estado);
        Assert.Null(guardada.AplicadaEn);
    }

    [Fact]
    public async Task AlArrancarSeAplicanLasSolicitudesQueQuedaronAprobadas()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);
        entorno.Otorgamiento.FallarLaProxima = true;
        await Record.ExceptionAsync(() => entorno.Como(Entorno.Carla).AprobarAsync(id));

        // Actuar
        var aplicadas = await entorno.ComoSistema().AplicarAprobadasAsync();

        // Verificar
        Assert.Equal(1, aplicadas);
        Assert.Equal(EstadoSolicitud.Aplicada, entorno.Leer(id).Estado);
        Assert.Equal([(Entorno.Ana.Id, "ListarUsuarios")], entorno.Otorgamiento.Otorgados);
    }

    [Fact]
    public async Task AprobarDosVecesLaMismaSolicitudSeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);
        await entorno.Como(Entorno.Carla).AprobarAsync(id);
        var primera = entorno.Leer(id);
        entorno.Reloj.Advance(TimeSpan.FromHours(1));

        // Actuar
        var segunda = await entorno.Como(Entorno.Dario).AprobarAsync(id);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, segunda.Fallo);
        var guardada = entorno.Leer(id);
        Assert.Equal(EstadoSolicitud.Aplicada, guardada.Estado);
        Assert.Equal(primera.ResueltaPorId, guardada.ResueltaPorId);
        Assert.Equal(primera.ResueltaEn, guardada.ResueltaEn);
        Assert.Single(entorno.Otorgamiento.Otorgados);
    }

    [Fact]
    public async Task AprobarUnaSolicitudRechazadaSeRechazaYSigueRechazadaSinPermiso()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);
        await entorno.Como(Entorno.Carla).RechazarAsync(id, "No.");

        // Actuar
        var resultado = await entorno.Como(Entorno.Dario).AprobarAsync(id);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        var guardada = entorno.Leer(id);
        Assert.Equal(EstadoSolicitud.Rechazada, guardada.Estado);
        Assert.Null(guardada.AplicadaEn);
        Assert.Empty(entorno.Otorgamiento.Otorgados);
    }

    [Fact]
    public async Task UnAdministradorNoPuedeAprobarSuPropiaSolicitud()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Carla, "ReactivarUsuario");

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).AprobarAsync(id);

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(EstadoSolicitud.Pendiente, entorno.Leer(id).Estado);
        Assert.Empty(entorno.Otorgamiento.Otorgados);
    }

    [Fact]
    public async Task OtroAdministradorSiPuedeAprobarLaSolicitudDeUnAdministrador()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Carla, "ReactivarUsuario");

        // Actuar
        var resultado = await entorno.Como(Entorno.Dario).AprobarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Entorno.Dario.Id, entorno.Leer(id).ResueltaPorId);
    }
}
