namespace Inventario.Integracion.Pruebas;

/// <summary>
/// Criterios N-01, N-03, N-10 y N-11 con las piezas reales: Gestión de permisos avisa por Notificaciones, que pregunta
/// a Control de acceso quiénes son los Administradores activos y encola los correos en la Cola de correos.
/// </summary>
public sealed class NotificacionesIntegracionPruebas
{
    [Fact]
    [Trait("Requisito", "RF-NOT-01")]
    public async Task CrearUnaSolicitudNotificaYEncolaCorreoSoloALosAdministradoresActivos()
    {
        // Preparar
        using var sistema = new Sistema();
        var a1 = await sistema.EntrarAsync("Admin Uno", "a1@ejemplo.com", administrador: true);
        var a2 = await sistema.EntrarAsync("Admin Dos", "a2@ejemplo.com", administrador: true);
        var ax = await sistema.EntrarAsync("Admin Inactivo", "ax@ejemplo.com", administrador: true);
        var e1 = await sistema.EntrarAsync("Estándar Uno", "e1@ejemplo.com", administrador: false);
        var e2 = await sistema.EntrarAsync("Estándar Dos", "e2@ejemplo.com", administrador: false);
        var bandejaAx = await sistema.NotificacionesAsync(ax);
        await sistema.DesactivarAsync("ax@ejemplo.com");

        // Actuar
        var creada = await (await sistema.SolicitudesAsync(e1)).CrearAsync("AjustarExistencia", "Conteo físico");

        // Verificar
        Assert.True(creada.Exito, creada.Mensaje);
        Assert.Equal(1, (await (await sistema.NotificacionesAsync(a1)).ListarMiasAsync("NoLeidas")).Valor!.NoLeidas);
        Assert.Equal(1, (await (await sistema.NotificacionesAsync(a2)).ListarMiasAsync("NoLeidas")).Valor!.NoLeidas);
        Assert.Equal(0, (await bandejaAx.ListarMiasAsync(null)).Valor!.NoLeidas);
        Assert.Equal(0, (await (await sistema.NotificacionesAsync(e1)).ListarMiasAsync(null)).Valor!.NoLeidas);
        Assert.Equal(0, (await (await sistema.NotificacionesAsync(e2)).ListarMiasAsync(null)).Valor!.NoLeidas);
        Assert.Equal(["a1@ejemplo.com", "a2@ejemplo.com"], sistema.LeerCola().Select(c => c.Destinatario));
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-07")]
    public async Task ConElCorreoDesactivadoElSolicitanteRecibeElRechazoSoloDentroDelSistema()
    {
        // Preparar
        using var sistema = new Sistema();
        var a1 = await sistema.EntrarAsync("Admin Uno", "a1@ejemplo.com", administrador: true);
        var e1 = await sistema.EntrarAsync("Estándar Uno", "e1@ejemplo.com", administrador: false);
        await (await sistema.NotificacionesAsync(e1)).CambiarPreferenciasAsync(correo: false);
        var id = (await (await sistema.SolicitudesAsync(e1)).CrearAsync("AjustarExistencia", null)).Valor!.Id;
        var correosAntes = sistema.LeerCola().Count;

        // Actuar
        await (await sistema.SolicitudesAsync(a1)).RechazarAsync(id, "No aplica");

        // Verificar
        var bandeja = (await (await sistema.NotificacionesAsync(e1)).ListarMiasAsync(null)).Valor!;
        Assert.Contains("No aplica", Assert.Single(bandeja.Notificaciones).Mensaje);
        Assert.Equal(correosAntes, sistema.LeerCola().Count);
        Assert.DoesNotContain(sistema.LeerCola(), c => c.Destinatario == "e1@ejemplo.com");
    }
}
