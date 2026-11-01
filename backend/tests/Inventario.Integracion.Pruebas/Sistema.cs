using Inventario.Core.ControlAcceso;
using Inventario.Core.ControlAcceso.Permisos;
using Inventario.Core.ControlAcceso.Seguridad;
using Inventario.Core.ControlAcceso.Sesiones;
using Inventario.Core.GestionPermisos;
using Inventario.Core.GestionPermisos.Solicitudes;
using Inventario.Negocio;
using Inventario.Negocio.Existencias;
using Inventario.Negocio.Productos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Time.Testing;

namespace Inventario.Integracion.Pruebas;

/// <summary>
/// Control de acceso, Gestión de permisos y el módulo de negocio reales, conectados como los conecta el host,
/// pero sin la API: cada «petición» valida la credencial en Control de acceso y arma los servicios con un contexto nuevo.
/// Cada pieza tiene su propia base SQLite en memoria (en PostgreSQL, su propio esquema). Ningún doble reemplaza a ninguna.
/// Cada prueba crea su propio sistema, así que no comparten datos y pasan en cualquier orden.
/// </summary>
public sealed class Sistema : IDisposable
{
    private const string Clave = "clave1234";

    private readonly SqliteConnection baseControlAcceso = new("DataSource=:memory:");
    private readonly SqliteConnection baseGestionPermisos = new("DataSource=:memory:");
    private readonly SqliteConnection baseNegocio = new("DataSource=:memory:");
    private readonly List<DbContext> contextos = [];
    private readonly OpcionesControlAcceso opcionesAcceso = new() { UrlPublica = "http://localhost:8080" };

    // El catálogo como lo arma el host: lo que el negocio deja pedir.
    private readonly OpcionesGestionPermisos opcionesPermisos = new() { PermisosSolicitables = PermisosNegocio.Solicitables };

    public Sistema()
    {
        foreach (var conexion in new[] { baseControlAcceso, baseGestionPermisos, baseNegocio })
            conexion.Open();
        ControlAcceso().Database.EnsureCreated();
        GestionPermisos().Database.EnsureCreated();
        Negocio().Database.EnsureCreated();
    }

    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Crea en Control de acceso un usuario activo e inicia su sesión. Devuelve su credencial.</summary>
    public async Task<string> EntrarAsync(string nombre, string correo, bool administrador)
    {
        var ahora = Reloj.GetUtcNow();
        var usuario = administrador ? Usuario.CrearAdministrador(nombre, correo, ahora) : Usuario.Registrar(nombre, correo, ahora);
        usuario.Activar(ahora);
        usuario.CambiarHash(Secretos.HashDeContrasena(Clave));
        var db = ControlAcceso();
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        var sesion = await Sesiones().IniciarAsync(correo, Clave);
        Assert.True(sesion.Exito, sesion.Mensaje);
        return sesion.Valor!.Token;
    }

    // Una petición de quien trae esa credencial a cada pieza, como en la API.
    public async Task<IServicioSolicitudes> SolicitudesAsync(string credencial) =>
        new ServicioSolicitudes(GestionPermisos(), await UsuarioDeAsync(credencial), new OtorgamientoPermisos(ControlAcceso(), Reloj), Reloj, opcionesPermisos);

    public async Task<IServicioExistencias> ExistenciasAsync(string credencial) =>
        new ServicioExistencias(Negocio(), await UsuarioDeAsync(credencial), new PermisosConcedidos(GestionPermisos()), Reloj);

    public async Task<IServicioProductos> ProductosAsync(string credencial) =>
        new ServicioProductos(Negocio(), await UsuarioDeAsync(credencial), Reloj);

    /// <summary>Lo que Control de acceso responde sobre la credencial en una petición nueva.</summary>
    public Task<UsuarioAutenticado?> ValidarAsync(string credencial) => Sesiones().ValidarAsync(credencial);

    /// <summary>Un producto nuevo, con existencia 0, registrado por quien trae la credencial.</summary>
    public async Task<int> CrearProductoAsync(string credencial)
    {
        var creado = await (await ProductosAsync(credencial)).RegistrarAsync("Arroz", "ARR-1", "saco", 5);
        Assert.True(creado.Exito, creado.Mensaje);
        return creado.Valor!.Id;
    }

    /// <summary>Lo que quedó guardado en el negocio, leído con un contexto nuevo.</summary>
    internal Producto LeerProducto(int id) => Negocio().Productos.AsNoTracking().Single(p => p.Id == id);

    internal List<MovimientoExistencia> LeerMovimientos(int productoId) =>
        Negocio().Movimientos.AsNoTracking().Where(m => m.ProductoId == productoId).ToList();

    public void Dispose()
    {
        foreach (var contexto in contextos)
            contexto.Dispose();
        baseControlAcceso.Dispose();
        baseGestionPermisos.Dispose();
        baseNegocio.Dispose();
    }

    /// <summary>
    /// Como el host en cada petición (AutenticacionSesion + UsuarioActualHttp): Control de acceso valida la credencial
    /// y el usuario que devuelve es el usuario actual de esa petición.
    /// </summary>
    private async Task<IUsuarioActual> UsuarioDeAsync(string credencial)
    {
        var usuario = await Sesiones().ValidarAsync(credencial);
        Assert.NotNull(usuario);
        return new UsuarioDeLaPeticion(usuario);
    }

    private ServicioSesiones Sesiones() => new(ControlAcceso(), Reloj, opcionesAcceso);

    private ControlAccesoDbContext ControlAcceso() =>
        Nuevo(new ControlAccesoDbContext(Opciones<ControlAccesoDbContext>(baseControlAcceso)));

    private GestionPermisosDbContext GestionPermisos() =>
        Nuevo(new GestionPermisosDbContext(Opciones<GestionPermisosDbContext>(baseGestionPermisos)));

    private InventarioDbContext Negocio() =>
        Nuevo(new InventarioDbContext(Opciones<InventarioDbContext>(baseNegocio)));

    private T Nuevo<T>(T contexto) where T : DbContext
    {
        contextos.Add(contexto);
        return contexto;
    }

    private static DbContextOptions<T> Opciones<T>(SqliteConnection conexion) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlite(conexion).ReplaceService<IModelCustomizer, FechasComoNumero>().Options;

    /// <summary>El usuario de la petición, tal como lo devolvió Control de acceso.</summary>
    private sealed class UsuarioDeLaPeticion(UsuarioAutenticado usuario) : IUsuarioActual
    {
        public UsuarioAutenticado? Usuario { get; } = usuario;
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
