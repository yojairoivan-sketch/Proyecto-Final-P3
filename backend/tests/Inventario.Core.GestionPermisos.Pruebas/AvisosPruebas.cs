using Inventario.Core.ControlAcceso;
using static Inventario.Core.GestionPermisos.Pruebas.Entorno;

namespace Inventario.Core.GestionPermisos.Pruebas;

/// <summary>Criterios N-01 a N-04: a quién avisa Gestión de permisos, y cuándo no avisa.</summary>
public sealed class AvisosPruebas
{
    [Fact]
    [Trait("Requisito", "RF-NOT-01")]
    public async Task CrearUnaSolicitudAvisaSoloALosAdministradoresConElSolicitanteYElPermiso()
    {
        // Preparar
        using var entorno = new Entorno();

        // Actuar
        await entorno.Como(Ana).CrearAsync("ListarUsuarios", "Necesito ver la lista.");

        // Verificar
        var aviso = Assert.Single(entorno.Notificador.Avisos);
        Assert.Null(aviso.UsuarioId);
        Assert.Contains("ListarUsuarios", aviso.Titulo);
        Assert.Contains("Ana", aviso.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-01")]
    public async Task UnaSolicitudQueNoSeCreaNoAvisaANadie()
    {
        // Preparar
        using var entorno = new Entorno();
        await entorno.CrearSolicitudAsync(Ana);
        entorno.Notificador.Avisos.Clear();

        // Actuar
        await entorno.Como(Ana).CrearAsync("ListarUsuarios", justificacion: null);

        // Verificar
        Assert.Empty(entorno.Notificador.Avisos);
    }

    // Hallazgo 2 (docs/bitacora-asignacion-3.md): dos peticiones simultáneas de la misma solicitud pasaban las dos la
    // comprobación de duplicado; quedaban dos solicitudes Pendientes y cada Administrador recibía el aviso dos veces.
    [Fact]
    [Trait("Requisito", "RF-NOT-01")]
    public async Task Hallazgo2_DosSolicitudesSimultaneasDelMismoPermisoCreanUnaSolaYAvisanUnaVez()
    {
        // Preparar: la segunda petición de Ana se guarda justo después de que la primera comprobó que no había otra.
        using var entorno = new Entorno();
        var primera = entorno.ComoConOtraPeticionEnMedio(Ana, () => entorno.CrearSolicitudAsync(Ana));

        // Actuar
        var resultado = await primera.CrearAsync("ListarUsuarios", justificacion: null);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(1, entorno.ContarSolicitudes());
        Assert.Single(entorno.Notificador.Avisos);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-02")]
    public async Task AprobarAvisaAlSolicitanteQueFueAprobada()
    {
        // Preparar
        using var entorno = new Entorno();
        var id = await entorno.CrearSolicitudAsync(Ana);
        entorno.Notificador.Avisos.Clear();

        // Actuar
        await entorno.Como(Carla).AprobarAsync(id);

        // Verificar
        var aviso = Assert.Single(entorno.Notificador.Avisos);
        Assert.Equal(Ana.Id, aviso.UsuarioId);
        Assert.Contains("aprobada", aviso.Titulo);
        Assert.Contains("ListarUsuarios", aviso.Titulo);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-02")]
    public async Task RechazarAvisaAlSolicitanteQueFueRechazadaYElMotivo()
    {
        // Preparar
        using var entorno = new Entorno();
        var id = await entorno.CrearSolicitudAsync(Ana);
        entorno.Notificador.Avisos.Clear();

        // Actuar
        await entorno.Como(Carla).RechazarAsync(id, "No aplica");

        // Verificar
        var aviso = Assert.Single(entorno.Notificador.Avisos);
        Assert.Equal(Ana.Id, aviso.UsuarioId);
        Assert.Contains("rechazada", aviso.Titulo);
        Assert.Contains("No aplica", aviso.Mensaje);
    }

    [Theory]
    [Trait("Requisito", "RF-NOT-02")]
    [InlineData("rechazar sin motivo")]
    [InlineData("aprobar la propia")]
    [InlineData("aprobar una ya rechazada")]
    public async Task UnaResolucionRechazadaNoAvisaANadie(string intento)
    {
        // Preparar
        using var entorno = new Entorno();
        var deAna = await entorno.CrearSolicitudAsync(Ana);
        var deCarla = await entorno.CrearSolicitudAsync(Carla);
        if (intento == "aprobar una ya rechazada")
            await entorno.Como(Dario).RechazarAsync(deAna, "No aplica");
        entorno.Notificador.Avisos.Clear();

        // Actuar
        var resultado = intento switch
        {
            "rechazar sin motivo" => await entorno.Como(Carla).RechazarAsync(deAna, "  "),
            "aprobar la propia" => await entorno.Como(Carla).AprobarAsync(deCarla),
            _ => await entorno.Como(Carla).AprobarAsync(deAna),
        };

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Empty(entorno.Notificador.Avisos);
    }
}
