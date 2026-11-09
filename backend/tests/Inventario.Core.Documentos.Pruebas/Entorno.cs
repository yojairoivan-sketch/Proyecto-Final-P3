using System.Text;
using Inventario.Core.ControlAcceso;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Inventario.Core.Documentos.Pruebas;

/// <summary>
/// El Manejador de documentos aislado (RD-12): base SQLite en memoria, una carpeta temporal propia como almacén,
/// reloj controlado y un doble de Control de acceso. No levanta la API, ni PostgreSQL, ni Docker.
/// Cada prueba crea su propio entorno, así que no comparten datos ni archivos y pasan en cualquier orden.
/// </summary>
public sealed class Entorno : IDisposable
{
    public static readonly UsuarioAutenticado Ana = new(10, "Ana", "ana@ejemplo.com", Rol.Estandar, SesionId: 1);
    public static readonly UsuarioAutenticado Beto = new(11, "Beto", "beto@ejemplo.com", Rol.Estandar, SesionId: 2);
    public static readonly UsuarioAutenticado Carla = new(1, "Carla", "carla@ejemplo.com", Rol.Administrador, SesionId: 3);

    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly List<DocumentosDbContext> contextos = [];

    public Entorno()
    {
        conexion.Open();
        NuevoContexto().Database.EnsureCreated();
        Opciones = new OpcionesDocumentos
        {
            Carpeta = Path.Combine(Path.GetTempPath(), "inventario-documentos-pruebas", Guid.NewGuid().ToString("N")),
            TamanoMaximoBytes = 1024,
        };
        Almacen = new AlmacenEnDisco(Opciones);
    }

    public FakeTimeProvider Reloj { get; } = new(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Un máximo de 1 KB para poder probar el límite sin archivos grandes.</summary>
    public OpcionesDocumentos Opciones { get; }

    internal AlmacenEnDisco Almacen { get; }

    /// <summary>El servicio con un contexto nuevo, como cada petición en la API, con <paramref name="usuario"/> como usuario actual.</summary>
    public IServicioDocumentos Como(UsuarioAutenticado? usuario) =>
        new ServicioDocumentos(NuevoContexto(), Almacen, new UsuarioActualFalso(usuario), Reloj, Opciones,
            NullLogger<ServicioDocumentos>.Instance);

    public static MemoryStream Contenido(string texto) => new(Encoding.UTF8.GetBytes(texto));

    /// <summary>Sube un documento que nace bien, para preparar las pruebas que parten de él.</summary>
    public async Task<int> SubirAsync(UsuarioAutenticado dueno, string nombre = "factura.pdf", string texto = "contenido de prueba")
    {
        using var contenido = Contenido(texto);
        var subido = await Como(dueno).SubirAsync(nombre, "application/pdf", contenido);
        Assert.True(subido.Exito, subido.Mensaje);
        return subido.Valor!.Id;
    }

    /// <summary>Lo que quedó guardado, leído con un contexto nuevo: no lo que el servicio tenía en memoria.</summary>
    internal Documento Leer(int id) => NuevoContexto().Documentos.AsNoTracking().Single(d => d.Id == id);

    internal int ContarRegistros() => NuevoContexto().Documentos.Count();

    /// <summary>Cuántos archivos hay en la carpeta del almacén.</summary>
    public int ContarArchivos() => Directory.GetFiles(Opciones.Carpeta).Length;

    public static async Task<string> LeerTodoAsync(Stream contenido)
    {
        await using (contenido)
        {
            using var lector = new StreamReader(contenido, Encoding.UTF8);
            return await lector.ReadToEndAsync();
        }
    }

    private DocumentosDbContext NuevoContexto()
    {
        var contexto = new DocumentosDbContext(new DbContextOptionsBuilder<DocumentosDbContext>()
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
        if (Directory.Exists(Opciones.Carpeta))
            Directory.Delete(Opciones.Carpeta, recursive: true);
    }

    /// <summary>SQLite no compara ni ordena DateTimeOffset; aquí se guardan como número. En PostgreSQL son timestamptz.</summary>
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
