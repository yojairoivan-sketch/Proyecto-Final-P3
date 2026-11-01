using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Existencias;

namespace Inventario.Negocio.Pruebas;

/// <summary>
/// Funcionalidades 5 y 6: salidas, historial y ajuste por conteo físico, a través de IServicioExistencias.
/// Aquí Gestión de permisos es un doble que dice qué está aprobado; con la pieza real, ver Inventario.Integracion.Pruebas.
/// </summary>
public sealed class ExistenciasPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task UnaSalidaRestaLaExistenciaYDejaElMovimiento()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        await entorno.DarExistenciaAsync(arroz, 20);

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Ana).RegistrarSalidaAsync(arroz, 4, "Venta de mostrador");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(16, entorno.LeerProducto(arroz).Existencia);
        var salida = entorno.LeerMovimientos(arroz)[^1];
        Assert.Equal((TipoMovimiento.Salida, -4m, 16m, "Venta de mostrador"), (salida.Tipo, salida.Cantidad, salida.ExistenciaResultante, salida.Motivo));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task UnaSalidaMayorQueLaExistenciaSeRechazaYLaExistenciaNoCambia()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        await entorno.DarExistenciaAsync(arroz, 3);

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Ana).RegistrarSalidaAsync(arroz, 4, "Venta");

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(3, entorno.LeerProducto(arroz).Existencia);
        Assert.Single(entorno.LeerMovimientos(arroz));
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData("0", "Venta")]
    [InlineData("-1", "Venta")]
    [InlineData(null, "Venta")]
    [InlineData("1", "  ")]
    public async Task UnaSalidaConDatosInvalidosNoSeRegistra(string? cantidad, string? motivo)
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        await entorno.DarExistenciaAsync(arroz, 3);

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Ana).RegistrarSalidaAsync(arroz, Entorno.Numero(cantidad), motivo);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(3, entorno.LeerProducto(arroz).Existencia);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task ElHistorialMuestraLosMovimientosDelProductoDelMasViejoAlMasNuevo()
    {
        // Preparar: un ajuste inicial y dos salidas.
        var arroz = await entorno.CrearProductoAsync();
        await entorno.DarExistenciaAsync(arroz, 20);
        await entorno.Existencias(Entorno.Ana).RegistrarSalidaAsync(arroz, 4, "Venta");
        await entorno.Existencias(Entorno.Ana).RegistrarSalidaAsync(arroz, 1, "Merma");

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Ana).ListarMovimientosAsync(arroz);

        // Verificar
        Assert.Equal(new[] { 20m, 16m, 15m }, resultado.Valor!.Select(m => m.ExistenciaResultante));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task UnAdministradorAjustaLaExistenciaPorSuRolSinConsultarPermisos()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Carla).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(37, entorno.LeerProducto(arroz).Existencia);
        Assert.Empty(entorno.Permisos.Consultas);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task UnEstandarSinElPermisoAprobadoNoAjustaLaExistencia()
    {
        // Preparar: Gestión de permisos no tiene aprobado nada para Ana.
        var arroz = await entorno.CrearProductoAsync();

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Ana).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(0, entorno.LeerProducto(arroz).Existencia);
        Assert.Empty(entorno.LeerMovimientos(arroz));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task ParaUnEstandarSeLePreguntaAGestionDePermisosPorSuPermisoDeAjuste()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();

        // Actuar
        await entorno.Existencias(Entorno.Ana).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar
        Assert.Equal([(Entorno.Ana.Id, PermisosNegocio.AjustarExistencia)], entorno.Permisos.Consultas);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task UnEstandarConElPermisoAprobadoAjustaLaExistencia()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        entorno.Permisos.Concedidos.Add((Entorno.Ana.Id, PermisosNegocio.AjustarExistencia));

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Ana).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var ajuste = Assert.Single(entorno.LeerMovimientos(arroz));
        Assert.Equal((TipoMovimiento.Ajuste, 37m, Entorno.Ana.Id), (ajuste.Tipo, ajuste.Cantidad, ajuste.UsuarioId));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task ElPermisoDeOtroUsuarioNoLeSirveAlEstandar()
    {
        // Preparar: el permiso está aprobado para Beto, no para Ana.
        var arroz = await entorno.CrearProductoAsync();
        entorno.Permisos.Concedidos.Add((Entorno.Beto.Id, PermisosNegocio.AjustarExistencia));

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Ana).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(0, entorno.LeerProducto(arroz).Existencia);
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData("37", null)]
    [InlineData("37", "")]
    [InlineData("-5", "Conteo")]
    [InlineData(null, "Conteo")]
    public async Task UnAjusteConDatosInvalidosNoCambiaLaExistencia(string? contada, string? motivo)
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        await entorno.DarExistenciaAsync(arroz, 20);

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Carla).AjustarAsync(arroz, Entorno.Numero(contada), motivo);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(20, entorno.LeerProducto(arroz).Existencia);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task AjustarALaMismaExistenciaNoDejaMovimiento()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        await entorno.DarExistenciaAsync(arroz, 20);

        // Actuar
        var resultado = await entorno.Existencias(Entorno.Carla).AjustarAsync(arroz, 20, "Conteo");

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Single(entorno.LeerMovimientos(arroz));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task SinSesionNoSeMueveLaExistencia()
    {
        // Preparar
        var arroz = await entorno.CrearProductoAsync();
        await entorno.DarExistenciaAsync(arroz, 20);

        // Actuar
        var resultado = await entorno.Existencias(usuario: null).RegistrarSalidaAsync(arroz, 4, "Venta");

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, resultado.Fallo);
        Assert.Equal(20, entorno.LeerProducto(arroz).Existencia);
    }
}
