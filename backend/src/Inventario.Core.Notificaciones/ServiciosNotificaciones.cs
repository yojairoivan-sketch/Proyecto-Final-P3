using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Core.Notificaciones;

public static class ServiciosNotificaciones
{
    /// <summary>
    /// Registra las interfaces provistas por Notificaciones. Requiere de Control de acceso IUsuarioActual e IDirectorioUsuarios,
    /// de la Cola de correos IColaCorreos, y un TimeProvider.
    /// </summary>
    public static IServiceCollection AddNotificaciones(this IServiceCollection services, string cadenaConexion)
    {
        services.AddDbContext<NotificacionesDbContext>(o => o
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", NotificacionesDbContext.Esquema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<INotificador, Notificador>();
        services.AddScoped<IServicioNotificaciones, ServicioNotificaciones>();
        return services;
    }

    /// <summary>Crea o actualiza las tablas de Notificaciones. Lo llama el host al arrancar.</summary>
    public static async Task MigrarNotificacionesAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<NotificacionesDbContext>().Database.MigrateAsync(ct);
    }
}
