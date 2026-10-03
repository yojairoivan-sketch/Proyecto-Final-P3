using Inventario.Core.ControlAcceso.Contrasenas;
using Inventario.Core.ControlAcceso.Cuentas;
using Inventario.Core.ControlAcceso.Sesiones;
using Inventario.Core.ControlAcceso.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Core.ControlAcceso;

public static class ServiciosControlAcceso
{
    /// <summary>Registra las interfaces provistas por Control de acceso. Requiere IColaCorreos y TimeProvider.</summary>
    public static IServiceCollection AddControlAcceso(
        this IServiceCollection services,
        string cadenaConexion,
        Action<OpcionesControlAcceso> configurar)
    {
        var opciones = new OpcionesControlAcceso();
        configurar(opciones);
        services.AddSingleton(opciones);

        services.AddDbContext<ControlAccesoDbContext>(o => o
            .UseNpgsql(cadenaConexion, npgsql => npgsql.MigrationsHistoryTable("__ef_migraciones", ControlAccesoDbContext.Esquema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IServicioCuentas, ServicioCuentas>();
        services.AddScoped<IServicioSesiones, ServicioSesiones>();
        services.AddScoped<IServicioUsuarios, ServicioUsuarios>();
        services.AddScoped<IServicioContrasenas, ServicioContrasenas>();
        return services;
    }

    /// <summary>Crea o actualiza las tablas de Control de acceso. Lo llama el host al arrancar.</summary>
    public static async Task MigrarControlAccesoAsync(this IServiceProvider servicios, CancellationToken ct = default)
    {
        await using var alcance = servicios.CreateAsyncScope();
        await alcance.ServiceProvider.GetRequiredService<ControlAccesoDbContext>().Database.MigrateAsync(ct);
    }
}
