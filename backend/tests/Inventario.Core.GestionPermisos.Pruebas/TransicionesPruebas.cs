using Inventario.Core.ControlAcceso;
using Inventario.Core.GestionPermisos.Solicitudes;
using static Inventario.Core.GestionPermisos.Solicitudes.EstadoSolicitud;

namespace Inventario.Core.GestionPermisos.Pruebas;

/// <summary>
/// RD-04 y RF-GP-03, 04, 06 y 07: la máquina de estados de una solicitud, sin base de datos.
/// Cada prueba arma una solicitud en un estado, intenta una sola transición y revisa cómo quedó.
/// </summary>
public sealed class TransicionesPruebas
{
    private static readonly DateTimeOffset Creada = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Despues = Creada.AddHours(3);
    private static readonly Actor Carla = Actor.Administrador(Entorno.Carla.Id, Entorno.Carla.Nombre);
    private static readonly Actor Dario = Actor.Administrador(Entorno.Dario.Id, Entorno.Dario.Nombre);

    [Fact]
    [Trait("Requisito", "RF-GP-03")]
    public void AprobarUnaPendienteLaDejaAprobadaConQuienAproboYCuando()
    {
        // Preparar
        var solicitud = NuevaSolicitud();

        // Actuar
        var resultado = solicitud.CambiarEstado(Aprobada, new IntentoTransicion(Carla, Despues));

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Aprobada, solicitud.Estado);
        Assert.Equal(Entorno.Carla.Id, solicitud.ResueltaPorId);
        Assert.Equal(Despues, solicitud.ResueltaEn);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-05")]
    public void ElSistemaAplicaUnaAprobadaYRegistraCuando()
    {
        // Preparar
        var solicitud = En(Aprobada);
        var aplicacion = Despues.AddSeconds(1);

        // Actuar
        var resultado = solicitud.CambiarEstado(Aplicada, new IntentoTransicion(Actor.Sistema, aplicacion));

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Aplicada, solicitud.Estado);
        Assert.Equal(aplicacion, solicitud.AplicadaEn);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-04")]
    public void RechazarUnaPendienteConMotivoLaDejaRechazadaConQuienCuandoYElMotivo()
    {
        // Preparar
        var solicitud = NuevaSolicitud();

        // Actuar
        var resultado = solicitud.CambiarEstado(Rechazada, new IntentoTransicion(Carla, Despues, "No lo necesitas para tu puesto."));

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Rechazada, solicitud.Estado);
        Assert.Equal("No lo necesitas para tu puesto.", solicitud.MotivoRechazo);
        Assert.Equal(Entorno.Carla.Id, solicitud.ResueltaPorId);
        Assert.Equal(Despues, solicitud.ResueltaEn);
    }

    [Theory]
    [Trait("Requisito", "RF-GP-04")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RechazarSinMotivoNoSeCompletaYLaSolicitudSiguePendiente(string? motivo)
    {
        // Preparar
        var solicitud = NuevaSolicitud();
        var antes = Foto.De(solicitud);

        // Actuar
        var resultado = solicitud.CambiarEstado(Rechazada, new IntentoTransicion(Carla, Despues, motivo));

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(antes, Foto.De(solicitud));
    }

    [Fact]
    [Trait("Requisito", "RF-GP-04")]
    public void UnMotivoDeMasDe500CaracteresNoSeAcepta()
    {
        // Preparar
        var solicitud = NuevaSolicitud();
        var motivo = new string('x', TransicionesSolicitud.LargoMaximoMotivo + 1);

        // Actuar
        var resultado = solicitud.CambiarEstado(Rechazada, new IntentoTransicion(Carla, Despues, motivo));

        // Verificar
        Assert.Equal(TipoFallo.Validacion, resultado.Fallo);
        Assert.Equal(Pendiente, solicitud.Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void PasarDeRechazadaAAplicadaSeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var solicitud = En(Rechazada);
        var antes = Foto.De(solicitud);

        // Actuar
        var resultado = solicitud.CambiarEstado(Aplicada, new IntentoTransicion(Actor.Sistema, Despues.AddHours(1)));

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(Rechazada, solicitud.Estado);
        Assert.Equal(antes, Foto.De(solicitud));
    }

    /// <summary>
    /// RF-GP-06: las 13 combinaciones que no están en la tabla del enunciado
    /// (Pendiente → Aprobada → Aplicada y Pendiente → Rechazada), cada una intentada por un Administrador y por el sistema.
    /// Están escritas a mano y no se calculan desde TransicionesSolicitud: si alguien agrega una transición indebida
    /// a la tabla, esta prueba lo detecta, la haya declarado para el Administrador o para el sistema.
    /// </summary>
    [Theory]
    [Trait("Requisito", "RF-GP-06")]
    [MemberData(nameof(FueraDeLaTabla))]
    public void UnaTransicionFueraDeLaTablaSeRechazaYLaSolicitudNoCambia(EstadoSolicitud desde, EstadoSolicitud hacia, Ejecutor quien)
    {
        // Preparar
        var solicitud = En(desde);
        var antes = Foto.De(solicitud);
        // Con motivo y, si es un Administrador, uno que no pidió ni resolvió la solicitud: así solo la tabla puede rechazarla.
        var intento = new IntentoTransicion(quien == Ejecutor.Sistema ? Actor.Sistema : Dario, Despues.AddHours(1), "Motivo escrito.");

        // Actuar
        var resultado = solicitud.CambiarEstado(hacia, intento);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(antes, Foto.De(solicitud));
    }

    public static TheoryData<EstadoSolicitud, EstadoSolicitud, Ejecutor> FueraDeLaTabla()
    {
        (EstadoSolicitud Desde, EstadoSolicitud Hacia)[] prohibidas =
        [
            (Pendiente, Pendiente), (Pendiente, Aplicada),
            (Aprobada, Pendiente), (Aprobada, Aprobada), (Aprobada, Rechazada),
            (Aplicada, Pendiente), (Aplicada, Aprobada), (Aplicada, Aplicada), (Aplicada, Rechazada),
            (Rechazada, Pendiente), (Rechazada, Aprobada), (Rechazada, Aplicada), (Rechazada, Rechazada),
        ];
        var datos = new TheoryData<EstadoSolicitud, EstadoSolicitud, Ejecutor>();
        foreach (var (desde, hacia) in prohibidas)
        {
            datos.Add(desde, hacia, Ejecutor.Administrador);
            datos.Add(desde, hacia, Ejecutor.Sistema);
        }
        return datos;
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void LaTablaDeclaraSoloLasTresTransicionesDelEnunciadoConQuienLasEjecuta()
    {
        // Preparar: la tabla del enunciado, escrita a mano.
        var esperadas = new[]
        {
            (Pendiente, Aprobada, Ejecutor.Administrador),
            (Aprobada, Aplicada, Ejecutor.Sistema),
            (Pendiente, Rechazada, Ejecutor.Administrador),
        };

        // Actuar
        var declaradas = TransicionesSolicitud.Permitidas.Select(t => (t.Desde, t.Hacia, t.Quien));

        // Verificar
        Assert.Equal(esperadas.Order(), declaradas.Order());
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void UnAdministradorNoPuedeMarcarAplicadaUnaSolicitudPorqueEsoLoHaceElSistema()
    {
        // Preparar
        var solicitud = En(Aprobada);

        // Actuar
        var resultado = solicitud.CambiarEstado(Aplicada, new IntentoTransicion(Dario, Despues.AddHours(1)));

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(Aprobada, solicitud.Estado);
        Assert.Null(solicitud.AplicadaEn);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-07")]
    public void UnAdministradorNoApruebaSuPropiaSolicitud()
    {
        // Preparar
        var solicitud = SolicitudPermiso.Crear(Entorno.Carla, "ReactivarUsuario", null, Creada);

        // Actuar
        var resultado = solicitud.CambiarEstado(Aprobada, new IntentoTransicion(Carla, Despues));

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(Pendiente, solicitud.Estado);
        Assert.Null(solicitud.ResueltaPorId);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-07")]
    public void UnAdministradorNoRechazaSuPropiaSolicitud()
    {
        // Preparar
        var solicitud = SolicitudPermiso.Crear(Entorno.Carla, "ReactivarUsuario", null, Creada);

        // Actuar
        var resultado = solicitud.CambiarEstado(Rechazada, new IntentoTransicion(Carla, Despues, "Me arrepentí."));

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(Pendiente, solicitud.Estado);
        Assert.Null(solicitud.MotivoRechazo);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void LosUnicosEstadosTerminalesSonAplicadaYRechazada()
    {
        // Preparar: los terminales que fija el enunciado, escritos a mano.
        var esperados = new[] { Aplicada, Rechazada };

        // Actuar
        var terminales = TransicionesSolicitud.Terminales.Order().ToArray();

        // Verificar
        Assert.Equal(esperados, terminales);
    }

    private static SolicitudPermiso NuevaSolicitud() => SolicitudPermiso.Crear(Entorno.Ana, "ListarUsuarios", "Para el conteo.", Creada);

    /// <summary>Lleva una solicitud nueva a <paramref name="estado"/> por el camino permitido.</summary>
    private static SolicitudPermiso En(EstadoSolicitud estado)
    {
        var solicitud = NuevaSolicitud();
        (EstadoSolicitud Hacia, IntentoTransicion Intento)[] pasos = estado switch
        {
            Pendiente => [],
            Aprobada => [(Aprobada, new IntentoTransicion(Carla, Despues))],
            Aplicada => [(Aprobada, new IntentoTransicion(Carla, Despues)), (Aplicada, new IntentoTransicion(Actor.Sistema, Despues))],
            Rechazada => [(Rechazada, new IntentoTransicion(Carla, Despues, "No."))],
            _ => throw new ArgumentOutOfRangeException(nameof(estado)),
        };
        foreach (var (hacia, intento) in pasos)
            Assert.True(solicitud.CambiarEstado(hacia, intento).Exito);
        return solicitud;
    }

    /// <summary>Todo lo que una transición puede tocar. Dos fotos iguales: la solicitud no cambió.</summary>
    private sealed record Foto(EstadoSolicitud Estado, int? ResueltaPorId, DateTimeOffset? ResueltaEn, string? MotivoRechazo, DateTimeOffset? AplicadaEn, int Version)
    {
        public static Foto De(SolicitudPermiso s) => new(s.Estado, s.ResueltaPorId, s.ResueltaEn, s.MotivoRechazo, s.AplicadaEn, s.Version);
    }
}
