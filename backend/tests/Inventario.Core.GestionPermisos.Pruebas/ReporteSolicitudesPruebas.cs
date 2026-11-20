using Inventario.Core.ControlAcceso;
using Inventario.Core.GestionPermisos.Solicitudes;
using static Inventario.Core.GestionPermisos.Pruebas.Entorno;

namespace Inventario.Core.GestionPermisos.Pruebas;

/// <summary>Criterios R-01, R-04, R-06, R-07 y R-08: el reporte de solicitudes por mes y estado (RF-REP-01).</summary>
public sealed class ReporteSolicitudesPruebas
{
    /// <summary>
    /// Seis solicitudes de tres usuarios en dos meses: en octubre, Ana (Rechazada) y Beto (Aplicada);
    /// en noviembre, Ana (Pendiente), Beto (Pendiente), Carla (Pendiente) y Ana otra vez (Aplicada).
    /// </summary>
    private static async Task<Entorno> SeisSolicitudesAsync()
    {
        var entorno = new Entorno();
        entorno.Reloj.SetUtcNow(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
        await entorno.Como(Dario).RechazarAsync(await entorno.CrearSolicitudAsync(Ana, "ListarUsuarios"), "No aplica");
        await entorno.Como(Dario).AprobarAsync(await entorno.CrearSolicitudAsync(Beto, "ListarUsuarios"));

        entorno.Reloj.SetUtcNow(new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero));
        await entorno.CrearSolicitudAsync(Ana, "ListarUsuarios");
        await entorno.CrearSolicitudAsync(Beto, "ReactivarUsuario");
        await entorno.CrearSolicitudAsync(Carla, "ListarUsuarios");
        await entorno.Como(Dario).AprobarAsync(await entorno.CrearSolicitudAsync(Ana, "ReactivarUsuario"));
        return entorno;
    }

    [Fact]
    [Trait("Requisito", "RF-REP-01")]
    public async Task ElAdministradorVeLasSolicitudesAgrupadasPorMesYEstadoConMenosFilasQueSolicitudes()
    {
        // Preparar
        using var entorno = await SeisSolicitudesAsync();

        // Actuar
        var reporte = (await entorno.Reporte(Carla).GenerarAsync("2026-10-01", "2026-11-30")).Valor!;

        // Verificar
        Assert.Equal(6, reporte.Total);
        Assert.Equal(
            [("2026-10", EstadoSolicitud.Aplicada, 1), ("2026-10", EstadoSolicitud.Rechazada, 1),
             ("2026-11", EstadoSolicitud.Pendiente, 3), ("2026-11", EstadoSolicitud.Aplicada, 1)],
            reporte.PorMes.Select(f => (f.Mes, f.Estado, f.Cantidad)).OrderBy(f => f.Mes).ThenBy(f => f.Estado));
        Assert.True(reporte.PorMes.Count < reporte.Total);
        Assert.Equal(reporte.Total, reporte.PorEstado.Sum(e => e.Cantidad));
    }

    [Fact]
    [Trait("Requisito", "RF-REP-04")]
    public async Task UnEstandarSoloCuentaSusPropiasSolicitudes()
    {
        // Preparar
        using var entorno = await SeisSolicitudesAsync();

        // Actuar
        var reporte = (await entorno.Reporte(Beto).GenerarAsync("2026-10-01", "2026-11-30")).Valor!;

        // Verificar
        Assert.True(reporte.SoloLasMias);
        Assert.Equal(2, reporte.Total);
        Assert.Equal([(EstadoSolicitud.Pendiente, 1), (EstadoSolicitud.Aplicada, 1)],
            reporte.PorEstado.Select(e => (e.Estado, e.Cantidad)));
    }

    [Fact]
    [Trait("Requisito", "RF-REP-05")]
    public async Task DosRangosDistintosDanResultadosDistintosSobreLosMismosDatos()
    {
        // Preparar
        using var entorno = await SeisSolicitudesAsync();

        // Actuar
        var octubre = (await entorno.Reporte(Carla).GenerarAsync("2026-10-01", "2026-10-31")).Valor!;
        var noviembre = (await entorno.Reporte(Carla).GenerarAsync("2026-11-01", "2026-11-30")).Valor!;

        // Verificar
        Assert.Equal((2, 4), (octubre.Total, noviembre.Total));
        Assert.All(octubre.PorMes, f => Assert.Equal("2026-10", f.Mes));
        Assert.All(noviembre.PorMes, f => Assert.Equal("2026-11", f.Mes));
    }

    [Fact]
    [Trait("Requisito", "RF-REP-05")]
    public async Task ElUltimoDiaDelRangoSeIncluyeCompletoYElSiguienteNo()
    {
        // Preparar
        using var entorno = new Entorno();
        entorno.Reloj.SetUtcNow(new DateTimeOffset(2026, 11, 30, 23, 59, 0, TimeSpan.Zero));
        await entorno.CrearSolicitudAsync(Ana, "ListarUsuarios");
        entorno.Reloj.SetUtcNow(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));
        await entorno.CrearSolicitudAsync(Beto, "ListarUsuarios");

        // Actuar
        var reporte = (await entorno.Reporte(Carla).GenerarAsync("2026-11-30", "2026-11-30")).Valor!;

        // Verificar
        Assert.Equal(1, reporte.Total);
    }

    [Theory]
    [Trait("Requisito", "RF-REP-05")]
    [InlineData("2026-12-01", "2026-11-01")]
    [InlineData("ayer", "2026-11-01")]
    public async Task UnRangoInvalidoSeRechazaSinGenerarElReporte(string desde, string hasta)
    {
        // Preparar
        using var entorno = new Entorno();

        // Actuar
        var reporte = await entorno.Reporte(Carla).GenerarAsync(desde, hasta);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, reporte.Fallo);
    }

    [Fact]
    [Trait("Requisito", "RD-06")]
    public async Task SinSesionNoHayReporte()
    {
        // Preparar
        using var entorno = await SeisSolicitudesAsync();

        // Actuar
        var reporte = await entorno.ReporteSinSesion().GenerarAsync("2026-10-01", "2026-11-30");

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, reporte.Fallo);
        Assert.Null(reporte.Valor);
    }
}
