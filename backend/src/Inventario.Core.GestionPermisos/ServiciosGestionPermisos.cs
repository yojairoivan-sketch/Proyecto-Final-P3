using Inventario.Core.GestionPermisos.Solicitudes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Core.GestionPermisos;

public static class ServiciosGestionPermisos
{
    /// <summary>
    /// Registra la interfaz provista por Gestión de permisos. Requiere de Control de acceso
    /// IUsuarioActual e IOtorgamientoPermisos, y un TimeProvider.
    /// </summary>
    public static IServiceCollection AddGestionPermisos(
        this IServiceCollection services,
        string cadenaConexion,
        Action<OpcionesGestionPermisos> configurar)
    {
        var opciones = new OpcionesGestionPermisos();
        configurar(opciones);
        services.AddSingleton(opciones);

        services.AddDbContext<GestionPermisosDbContext>(o => o
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", GestionPermisosDbContext.Esquema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IServicioSolicitudes, ServicioSolicitudes>();
        return services;
    }

    /// <summary>Crea o actualiza las tablas de Gestión de permisos. Lo llama el host al arrancar.</summary>
    public static async Task MigrarGestionPermisosAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<GestionPermisosDbContext>().Database.MigrateAsync(ct);
    }

    /// <summary>Aplica las solicitudes que quedaron Aprobadas sin aplicar. Lo llama el host al arrancar.</summary>
    public static async Task<int> AplicarSolicitudesAprobadasAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        return await alcance.ServiceProvider.GetRequiredService<IServicioSolicitudes>().AplicarAprobadasAsync(ct);
    }
}
