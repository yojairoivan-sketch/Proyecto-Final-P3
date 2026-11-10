using Inventario.Core.ControlAcceso;
using Inventario.Core.GestionPermisos;
using Inventario.Negocio.Cotizaciones;
using Inventario.Negocio.Existencias;
using Inventario.Negocio.Ordenes;
using Inventario.Negocio.Productos;
using Inventario.Negocio.Proveedores;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Time.Testing;

namespace Inventario.Negocio.Pruebas;

/// <summary>
/// El módulo de negocio aislado (RD-12): base SQLite en memoria, reloj controlado y dobles de las interfaces del Core
/// que usa (IUsuarioActual de Control de acceso e IPermisosConcedidos de Gestión de permisos).
/// Cada prueba crea su propio entorno, así que no comparten datos y pasan en cualquier orden.
/// </summary>
public sealed class Entorno : IDisposable
{
    public static readonly UsuarioAutenticado Ana = new(10, "Ana", "ana@ejemplo.com", Rol.Estandar, SesionId: 1);
    public static readonly UsuarioAutenticado Beto = new(11, "Beto", "beto@ejemplo.com", Rol.Estandar, SesionId: 2);
    public static readonly UsuarioAutenticado Carla = new(1, "Carla", "carla@ejemplo.com", Rol.Administrador, SesionId: 3);

    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly List<InventarioDbContext> contextos = [];

    public Entorno()
    {
        conexion.Open();
        NuevoContexto().Database.EnsureCreated();
    }

    /// <summary>Un número escrito en texto (los atributos de xUnit no admiten decimal).</summary>
    public static decimal? Numero(string? texto) => texto is null ? null : decimal.Parse(texto, CultureInfo.InvariantCulture);

    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
    public PermisosConcedidosFalso Permisos { get; } = new();

    // Cada servicio con un contexto nuevo, como cada petición en la API, y con <usuario> como usuario de la sesión.
    public IServicioProveedores Proveedores(UsuarioAutenticado? usuario) =>
        new ServicioProveedores(NuevoContexto(), new UsuarioActualFalso(usuario), Reloj);

    public IServicioProductos Productos(UsuarioAutenticado? usuario) =>
        new ServicioProductos(NuevoContexto(), new UsuarioActualFalso(usuario), Reloj);

    public IServicioOrdenes Ordenes(UsuarioAutenticado? usuario) =>
        new ServicioOrdenes(NuevoContexto(), new UsuarioActualFalso(usuario), Reloj);

    public IServicioExistencias Existencias(UsuarioAutenticado? usuario) =>
        new ServicioExistencias(NuevoContexto(), new UsuarioActualFalso(usuario), Permisos, Reloj);

    /// <summary>La API externa de tasas de cambio reemplazada por un doble: cada prueba decide qué responde.</summary>
    public TasasFalsas Tasas { get; } = new();

    public IServicioCotizaciones Cotizaciones(UsuarioAutenticado? usuario) =>
        new ServicioCotizaciones(NuevoContexto(), new UsuarioActualFalso(usuario), Tasas);

    // ---- Preparación: datos que nacen bien, para las pruebas que parten de ellos ----

    public async Task<int> CrearProveedorAsync(string nombre = "Molinos del Norte")
    {
        var creado = await Proveedores(Carla).RegistrarAsync(nombre, "809-555-0101", "ventas@molinos.com");
        Assert.True(creado.Exito, creado.Mensaje);
        return creado.Valor!.Id;
    }

    public async Task<int> CrearProductoAsync(string sku = "ARR-1", decimal puntoReorden = 5, string nombre = "Arroz")
    {
        var creado = await Productos(Carla).RegistrarAsync(nombre, sku, "saco", puntoReorden);
        Assert.True(creado.Exito, creado.Mensaje);
        return creado.Valor!.Id;
    }

    /// <summary>Una orden en Borrador con una línea del producto.</summary>
    public async Task<int> CrearOrdenAsync(int productoId, decimal cantidad = 10, decimal costo = 850)
    {
        var orden = await Ordenes(Carla).CrearAsync(await CrearProveedorAsync());
        Assert.True(orden.Exito, orden.Mensaje);
        var linea = await Ordenes(Carla).AgregarLineaAsync(orden.Valor!.Id, productoId, cantidad, costo);
        Assert.True(linea.Exito, linea.Mensaje);
        return orden.Valor.Id;
    }

    /// <summary>Lleva la orden por los estados indicados, como Administrador.</summary>
    public async Task LlevarAsync(int ordenId, params EstadoOrdenCompra[] pasos)
    {
        foreach (var paso in pasos)
        {
            var cambio = await Ordenes(Carla).CambiarEstadoAsync(ordenId, paso.ToString());
            Assert.True(cambio.Exito, cambio.Mensaje);
        }
    }

