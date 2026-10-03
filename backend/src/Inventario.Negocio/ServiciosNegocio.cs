using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Negocio;

public static class ServiciosNegocio
{
    public static IServiceCollection AddNegocio(this IServiceCollection services, string cadenaConexion)
    {
        services.AddDbContext<InventarioDbContext>(opciones => opciones
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", InventarioDbContext.Esquema))
            .UseSnakeCaseNamingConvention());
        return services;
    }

    /// <summary>Crea o actualiza las tablas del módulo de negocio. Lo llama el host al arrancar.</summary>
    public static async Task MigrarNegocioAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<InventarioDbContext>().Database.MigrateAsync(ct);
    }
}
