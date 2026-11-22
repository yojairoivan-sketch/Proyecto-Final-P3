using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Core.Auditoria;

/// <summary>Lo que el host le dice a la auditoría. Así la pieza no conoce a las demás ni al negocio (RD-03).</summary>
public sealed class OpcionesAuditoria
{
    /// <summary>Las entidades que alguna pieza audita. El filtro por entidad solo acepta estas.</summary>
    public IReadOnlyList<string> Entidades { get; set; } = [];

    /// <summary>
    /// El desfase del día del negocio respecto de UTC, para que «desde» y «hasta» corten los días igual que los reportes (RD-11).
    /// El host lo toma del mismo lugar que los reportes, así el criterio está escrito una sola vez.
    /// </summary>
    public TimeSpan DesfaseNegocio { get; set; } = TimeSpan.Zero;
}

public static class ServiciosAuditoria
{
    /// <summary>Registra la interfaz provista IConsultaAuditoria. Escribir no necesita registro: cada pieza usa su propio contexto.</summary>
    public static IServiceCollection AddAuditoria(
        this IServiceCollection services, string cadenaConexion, Action<OpcionesAuditoria> configurar)
    {
        var opciones = new OpcionesAuditoria();
        configurar(opciones);
        services.AddSingleton(opciones);

        services.AddDbContext<AuditoriaDbContext>(o => o
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", HistorialAuditoria.Esquema))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IConsultaAuditoria, ConsultaAuditoria>();
        return services;
    }

    /// <summary>
    /// Crea la tabla del historial y su trigger. El host la llama al arrancar, antes que las migraciones de las piezas auditadas,
    /// porque ellas escriben en esa tabla.
    /// </summary>
    public static async Task MigrarAuditoriaAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<AuditoriaDbContext>().Database.MigrateAsync(ct);
    }
}
