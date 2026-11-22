using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Inventario.Core.Auditoria.Pruebas;

/// <summary>
/// La pieza de Auditoría aislada (RD-12): base SQLite en memoria y fechas fijas. No levanta la API ni PostgreSQL.
/// Cada prueba crea su propio entorno, así que no comparten datos y pasan en cualquier orden.
/// </summary>
public sealed class Entorno : IDisposable
{
    public static readonly Consultante Administrador = new(1, "Administrador", EsAdministrador: true);
    public static readonly Consultante Estandar = new(2, "Estándar", EsAdministrador: false);

    public static readonly QuienActua Carla = new(1, "carla@ejemplo.com", "Administrador");
    public static readonly QuienActua Ana = new(10, "ana@ejemplo.com", "Estándar");

    /// <summary>El día del negocio: Santo Domingo, UTC−4, como lo configura el host con el desfase de los reportes.</summary>
    public static readonly TimeSpan DesfaseNegocio = TimeSpan.FromHours(-4);

    private readonly SqliteConnection conexion = new("DataSource=:memory:");
    private readonly List<AuditoriaDbContext> contextos = [];

    public Entorno()
    {
        conexion.Open();
        NuevoContexto().Database.EnsureCreated();
    }

    public OpcionesAuditoria Opciones { get; } = new()
    {
        Entidades = ["Usuario", "SolicitudPermiso", "Documento"],
        DesfaseNegocio = DesfaseNegocio,
    };

    /// <summary>La consulta con un contexto nuevo, como cada petición en la API.</summary>
    public IConsultaAuditoria Consulta() => new ConsultaAuditoria(NuevoContexto(), Opciones);

    /// <summary>Una hora del día del negocio (Santo Domingo), por ejemplo «el 30 de noviembre a las 23:59».</summary>
    public static DateTimeOffset HoraDelNegocio(int anio, int mes, int dia, int hora = 12, int minuto = 0, int segundo = 0) =>
        new(anio, mes, dia, hora, minuto, segundo, DesfaseNegocio);

    /// <summary>Deja un registro en el historial, como lo hace una pieza auditada al guardar su cambio. Devuelve su id.</summary>
    public long Registrar(
        DateTimeOffset cuando, QuienActua? quien = null, string accion = "CambiarRol", string entidad = "Usuario", object? entidadId = null)
    {
        var db = NuevoContexto();
        var registro = db.Auditar(quien ?? Carla, accion, entidad, entidadId ?? 5,
            new { rol = "Estándar" }, new { rol = "Administrador" }, cuando);
        db.SaveChanges();
        return registro.Id;
    }

    /// <summary>Lo que quedó guardado, leído con un contexto nuevo.</summary>
    internal List<RegistroAuditoria> Leer() => NuevoContexto().Historial.AsNoTracking().OrderBy(r => r.Id).ToList();

    internal AuditoriaDbContext NuevoContexto()
    {
        var contexto = new AuditoriaDbContext(new DbContextOptionsBuilder<AuditoriaDbContext>()
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
