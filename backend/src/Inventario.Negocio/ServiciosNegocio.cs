using Inventario.Negocio.Existencias;
using Inventario.Negocio.Ordenes;
using Inventario.Negocio.Productos;
using Inventario.Negocio.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Negocio;

public static class ServiciosNegocio
{
    /// <summary>
    /// Registra los servicios del módulo de negocio. Requiere de Control de acceso IUsuarioActual,
    /// de Gestión de permisos IPermisosConcedidos, y un TimeProvider.
    /// </summary>
    public static IServiceCollection AddNegocio(this IServiceCollection services, string cadenaConexion)
    {
        services.AddDbContext<InventarioDbContext>(opciones => opciones
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", InventarioDbContext.Esquema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IServicioProveedores, ServicioProveedores>();
        services.AddScoped<IServicioProductos, ServicioProductos>();
        services.AddScoped<IServicioOrdenes, ServicioOrdenes>();
        services.AddScoped<IServicioExistencias, ServicioExistencias>();
        return services;
    }

    /// <summary>Crea o actualiza las tablas del módulo de negocio. Lo llama el host al arrancar.</summary>
    public static async Task MigrarNegocioAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<InventarioDbContext>().Database.MigrateAsync(ct);
    }
}
