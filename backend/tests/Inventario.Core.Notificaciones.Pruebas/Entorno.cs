using Inventario.Core.ColaCorreos;
using Inventario.Core.ControlAcceso;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Time.Testing;

namespace Inventario.Core.Notificaciones.Pruebas;

/// <summary>
/// Notificaciones aislada (RD-12): base SQLite en memoria, reloj controlado y dobles de Control de acceso y de la cola.
/// Cada prueba crea su propio entorno, así que no comparten datos y pasan en cualquier orden.
/// </summary>
public sealed class Entorno : IDisposable
{
    public static readonly UsuarioAutenticado Ana = new(10, "Ana", "ana@ejemplo.com", Rol.Estandar, SesionId: 1);
    public static readonly UsuarioAutenticado Beto = new(11, "Beto", "beto@ejemplo.com", Rol.Estandar, SesionId: 2);
    public static readonly UsuarioAutenticado Carla = new(1, "Carla", "carla@ejemplo.com", Rol.Administrador, SesionId: 3);
    public static readonly UsuarioAutenticado Dario = new(2, "Darío", "dario@ejemplo.com", Rol.Administrador, SesionId: 4);

    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly List<NotificacionesDbContext> contextos = [];

    public Entorno()
    {
        conexion.Open();
        NuevoContexto().Database.EnsureCreated();
    }

    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Lo que Control de acceso respondería: Carla y Darío son los Administradores activos.</summary>
    public DirectorioFalso Directorio { get; } = new([Carla, Dario], [Ana, Beto]);

    public ColaFalsa Cola { get; } = new();

    public INotificador Notificador() => new Notificador(NuevoContexto(), Directorio, Cola, Reloj);

    /// <summary>La bandeja con <paramref name="usuario"/> como usuario de la sesión (null = sin sesión).</summary>
    public IServicioNotificaciones Bandeja(UsuarioAutenticado? usuario) =>
        new ServicioNotificaciones(NuevoContexto(), new UsuarioActualFalso(usuario), Reloj);

    /// <summary>Le deja a <paramref name="usuario"/> una notificación nueva y devuelve su id.</summary>
    public async Task<long> NotificarAsync(UsuarioAutenticado usuario, string titulo = "Aviso")
    {
        await Notificador().NotificarAsync(usuario.Id, titulo, "Mensaje de prueba");
        return NuevoContexto().Notificaciones.AsNoTracking().Where(n => n.UsuarioId == usuario.Id).Max(n => n.Id);
    }

    /// <summary>Lo que quedó guardado, leído con un contexto nuevo.</summary>
    internal List<Notificacion> LeerDe(UsuarioAutenticado usuario) =>
        NuevoContexto().Notificaciones.AsNoTracking().Where(n => n.UsuarioId == usuario.Id).ToList();

    internal Notificacion Leer(long id) => NuevoContexto().Notificaciones.AsNoTracking().Single(n => n.Id == id);

    private NotificacionesDbContext NuevoContexto()
    {
        var contexto = new NotificacionesDbContext(new DbContextOptionsBuilder<NotificacionesDbContext>()
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

public sealed class UsuarioActualFalso(UsuarioAutenticado? usuario) : IUsuarioActual
{
    public UsuarioAutenticado? Usuario { get; } = usuario;
}

/// <summary>Doble de IDirectorioUsuarios: los Administradores activos y los demás usuarios que existen.</summary>
public sealed class DirectorioFalso(IEnumerable<UsuarioAutenticado> administradores, IEnumerable<UsuarioAutenticado> otros)
    : IDirectorioUsuarios
{
    public List<UsuarioContacto> Administradores { get; } = administradores.Select(Contacto).ToList();
    private readonly List<UsuarioContacto> todos = administradores.Concat(otros).Select(Contacto).ToList();

    public Task<IReadOnlyList<UsuarioContacto>> AdministradoresActivosAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<UsuarioContacto>>(Administradores.ToList());

    public Task<UsuarioContacto?> BuscarAsync(int usuarioId, CancellationToken ct = default) =>
        Task.FromResult(todos.SingleOrDefault(u => u.Id == usuarioId));

    private static UsuarioContacto Contacto(UsuarioAutenticado u) => new(u.Id, u.Nombre, u.Correo);
}

/// <summary>Doble de IColaCorreos: anota lo que se encolaría.</summary>
public sealed class ColaFalsa : IColaCorreos
{
    public List<(string Destinatario, string Asunto, string Cuerpo)> Encolados { get; } = [];

    public Task EncolarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct = default)
    {
        Encolados.Add((destinatario, asunto, cuerpo));
        return Task.CompletedTask;
    }
}
