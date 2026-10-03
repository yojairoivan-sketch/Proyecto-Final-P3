using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Core.ColaCorreos;

public static class ServiciosColaCorreos
{
    /// <summary>Registra la interfaz provista IColaCorreos. Quien solo encola no necesita datos de SMTP.</summary>
    public static IServiceCollection AddColaCorreos(this IServiceCollection services, string cadenaConexion)
    {
        services.AddDbContext<ColaCorreosDbContext>(opciones => opciones
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", ColaCorreosDbContext.Esquema))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IColaCorreos, ColaCorreos>();
        return services;
    }

    /// <summary>Registra el procesador que entrega los pendientes por SMTP.</summary>
    public static IServiceCollection AddProcesadorCola(this IServiceCollection services, ConfiguracionSmtp smtp)
    {
        services.AddSingleton(smtp);
        services.AddScoped<ITransporteCorreo, TransporteSmtp>();
        services.AddScoped<ProcesadorCola>();
        return services;
    }

    /// <summary>Crea o actualiza las tablas de la cola. Lo llama el host al arrancar.</summary>
    public static async Task MigrarColaCorreosAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<ColaCorreosDbContext>().Database.MigrateAsync(ct);
    }
}
