using Inventario.Core.ControlAcceso;
using Inventario.Negocio;

namespace Inventario.Integracion.Pruebas;

/// <summary>
/// El negocio y Gestión de permisos trabajando juntos: ajustar la existencia (funcionalidad 6) es para Administradores,
/// y un Estándar solo puede hacerlo después de que un Administrador le aprueba el permiso AjustarExistencia.
/// Todo con las piezas reales: la solicitud se crea y se aprueba en Gestión de permisos, el permiso se otorga
/// en Control de acceso, y el negocio le pregunta a Gestión de permisos antes de ajustar.
/// </summary>
public sealed class AjusteConPermisoPruebas : IDisposable
{
    private readonly Sistema sistema = new();

    public void Dispose() => sistema.Dispose();

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task UnEstandarAjustaLaExistenciaSoloDespuesDeQueUnAdministradorApruebaSuSolicitud()
    {
        // Preparar: Carla es Administradora y Ana, Estándar. Ana pide el permiso; su solicitud queda Pendiente.
        var carla = await sistema.EntrarAsync("Carla", "carla@inventario.local", administrador: true);
        var ana = await sistema.EntrarAsync("Ana", "ana@inventario.local", administrador: false);
        var arroz = await sistema.CrearProductoAsync(carla);
        var solicitud = await (await sistema.SolicitudesAsync(ana)).CrearAsync(PermisosNegocio.AjustarExistencia, "Hago el conteo de fin de mes.");
        Assert.True(solicitud.Exito, solicitud.Mensaje);

        // Actuar: Ana intenta ajustar, Carla aprueba la solicitud y Ana lo intenta otra vez.
        var mientrasPendiente = await (await sistema.ExistenciasAsync(ana)).AjustarAsync(arroz, 37, "Conteo de octubre");
        var aprobacion = await (await sistema.SolicitudesAsync(carla)).AprobarAsync(solicitud.Valor!.Id);
        var trasAprobar = await (await sistema.ExistenciasAsync(ana)).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar: rechazado mientras la solicitud no estaba aprobada; permitido después.
        Assert.Equal(TipoFallo.Prohibido, mientrasPendiente.Fallo);
        Assert.True(aprobacion.Exito, aprobacion.Mensaje);
        Assert.True(trasAprobar.Exito, trasAprobar.Mensaje);
        Assert.Equal(37, sistema.LeerProducto(arroz).Existencia);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task UnEstandarQueNoPidioElPermisoNoAjustaLaExistencia()
    {
        // Preparar
        var carla = await sistema.EntrarAsync("Carla", "carla@inventario.local", administrador: true);
        var ana = await sistema.EntrarAsync("Ana", "ana@inventario.local", administrador: false);
        var arroz = await sistema.CrearProductoAsync(carla);

        // Actuar
        var resultado = await (await sistema.ExistenciasAsync(ana)).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(0, sistema.LeerProducto(arroz).Existencia);
        Assert.Empty(sistema.LeerMovimientos(arroz));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task SiUnAdministradorRechazaLaSolicitudElEstandarSigueSinAjustar()
    {
        // Preparar
        var carla = await sistema.EntrarAsync("Carla", "carla@inventario.local", administrador: true);
        var ana = await sistema.EntrarAsync("Ana", "ana@inventario.local", administrador: false);
        var arroz = await sistema.CrearProductoAsync(carla);
        var solicitud = await (await sistema.SolicitudesAsync(ana)).CrearAsync(PermisosNegocio.AjustarExistencia, null);
        var rechazo = await (await sistema.SolicitudesAsync(carla)).RechazarAsync(solicitud.Valor!.Id, "El conteo lo hace el encargado.");
        Assert.True(rechazo.Exito, rechazo.Mensaje);

        // Actuar
        var resultado = await (await sistema.ExistenciasAsync(ana)).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(0, sistema.LeerProducto(arroz).Existencia);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task ElPermisoAprobadoAOtroEstandarNoLeSirveAAna()
    {
        // Preparar: Beto pide el permiso y Carla se lo aprueba a él.
        var carla = await sistema.EntrarAsync("Carla", "carla@inventario.local", administrador: true);
        var ana = await sistema.EntrarAsync("Ana", "ana@inventario.local", administrador: false);
        var beto = await sistema.EntrarAsync("Beto", "beto@inventario.local", administrador: false);
        var arroz = await sistema.CrearProductoAsync(carla);
        var solicitud = await (await sistema.SolicitudesAsync(beto)).CrearAsync(PermisosNegocio.AjustarExistencia, null);
        Assert.True((await (await sistema.SolicitudesAsync(carla)).AprobarAsync(solicitud.Valor!.Id)).Exito);

        // Actuar
        var resultado = await (await sistema.ExistenciasAsync(ana)).AjustarAsync(arroz, 37, "Conteo de octubre");

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(0, sistema.LeerProducto(arroz).Existencia);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task AlAprobarElPermisoTambienQuedaOtorgadoEnControlDeAcceso()
    {
        // Preparar
        var carla = await sistema.EntrarAsync("Carla", "carla@inventario.local", administrador: true);
        var ana = await sistema.EntrarAsync("Ana", "ana@inventario.local", administrador: false);
        var solicitud = await (await sistema.SolicitudesAsync(ana)).CrearAsync(PermisosNegocio.AjustarExistencia, null);

        // Actuar
        await (await sistema.SolicitudesAsync(carla)).AprobarAsync(solicitud.Valor!.Id);

        // Verificar: la misma sesión de Ana, validada otra vez, ya trae el permiso (RF-GP-05).
        var usuario = await sistema.ValidarAsync(ana);
        Assert.Equal([PermisosNegocio.AjustarExistencia], usuario!.Permisos);
    }
}
