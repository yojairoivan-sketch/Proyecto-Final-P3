using Inventario.Core.ControlAcceso;
using static Inventario.Core.Notificaciones.Pruebas.Entorno;

namespace Inventario.Core.Notificaciones.Pruebas;

/// <summary>Criterios N-01 a N-13 de docs/criterios-notificaciones-reportes.md, en la pieza aislada.</summary>
public sealed class NotificacionesPruebas
{
    // ---- Entrega: a quién y por qué canal ----

    [Fact]
    [Trait("Requisito", "RF-NOT-01")]
    public async Task AvisarALosAdministradoresCreaUnaNotificacionSinLeerParaCadaUnoYParaNadieMas()
    {
        // Preparar
        using var entorno = new Entorno();

        // Actuar
        await entorno.Notificador().NotificarAdministradoresAsync("Nueva solicitud", "Ana pidió ListarUsuarios.");

        // Verificar
        Assert.Single(entorno.LeerDe(Carla), n => !n.Leida && n.Titulo == "Nueva solicitud");
        Assert.Single(entorno.LeerDe(Dario), n => !n.Leida);
        Assert.Empty(entorno.LeerDe(Ana));
        Assert.Empty(entorno.LeerDe(Beto));
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-02")]
    public async Task AvisarAUnUsuarioSoloLeDejaLaNotificacionAEl()
    {
        // Preparar
        using var entorno = new Entorno();

        // Actuar
        await entorno.Notificador().NotificarAsync(Ana.Id, "Tu solicitud fue rechazada", "Motivo: No aplica");

        // Verificar
        var notificacion = Assert.Single(entorno.LeerDe(Ana));
        Assert.Contains("No aplica", notificacion.Mensaje);
        Assert.Empty(entorno.LeerDe(Beto));
        Assert.Empty(entorno.LeerDe(Carla));
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-06")]
    public async Task ConElCorreoActivoLaNotificacionTambienSeEncolaAlCorreoDelDestinatarioConElMismoAsunto()
    {
        // Preparar
        using var entorno = new Entorno();

        // Actuar
        await entorno.Notificador().NotificarAsync(Ana.Id, "Tu solicitud fue aprobada", "Ya puedes usar ListarUsuarios.");

        // Verificar
        Assert.Single(entorno.LeerDe(Ana));
        var correo = Assert.Single(entorno.Cola.Encolados);
        Assert.Equal(("ana@ejemplo.com", "Tu solicitud fue aprobada"), (correo.Destinatario, correo.Asunto));
        Assert.Contains("Ya puedes usar ListarUsuarios.", correo.Cuerpo);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-07")]
    public async Task ConElCorreoDesactivadoLaNotificacionQuedaDentroDelSistemaYNoSeEncolaNingunCorreo()
    {
        // Preparar
        using var entorno = new Entorno();
        await entorno.Bandeja(Ana).CambiarPreferenciasAsync(correo: false);

        // Actuar
        await entorno.Notificador().NotificarAsync(Ana.Id, "Aviso", "Mensaje");

        // Verificar
        Assert.Single(entorno.LeerDe(Ana));
        Assert.Empty(entorno.Cola.Encolados);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-07")]
    public async Task LaPreferenciaDeCorreoEsDeCadaUsuario()
    {
        // Preparar
        using var entorno = new Entorno();
        await entorno.Bandeja(Carla).CambiarPreferenciasAsync(correo: false);

        // Actuar
        await entorno.Notificador().NotificarAdministradoresAsync("Nueva solicitud", "Mensaje");

        // Verificar
        Assert.Single(entorno.LeerDe(Carla));
        var correo = Assert.Single(entorno.Cola.Encolados);
        Assert.Equal("dario@ejemplo.com", correo.Destinatario);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-07")]
    public async Task VolverAActivarElCorreoHaceQueSeEncoleOtraVez()
    {
        // Preparar
        using var entorno = new Entorno();
        await entorno.Bandeja(Ana).CambiarPreferenciasAsync(correo: false);
        await entorno.Bandeja(Ana).CambiarPreferenciasAsync(correo: true);

        // Actuar
        await entorno.Notificador().NotificarAsync(Ana.Id, "Aviso", "Mensaje");

        // Verificar
        Assert.Single(entorno.Cola.Encolados);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-07")]
    public async Task LasPreferenciasDicenSiElCorreoEstaActivoYPorDefectoLoEsta()
    {
        // Preparar
        using var entorno = new Entorno();
        await entorno.Bandeja(Beto).CambiarPreferenciasAsync(correo: false);

        // Actuar
        var deAna = await entorno.Bandeja(Ana).ConsultarPreferenciasAsync();
        var deBeto = await entorno.Bandeja(Beto).ConsultarPreferenciasAsync();

        // Verificar
        Assert.Equal((true, false), (deAna.Valor!.Correo, deBeto.Valor!.Correo));
    }

    // Hallazgo 4 (docs/bitacora-asignacion-3.md): dos PUT /api/notificaciones/preferencias a la vez de un usuario que aún
    // no tenía fila intentaban crearla los dos; el segundo chocaba con la clave y respondía 500.
    [Fact]
    [Trait("Requisito", "RF-NOT-07")]
    public async Task Hallazgo4_DosCambiosSimultaneosDePreferenciasNoFallanYQuedaElUltimo()
    {
        // Preparar: otra petición de Ana guarda {"correo": true} justo antes de que esta guarde.
        using var entorno = new Entorno();
        var bandeja = entorno.BandejaConOtraPeticionEnMedio(Ana,
            async () => await entorno.Bandeja(Ana).CambiarPreferenciasAsync(correo: true));

        // Actuar
        var resultado = await bandeja.CambiarPreferenciasAsync(correo: false);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        var guardada = await entorno.Bandeja(Ana).ConsultarPreferenciasAsync();
        Assert.False(guardada.Valor!.Correo);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-07")]
    public async Task CambiarLasPreferenciasSinDecirSiElCorreoVaSeRechaza()
    {
        // Preparar
        using var entorno = new Entorno();

        // Actuar
        var resultado = await entorno.Bandeja(Ana).CambiarPreferenciasAsync(correo: null);

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
    }

    // ---- Consultar y marcar: solo las propias ----

    [Fact]
    [Trait("Requisito", "RF-NOT-04")]
    public async Task ConsultarDevuelveSoloLasNotificacionesPropiasConSiEstanLeidas()
    {
        // Preparar
        using var entorno = new Entorno();
        var primera = await entorno.NotificarAsync(Ana, "Primera");
        await entorno.NotificarAsync(Ana, "Segunda");
        await entorno.NotificarAsync(Beto, "De Beto");
        await entorno.Bandeja(Ana).MarcarLeidaAsync(primera);

        // Actuar
        var bandeja = await entorno.Bandeja(Ana).ListarMiasAsync(estado: null);

        // Verificar
        Assert.True(bandeja.Exito, bandeja.Mensaje);
        Assert.Equal(1, bandeja.Valor!.NoLeidas);
        Assert.Equal([("Segunda", false), ("Primera", true)], bandeja.Valor.Notificaciones.Select(n => (n.Titulo, n.Leida)));
    }

    [Theory]
    [Trait("Requisito", "RF-NOT-04")]
    [InlineData("NoLeidas", "Segunda")]
    [InlineData("leidas", "Primera")]
    public async Task ElFiltroPorEstadoDevuelveSoloLasLeidasOSoloLasNoLeidas(string estado, string esperada)
    {
        // Preparar
        using var entorno = new Entorno();
        var primera = await entorno.NotificarAsync(Ana, "Primera");
        await entorno.NotificarAsync(Ana, "Segunda");
        await entorno.Bandeja(Ana).MarcarLeidaAsync(primera);

        // Actuar
        var bandeja = await entorno.Bandeja(Ana).ListarMiasAsync(estado);

        // Verificar
        Assert.Equal(esperada, Assert.Single(bandeja.Valor!.Notificaciones).Titulo);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-04")]
    public async Task UnEstadoQueNoExisteSeRechazaConMensaje()
    {
        // Preparar
        using var entorno = new Entorno();

        // Actuar
        var bandeja = await entorno.Bandeja(Ana).ListarMiasAsync("archivadas");

        // Verificar
        Assert.Equal(TipoFallo.Validacion, bandeja.Fallo);
        Assert.Contains("NoLeidas", bandeja.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-04")]
    public async Task UnAdministradorTampocoVeLasNotificacionesDeOtros()
    {
        // Preparar
        using var entorno = new Entorno();
        await entorno.NotificarAsync(Ana);

        // Actuar
        var bandeja = await entorno.Bandeja(Carla).ListarMiasAsync(estado: null);

        // Verificar
        Assert.Empty(bandeja.Valor!.Notificaciones);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-05")]
    public async Task MarcarComoLeidaUnaPropiaGuardaLaFechaYRepetirloNoLaCambia()
    {
        // Preparar
        using var entorno = new Entorno();
        var id = await entorno.NotificarAsync(Ana);
        await entorno.Bandeja(Ana).MarcarLeidaAsync(id);
        var primeraLectura = entorno.Leer(id).LeidaEn;
        entorno.Reloj.Advance(TimeSpan.FromMinutes(5));

        // Actuar
        var otraVez = await entorno.Bandeja(Ana).MarcarLeidaAsync(id);

        // Verificar
        Assert.True(otraVez.Exito, otraVez.Mensaje);
        Assert.NotNull(primeraLectura);
        Assert.Equal(primeraLectura, entorno.Leer(id).LeidaEn);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-05")]
    public async Task MarcarLaNotificacionDeOtroSeRechazaComoSiNoExistieraYLaDelOtroSigueSinLeer()
    {
        // Preparar
        using var entorno = new Entorno();
        var deBeto = await entorno.NotificarAsync(Beto);

        // Actuar
        var resultado = await entorno.Bandeja(Ana).MarcarLeidaAsync(deBeto);

        // Verificar
        Assert.Equal(TipoFallo.NoEncontrado, resultado.Fallo);
        Assert.False(entorno.Leer(deBeto).Leida);
    }

    [Fact]
    [Trait("Requisito", "RF-NOT-05")]
    public async Task UnAdministradorTampocoPuedeMarcarLaNotificacionDeOtro()
    {
        // Preparar
        using var entorno = new Entorno();
        var deAna = await entorno.NotificarAsync(Ana);

        // Actuar
        var resultado = await entorno.Bandeja(Carla).MarcarLeidaAsync(deAna);

        // Verificar
        Assert.Equal(TipoFallo.NoEncontrado, resultado.Fallo);
        Assert.False(entorno.Leer(deAna).Leida);
    }

    [Theory]
    [Trait("Requisito", "RD-06")]
    [InlineData("Listar")]
    [InlineData("Marcar")]
    [InlineData("ConsultarPreferencias")]
    [InlineData("CambiarPreferencias")]
    public async Task SinSesionNingunaOperacionDeLaBandejaSeEjecuta(string operacion)
    {
        // Preparar
        using var entorno = new Entorno();
        var id = await entorno.NotificarAsync(Ana);
        var sinSesion = entorno.Bandeja(null);

        // Actuar
        Resultado resultado = operacion switch
        {
            "Listar" => await sinSesion.ListarMiasAsync(null),
            "Marcar" => await sinSesion.MarcarLeidaAsync(id),
            "ConsultarPreferencias" => await sinSesion.ConsultarPreferenciasAsync(),
            _ => await sinSesion.CambiarPreferenciasAsync(false),
        };

        // Verificar
        Assert.Equal(TipoFallo.NoAutenticado, resultado.Fallo);
        Assert.False(entorno.Leer(id).Leida);
    }
}