    /// <summary>Deja la existencia del producto en <paramref name="existencia"/> con un ajuste del Administrador.</summary>
    public async Task DarExistenciaAsync(int productoId, decimal existencia)
    {
        var ajuste = await Existencias(Carla).AjustarAsync(productoId, existencia, "Inventario inicial");
        Assert.True(ajuste.Exito, ajuste.Mensaje);
    }

    // ---- Lectura: lo que quedó guardado, con un contexto nuevo, no lo que el servicio tenía en memoria ----

    internal OrdenDeCompra LeerOrden(int id) => NuevoContexto().OrdenesDeCompra.AsNoTracking()
        .Include(o => o.Lineas).ThenInclude(l => l.Producto)
        .Single(o => o.Id == id);

    internal Producto LeerProducto(int id) => NuevoContexto().Productos.AsNoTracking().Single(p => p.Id == id);

    internal List<MovimientoExistencia> LeerMovimientos(int productoId) =>
        NuevoContexto().Movimientos.AsNoTracking().Where(m => m.ProductoId == productoId).OrderBy(m => m.Id).ToList();

    internal int Contar<T>() where T : class => NuevoContexto().Set<T>().Count();

    private InventarioDbContext NuevoContexto()
    {
        var contexto = new InventarioDbContext(new DbContextOptionsBuilder<InventarioDbContext>()
            .UseSqlite(conexion)
            .ReplaceService<IModelCustomizer, FechasComoNumero>()
            .Options);
        contextos.Add(contexto);
        return contexto;
    }

    public void Dispose()
    {
        foreach (var contexto in contextos)
            contexto.Dispose();
        conexion.Dispose();
    }

    /// <summary>SQLite no compara DateTimeOffset; aquí se guardan como número. En PostgreSQL son timestamptz.</summary>
    private sealed class FechasComoNumero(ModelCustomizerDependencies dependencias) : RelationalModelCustomizer(dependencias)
    {
        public override void Customize(ModelBuilder modelo, DbContext contexto)
        {
            base.Customize(modelo, contexto);
            var fechas = modelo.Model.GetEntityTypes()
                .SelectMany(entidad => entidad.GetProperties())
                .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?));
            foreach (var propiedad in fechas)
                propiedad.SetValueConverter(new DateTimeOffsetToBinaryConverter());
        }
    }
}

/// <summary>Doble de IUsuarioActual: Control de acceso diría que este es el usuario de la petición (o que no hay sesión).</summary>
public sealed class UsuarioActualFalso(UsuarioAutenticado? usuario) : IUsuarioActual
{
    public UsuarioAutenticado? Usuario { get; } = usuario;
}

/// <summary>
/// Doble de IPermisosConcedidos: Gestión de permisos diría que estos permisos están aprobados. Anota cada consulta.
/// La prueba con la pieza real está en Inventario.Integracion.Pruebas.
/// </summary>
public sealed class PermisosConcedidosFalso : IPermisosConcedidos
{
    public HashSet<(int UsuarioId, string Permiso)> Concedidos { get; } = [];
    public List<(int UsuarioId, string Permiso)> Consultas { get; } = [];

    public Task<bool> EstaConcedidoAsync(int usuarioId, string permiso, CancellationToken ct = default)
    {
        Consultas.Add((usuarioId, permiso));
        return Task.FromResult(Concedidos.Contains((usuarioId, permiso)));
    }
}

/// <summary>
/// Doble de ITasasDeCambio: responde la tasa o el fallo que la prueba le diga, sin salir a internet, y anota cada consulta.
/// El cliente real de la API se prueba aparte en TasasExchangeRateApiPruebas, con respuestas HTTP simuladas.
/// </summary>
public sealed class TasasFalsas : ITasasDeCambio
{
    public static readonly DateTimeOffset Publicada = new(2026, 11, 9, 0, 0, 1, TimeSpan.Zero);

    public Resultado<TasaDeCambio>? Respuesta { get; set; }
    public List<(string Desde, string Hacia)> Consultas { get; } = [];

    public string Fuente => "Tasas de prueba";

    public Task<Resultado<TasaDeCambio>> ObtenerAsync(string desde, string hacia, CancellationToken ct = default)
    {
        Consultas.Add((desde, hacia));
        return Task.FromResult(Respuesta ?? throw new InvalidOperationException("La prueba no dijo qué tasa responder."));
    }

    public void Responder(decimal valor) =>
        Respuesta = Resultado<TasaDeCambio>.Ok(new TasaDeCambio("DOP", "USD", valor, Publicada));

    public void Fallar(TipoFallo fallo, string mensaje) => Respuesta = Resultado<TasaDeCambio>.Falla(fallo, mensaje);
}
