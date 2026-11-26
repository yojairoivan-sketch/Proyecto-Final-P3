using Inventario.Core.Auditoria;
using Inventario.Core.GestionPermisos.Solicitudes;

namespace Inventario.Core.GestionPermisos.Pruebas;

/// <summary>
/// RF-AUD-02: toda resolución de una solicitud de permiso queda en el historial: quién la aprobó o rechazó, qué solicitud
/// (y qué permiso), cuándo, y el estado anterior y el nuevo (RF-AUD-01). El registro se lee de la misma base.
/// </summary>
public sealed class AuditoriaSolicitudesPruebas : IDisposable
{
    private readonly Entorno entorno = new();

    public void Dispose() => entorno.Dispose();

    [Fact]
    [Trait("Requisito", "RF-AUD-02")]
    public async Task AprobarDejaQuienAproboQueSolicitudYPermisoYCuando()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana, "ListarUsuarios");
        entorno.Reloj.Advance(TimeSpan.FromMinutes(30));
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).AprobarAsync(id);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = Assert.Single(entorno.Historial());
        DeCarla(registro);
        Assert.Equal("AprobarSolicitud", registro.Accion);
        Assert.Equal("SolicitudPermiso", registro.Entidad);
        Assert.Equal(id.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal("""{"estado":"Pendiente","permiso":"ListarUsuarios","solicitante":"ana@ejemplo.com"}""", registro.ValorAnterior);
        Assert.Equal("""{"estado":"Aprobada","permiso":"ListarUsuarios"}""", registro.ValorNuevo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-02")]
    public async Task RechazarDejaQuienRechazoQueSolicitudCuandoYElMotivo()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana, "ReactivarUsuario");
        entorno.Reloj.Advance(TimeSpan.FromHours(1));
        var ahora = entorno.Reloj.GetUtcNow();

        // Actuar
        var resultado = await entorno.Como(Entorno.Carla).RechazarAsync(id, "Pídeselo a tu supervisor primero.");

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var registro = Assert.Single(entorno.Historial());
        DeCarla(registro);
        Assert.Equal("RechazarSolicitud", registro.Accion);
        Assert.Equal("SolicitudPermiso", registro.Entidad);
        Assert.Equal(id.ToString(), registro.EntidadId);
        Assert.Equal(ahora, registro.FechaHora);
        Assert.Equal("""{"estado":"Pendiente","permiso":"ReactivarUsuario","solicitante":"ana@ejemplo.com"}""", registro.ValorAnterior);
        Assert.Equal("""{"estado":"Rechazada","permiso":"ReactivarUsuario","motivo":"Pídeselo a tu supervisor primero."}""", registro.ValorNuevo);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-02")]
    public async Task LaFechaDelRegistroEsLaMismaQueLaDeLaResolucion()
    {
        // Preparar
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);
        entorno.Reloj.Advance(TimeSpan.FromMinutes(7));

        // Actuar
        await entorno.Como(Entorno.Carla).AprobarAsync(id);

        // Verificar (RD-11): el historial y la solicitud dicen la misma hora.
        Assert.Equal(entorno.Leer(id).ResueltaEn, Assert.Single(entorno.Historial()).FechaHora);
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-02")]
    public async Task UnaResolucionRechazadaNoDejaRegistro()
    {
        // Preparar: Carla pidió un permiso; resolver la propia está prohibido (RF-GP-07), y rechazar sin motivo también.
        var propia = await entorno.CrearSolicitudAsync(Entorno.Carla);
        var deAna = await entorno.CrearSolicitudAsync(Entorno.Ana);

        // Actuar
        var aprobarPropia = await entorno.Como(Entorno.Carla).AprobarAsync(propia);
        var sinMotivo = await entorno.Como(Entorno.Carla).RechazarAsync(deAna, "  ");
        var inexistente = await entorno.Como(Entorno.Carla).AprobarAsync(9999);

        // Verificar
        Assert.False(aprobarPropia.Exito);
        Assert.False(sinMotivo.Exito);
        Assert.False(inexistente.Exito);
        Assert.Empty(entorno.Historial());
    }

    [Fact]
    [Trait("Requisito", "RF-AUD-02")]
    public async Task SiOtroAdministradorLaResolvioAlMismoTiempoNoQuedaNiLaResolucionNiSuRegistro()
    {
        // Preparar: Darío la aprueba justo antes de que se guarde el rechazo de Carla.
        var id = await entorno.CrearSolicitudAsync(Entorno.Ana);
        var servicioDeCarla = entorno.ComoConOtraPeticionEnMedio(Entorno.Carla,
            async () => Assert.True((await entorno.Como(Entorno.Dario).AprobarAsync(id)).Exito));

        // Actuar
        var rechazo = await servicioDeCarla.RechazarAsync(id, "No corresponde.");

        // Verificar: solo queda lo que hizo Darío.
        Assert.False(rechazo.Exito);
        var registro = Assert.Single(entorno.Historial());
        Assert.Equal(Entorno.Dario.Id, registro.UsuarioId);
        Assert.Equal("AprobarSolicitud", registro.Accion);
        Assert.Equal(EstadoSolicitud.Aplicada, entorno.Leer(id).Estado);
    }

    private static void DeCarla(RegistroAuditoria registro)
    {
        Assert.Equal(Entorno.Carla.Id, registro.UsuarioId);
        Assert.Equal("carla@ejemplo.com", registro.UsuarioCorreo);
        Assert.Equal("Administrador", registro.UsuarioRol);
    }
}
