using Inventario.Negocio.Cotizaciones;
using Inventario.Negocio.Existencias;
using Inventario.Negocio.Ordenes;
using Inventario.Negocio.Productos;
using Inventario.Negocio.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventario.Negocio;

public static class ServiciosNegocio
{
    /// <summary>
    /// Registra los servicios del módulo de negocio. Requiere de Control de acceso IUsuarioActual,
    /// de Gestión de permisos IPermisosConcedidos, y un TimeProvider. <paramref name="configurarTasas"/> da la clave
    /// de la API de tasas de cambio; sin clave, solo la cotización en otra moneda se rechaza.
    /// </summary>
    public static IServiceCollection AddNegocio(
        this IServiceCollection services, string cadenaConexion, Action<OpcionesTasasDeCambio>? configurarTasas = null)
    {
        var opcionesTasas = new OpcionesTasasDeCambio();
        configurarTasas?.Invoke(opcionesTasas);

        services.AddDbContext<InventarioDbContext>(opciones => opciones
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", InventarioDbContext.Esquema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IServicioProveedores, ServicioProveedores>();
        services.AddScoped<IServicioProductos, ServicioProductos>();
        services.AddScoped<IServicioOrdenes, ServicioOrdenes>();
        services.AddScoped<IServicioExistencias, ServicioExistencias>();

        // Un solo HttpClient para la API externa, con conexiones que se renuevan para no quedarse con un DNS viejo.
        services.AddSingleton<ITasasDeCambio>(proveedor => new TasasExchangeRateApi(
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) }),
            opcionesTasas,
            proveedor.GetRequiredService<ILogger<TasasExchangeRateApi>>()));
        services.AddScoped<IServicioCotizaciones, ServicioCotizaciones>();
        return services;
    }

    /// <summary>Crea o actualiza las tablas del módulo de negocio. Lo llama el host al arrancar.</summary>
    public static async Task MigrarNegocioAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<InventarioDbContext>().Database.MigrateAsync(ct);
    }
}
