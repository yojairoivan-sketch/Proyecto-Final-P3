using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Existencias;
using Inventario.Negocio.Ordenes;
using Inventario.Negocio.Productos;
using Inventario.Negocio.Proveedores;
using static Inventario.Negocio.Ordenes.EstadoOrdenCompra;

namespace Inventario.Negocio.Pruebas;

/// <summary>
/// La máquina de estados de la orden de compra (RF-NEG-03, 04 y 05, RD-04), en memoria y sin base de datos.
/// Hay al menos una prueba por cada fila de las tablas de docs/maquina-de-estados.md: cada una arma una orden en un estado,
/// intenta una sola transición y revisa cómo quedó. Las tablas esperadas están escritas a mano, no se leen del código.
/// </summary>
public sealed class TransicionesOrdenCompraPruebas
{
    private static readonly DateTimeOffset Creada = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Despues = Creada.AddHours(3);
    private static readonly Responsable Carla = new(1, "Carla", Rol.Administrador);
    private static readonly Responsable Ana = new(10, "Ana", Rol.Estandar);

    // ---- Permitida 1: Borrador → Enviada, solo Administrador, con al menos una línea ----

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void UnAdministradorEnviaUnaOrdenConLineasYQuedaEnviada()
    {
        // Preparar
        var orden = NuevaOrden();

        // Actuar
        var resultado = orden.CambiarEstado(Enviada, Carla, Despues);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Enviada, orden.Estado);
        Assert.Equal(Despues, orden.ActualizadaEn);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void EnviarUnaOrdenSinLineasSeRechazaYSigueEnBorrador()
    {
        // Preparar
        var orden = NuevaOrden(conLinea: false);
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(Enviada, Carla, Despues);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(antes, Foto.De(orden));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void UnEstandarNoPuedeEnviarUnaOrdenYSigueEnBorrador()
    {
        // Preparar
        var orden = NuevaOrden();
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(Enviada, Ana, Despues);

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(antes, Foto.De(orden));
    }

    // ---- Permitida 2: Borrador → Cancelada, Administrador o Estándar ----

    [Theory]
    [Trait("Requisito", "RF-NEG-03")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.Estandar)]
    public void CancelarUnaOrdenEnBorradorLaDejaCancelada(string rol)
    {
        // Preparar
        var orden = NuevaOrden();

        // Actuar
        var resultado = orden.CambiarEstado(Cancelada, Con(rol), Despues);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Cancelada, orden.Estado);
    }

    // ---- Permitida 3: Enviada → Recibida, Administrador o Estándar; suma la existencia ----

    [Theory]
    [Trait("Requisito", "RF-NEG-03")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.Estandar)]
    public void RecibirUnaOrdenEnviadaLaDejaRecibida(string rol)
    {
        // Preparar
        var orden = En(Enviada);

        // Actuar
        var resultado = orden.CambiarEstado(Recibida, Con(rol), Despues);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Recibida, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void RecibirUnaOrdenSumaLaCantidadDeCadaLineaALaExistenciaDelProducto()
    {
        // Preparar: 10 sacos de arroz y 24 botellas de aceite.
        var orden = NuevaOrden();
        var aceite = Producto.Crear("Aceite", "ACE-1", "botella", 12, Creada);
        Assert.True(orden.AgregarLinea(aceite, 24, 210, Creada).Exito);
        Assert.True(orden.CambiarEstado(Enviada, Carla, Creada.AddHours(1)).Exito);

        // Actuar
        orden.CambiarEstado(Recibida, Ana, Despues);

        // Verificar
        Assert.Equal(10, orden.Lineas[0].Producto.Existencia);
        Assert.Equal(24, aceite.Existencia);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void RecibirUnaOrdenDejaUnMovimientoDeEntradaConQuienYCuando()
    {
        // Preparar
        var orden = En(Enviada);
        var arroz = orden.Lineas[0].Producto;

        // Actuar
        orden.CambiarEstado(Recibida, Ana, Despues);

        // Verificar
        var movimiento = Assert.Single(arroz.Movimientos);
        Assert.Equal(TipoMovimiento.Entrada, movimiento.Tipo);
        Assert.Equal(10, movimiento.Cantidad);
        Assert.Equal(10, movimiento.ExistenciaResultante);
        Assert.Same(orden, movimiento.OrdenDeCompra);
        Assert.Equal(Ana.UsuarioId, movimiento.UsuarioId);
        Assert.Equal(Despues, movimiento.RegistradoEn);
    }

    // ---- Permitida 4: Enviada → Cancelada, solo Administrador ----

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void UnAdministradorCancelaUnaOrdenEnviadaYQuedaCancelada()
    {
        // Preparar
        var orden = En(Enviada);

        // Actuar
        var resultado = orden.CambiarEstado(Cancelada, Carla, Despues);

        // Verificar
        Assert.True(resultado.Exito, resultado.Mensaje);
        Assert.Equal(Cancelada, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void UnEstandarNoPuedeCancelarUnaOrdenEnviadaYSigueEnviada()
    {
        // Preparar
        var orden = En(Enviada);
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(Cancelada, Ana, Despues);

        // Verificar
        Assert.Equal(TipoFallo.Prohibido, resultado.Fallo);
        Assert.Equal(antes, Foto.De(orden));
    }

    // ---- Prohibida 1: Enviada → Borrador ----

    [Fact]
    [Trait("Requisito", "RF-NEG-04")]
    public void PasarUnaOrdenEnviadaABorradorSeRechazaYSigueEnviada()
    {
        // Preparar
        var orden = En(Enviada);
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(Borrador, Carla, Despues);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Contains("no vuelve a Borrador", resultado.Mensaje);
        Assert.Equal(antes, Foto.De(orden));
    }

    // ---- Prohibida 2: Recibida → Cancelada ----

    [Fact]
    [Trait("Requisito", "RF-NEG-04")]
    public void CancelarUnaOrdenRecibidaSeRechazaYNoTocaLaExistencia()
    {
        // Preparar
        var orden = En(Recibida);
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(Cancelada, Carla, Despues);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Contains("ya entró al inventario", resultado.Mensaje);
        Assert.Equal(antes, Foto.De(orden));
    }

    // ---- Prohibida 3: Borrador → Recibida ----

    [Fact]
    [Trait("Requisito", "RF-NEG-04")]
    public void RecibirUnaOrdenEnBorradorSinEnviarlaSeRechazaYNoSumaExistencia()
    {
        // Preparar
        var orden = NuevaOrden();
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(Recibida, Ana, Despues);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Contains("no se envió", resultado.Mensaje);
        Assert.Equal(antes, Foto.De(orden));
    }

    // ---- Prohibida 4: pasar al estado en que ya está ----

    [Theory]
    [Trait("Requisito", "RF-NEG-04")]
    [InlineData(Borrador)]
    [InlineData(Enviada)]
    public void PasarUnaOrdenAlEstadoEnQueYaEstaSeRechazaYNoCambia(EstadoOrdenCompra estado)
    {
        // Preparar
        var orden = En(estado);
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(estado, Carla, Despues);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(antes, Foto.De(orden));
    }

    // ---- Prohibidas 5 y 6: de un estado terminal no se sale ----

    /// <summary>
    /// RF-NEG-05: desde Recibida y desde Cancelada se intenta ir a cada uno de los cuatro estados, con los dos roles.
    /// Ninguno pasa, y la orden y la existencia quedan como estaban.
    /// </summary>
    [Theory]
    [Trait("Requisito", "RF-NEG-05")]
    [MemberData(nameof(DesdeLosTerminales))]
    public void DeUnEstadoTerminalNoSeSaleYLaOrdenNoCambia(EstadoOrdenCompra terminal, EstadoOrdenCompra hacia, string rol)
    {
        // Preparar
        var orden = En(terminal);
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(hacia, Con(rol), Despues);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(antes, Foto.De(orden));
    }

    public static TheoryData<EstadoOrdenCompra, EstadoOrdenCompra, string> DesdeLosTerminales()
    {
        var datos = new TheoryData<EstadoOrdenCompra, EstadoOrdenCompra, string>();
        foreach (var terminal in new[] { Recibida, Cancelada })
            foreach (var hacia in new[] { Borrador, Enviada, Recibida, Cancelada })
                foreach (var rol in new[] { Rol.Administrador, Rol.Estandar })
                    datos.Add(terminal, hacia, rol);
        return datos;
    }

    // ---- Todas las prohibidas juntas ----

    /// <summary>
    /// RF-NEG-04: las 12 combinaciones que no están en la tabla de permitidas, escritas a mano, cada una intentada
    /// por un Administrador y por un Estándar sobre una orden con líneas, para que solo la tabla pueda rechazarla.
    /// Si alguien agrega una transición indebida, esta prueba lo detecta sin importar para qué rol la declaró.
    /// </summary>
    [Theory]
    [Trait("Requisito", "RF-NEG-04")]
    [MemberData(nameof(Prohibidas))]
    public void UnaTransicionProhibidaSeRechazaYLaOrdenNoCambia(EstadoOrdenCompra desde, EstadoOrdenCompra hacia, string rol)
    {
        // Preparar
        var orden = En(desde);
        var antes = Foto.De(orden);

        // Actuar
        var resultado = orden.CambiarEstado(hacia, Con(rol), Despues);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Equal(antes, Foto.De(orden));
    }

    public static TheoryData<EstadoOrdenCompra, EstadoOrdenCompra, string> Prohibidas()
    {
        (EstadoOrdenCompra Desde, EstadoOrdenCompra Hacia)[] prohibidas =
        [
            (Borrador, Borrador), (Borrador, Recibida),
            (Enviada, Borrador), (Enviada, Enviada),
            (Recibida, Borrador), (Recibida, Enviada), (Recibida, Recibida), (Recibida, Cancelada),
            (Cancelada, Borrador), (Cancelada, Enviada), (Cancelada, Recibida), (Cancelada, Cancelada),
        ];
        var datos = new TheoryData<EstadoOrdenCompra, EstadoOrdenCompra, string>();
        foreach (var (desde, hacia) in prohibidas)
        {
            datos.Add(desde, hacia, Rol.Administrador);
            datos.Add(desde, hacia, Rol.Estandar);
        }
        return datos;
    }

    // ---- La tabla ----

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void LaOrdenTieneLosCuatroEstadosDelDocumento()
    {
        // Preparar
        var esperados = new[] { Borrador, Enviada, Recibida, Cancelada };

        // Actuar
        var declarados = Enum.GetValues<EstadoOrdenCompra>();

        // Verificar
        Assert.Equal(esperados, declarados);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void UnaOrdenNuevaNaceEnBorrador()
    {
        // Preparar
        var proveedor = Proveedor.Crear("Molinos del Norte", null, null, Creada);

        // Actuar
        var orden = OrdenDeCompra.Crear(proveedor, Creada);

        // Verificar
        Assert.Equal(Borrador, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void LaTablaDeclaraSoloLasCuatroTransicionesPermitidasConQuienLasEjecuta()
    {
        // Preparar: la tabla de permitidas del documento, escrita a mano.
        var esperadas = new[]
        {
            (Borrador, Enviada, "Administrador"),
            (Borrador, Cancelada, "Administrador o Estándar"),
            (Enviada, Recibida, "Administrador o Estándar"),
            (Enviada, Cancelada, "Administrador"),
        };

        // Actuar
        var declaradas = TransicionesOrdenCompra.Permitidas.Select(t => (t.Desde, t.Hacia, string.Join(" o ", t.Quien)));

        // Verificar
        Assert.Equal(esperadas.Order(), declaradas.Order());
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-04")]
    public void LasProhibidasExplicitasSonLasTresDelDocumento()
    {
        // Preparar
        var esperadas = new[] { (Enviada, Borrador), (Recibida, Cancelada), (Borrador, Recibida) };

        // Actuar
        var declaradas = TransicionesOrdenCompra.Prohibidas.Select(t => (t.Desde, t.Hacia));

        // Verificar
        Assert.Equal(esperadas, declaradas);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-04")]
    public void NingunaProhibidaExplicitaEstaEntreLasPermitidas()
    {
        // Preparar
        var prohibidas = TransicionesOrdenCompra.Prohibidas;

        // Actuar
        var permitidas = prohibidas.Where(p => TransicionesOrdenCompra.Buscar(p.Desde, p.Hacia) is not null).ToList();

        // Verificar
        Assert.Empty(permitidas);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-05")]
    public void LosEstadosTerminalesSonRecibidaYCancelada()
    {
        // Preparar
        var esperados = new[] { Recibida, Cancelada };

        // Actuar
        var terminales = TransicionesOrdenCompra.Terminales.Order().ToArray();

        // Verificar
        Assert.Equal(esperados, terminales);
    }

    [Theory]
    [Trait("Requisito", "RD-04")]
    [InlineData(Enviada)]
    [InlineData(Recibida)]
    [InlineData(Cancelada)]
    public void SoloUnaOrdenEnBorradorAdmiteLineasNuevas(EstadoOrdenCompra estado)
    {
        // Preparar
        var orden = En(estado);
        var aceite = Producto.Crear("Aceite", "ACE-1", "botella", 12, Creada);

        // Actuar
        var resultado = orden.AgregarLinea(aceite, 24, 210, Despues);

        // Verificar
        Assert.Equal(TipoFallo.Conflicto, resultado.Fallo);
        Assert.Single(orden.Lineas);
    }

    private static Responsable Con(string rol) => rol == Rol.Administrador ? Carla : Ana;

    /// <summary>Una orden nueva, en Borrador, con una línea de 10 sacos de arroz (o sin líneas).</summary>
    private static OrdenDeCompra NuevaOrden(bool conLinea = true)
    {
        var orden = OrdenDeCompra.Crear(Proveedor.Crear("Molinos del Norte", null, null, Creada), Creada);
        if (conLinea)
            Assert.True(orden.AgregarLinea(Producto.Crear("Arroz", "ARR-1", "saco", 5, Creada), 10, 850, Creada).Exito);
        return orden;
    }

    /// <summary>Lleva una orden nueva con una línea a <paramref name="estado"/> por el camino permitido.</summary>
    private static OrdenDeCompra En(EstadoOrdenCompra estado)
    {
        var orden = NuevaOrden();
        EstadoOrdenCompra[] pasos = estado switch
        {
            Borrador => [],
            Enviada => [Enviada],
            Recibida => [Enviada, Recibida],
            Cancelada => [Cancelada],
            _ => throw new ArgumentOutOfRangeException(nameof(estado)),
        };
        foreach (var hacia in pasos)
            Assert.True(orden.CambiarEstado(hacia, Carla, Creada.AddHours(1)).Exito);
        return orden;
    }

    /// <summary>Todo lo que una transición puede tocar. Dos fotos iguales: la orden y la existencia no cambiaron.</summary>
    private sealed record Foto(EstadoOrdenCompra Estado, DateTimeOffset ActualizadaEn, int Version, decimal Existencia, int Movimientos)
    {
        public static Foto De(OrdenDeCompra o) => new(
            o.Estado, o.ActualizadaEn, o.Version,
            o.Lineas.Sum(l => l.Producto.Existencia), o.Lineas.Sum(l => l.Producto.Movimientos.Count));
    }
}
