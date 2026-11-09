using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Core.Documentos;

public static class ServiciosDocumentos
{
    /// <summary>
    /// Registra las interfaces provistas por el Manejador de documentos. Requiere de Control de acceso IUsuarioActual,
    /// y un TimeProvider.
    /// </summary>
    public static IServiceCollection AddDocumentos(
        this IServiceCollection services,
        string cadenaConexion,
        Action<OpcionesDocumentos> configurar)
    {
        var opciones = new OpcionesDocumentos();
        configurar(opciones);
        services.AddSingleton(opciones);

        services.AddDbContext<DocumentosDbContext>(o => o
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", DocumentosDbContext.Esquema))
            .UseSnakeCaseNamingConvention());

        services.AddSingleton<IAlmacenArchivos, AlmacenEnDisco>();
        services.AddScoped<IServicioDocumentos, ServicioDocumentos>();
        return services;
    }

    /// <summary>Crea o actualiza las tablas del Manejador de documentos. Lo llama el host al arrancar.</summary>
    public static async Task MigrarDocumentosAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<DocumentosDbContext>().Database.MigrateAsync(ct);
        // Crea la carpeta de los archivos ya al arrancar: si no se puede escribir en ella, se ve en el log desde el principio.
        alcance.ServiceProvider.GetRequiredService<IAlmacenArchivos>();
    }
}
