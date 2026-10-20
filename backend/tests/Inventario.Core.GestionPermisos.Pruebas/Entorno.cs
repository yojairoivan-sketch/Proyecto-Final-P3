using Inventario.Core.ControlAcceso;
using Inventario.Core.GestionPermisos.Solicitudes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Time.Testing;

namespace Inventario.Core.GestionPermisos.Pruebas;

/// <summary>
/// Gestión de permisos aislada (RD-12): base SQLite en memoria, reloj controlado y dobles de Control de acceso.
/// No levanta la API, ni PostgreSQL, ni Control de acceso: la pieza solo lo conoce por sus interfaces.
/// Cada prueba crea su propio entorno, así que no comparten datos y pasan en cualquier orden.
/// </summary>
public sealed class Entorno : IDisposable
{
    public static readonly UsuarioAutenticado Ana = new(10, "Ana", "ana@ejemplo.com", Rol.Estandar, SesionId: 1);
    public static readonly UsuarioAutenticado Beto = new(11, "Beto", "beto@ejemplo.com", Rol.Estandar, SesionId: 2);
    public static readonly UsuarioAutenticado Carla = new(1, "Carla", "carla@ejemplo.com", Rol.Administrador, SesionId: 3);
    public static readonly UsuarioAutenticado Dario = new(2, "Darío", "dario@ejemplo.com", Rol.Administrador, SesionId: 4);

    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly List<GestionPermisosDbContext> contextos = [];

    public Entorno()
    {
        conexion.Open();
        NuevoContexto().Database.EnsureCreated();
    }

    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
    public OtorgamientoFalso Otorgamiento { get; } = new();

    public OpcionesGestionPermisos Opciones { get; } = new()
    {
        PermisosSolicitables =
        [
            new("ListarUsuarios", "Ver la lista de usuarios."),
            new("ReactivarUsuario", "Reactivar a un usuario desactivado."),
        ],
    };

    /// <summary>El servicio con un contexto nuevo, como cada petición en la API, con <paramref name="usuario"/> como usuario actual.</summary>
    public IServicioSolicitudes Como(UsuarioAutenticado usuario) =>
        new ServicioSolicitudes(NuevoContexto(), new UsuarioActualFalso(usuario), Otorgamiento, Reloj, Opciones);

    /// <summary>El servicio sin usuario, como al arrancar la aplicación.</summary>
    public IServicioSolicitudes ComoSistema() =>
        new ServicioSolicitudes(NuevoContexto(), new UsuarioActualFalso(null), Otorgamiento, Reloj, Opciones);

    /// <summary>Crea una solicitud que nace bien, para preparar las pruebas que parten de ella.</summary>
    public async Task<int> CrearSolicitudAsync(UsuarioAutenticado solicitante, string permiso = "ListarUsuarios")
    {
        var creada = await Como(solicitante).CrearAsync(permiso, justificacion: null);
        Assert.True(creada.Exito, creada.Mensaje);
        return creada.Valor!.Id;
    }

    /// <summary>Lo que quedó guardado, leído con un contexto nuevo: no lo que el servicio tenía en memoria.</summary>
    internal SolicitudPermiso Leer(int id) => NuevoContexto().Solicitudes.AsNoTracking().Single(s => s.Id == id);

    internal int ContarSolicitudes() => NuevoContexto().Solicitudes.Count();

    private GestionPermisosDbContext NuevoContexto()
    {
        var contexto = new GestionPermisosDbContext(new DbContextOptionsBuilder<GestionPermisosDbContext>()
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

/// <summary>Doble de IUsuarioActual: Control de acceso diría que este es el usuario de la petición.</summary>
public sealed class UsuarioActualFalso(UsuarioAutenticado? usuario) : IUsuarioActual
{
    public UsuarioAutenticado? Usuario { get; } = usuario;
}

/// <summary>Doble de IOtorgamientoPermisos: anota lo que Gestión de permisos otorgaría en Control de acceso.</summary>
public sealed class OtorgamientoFalso : IOtorgamientoPermisos
{
    public List<(int UsuarioId, string Permiso)> Otorgados { get; } = [];

    /// <summary>Si es true, la próxima llamada falla, como si Control de acceso no respondiera.</summary>
    public bool FallarLaProxima { get; set; }

    public Task OtorgarAsync(int usuarioId, string permiso, CancellationToken ct = default)
    {
        if (FallarLaProxima)
        {
            FallarLaProxima = false;
            throw new InvalidOperationException("Control de acceso no respondió.");
        }

        Otorgados.Add((usuarioId, permiso));
        return Task.CompletedTask;
    }
}
