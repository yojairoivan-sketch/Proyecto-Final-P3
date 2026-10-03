using Inventario.Core.ControlAcceso.Seguridad;
using Inventario.Core.ControlAcceso.Validacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Core.ControlAcceso.Usuarios;

public static class AdministradorInicial
{
    /// <summary>
    /// Crea el primer Administrador, ya activo, si todavía no existe una cuenta con ese correo.
    /// Los datos llegan del host (variables ADMIN_*); si no son válidos, el arranque se detiene con un mensaje claro.
    /// </summary>
    public static async Task<string> AsegurarAdministradorInicialAsync(
        this IServiceProvider servicios,
        string? nombre,
        string? correo,
        string? contrasena,
        CancellationToken ct = default)
    {
        var (nombreValido, errorNombre) = ValidacionEntrada.Nombre(nombre);
        var (correoValido, errorCorreo) = ValidacionEntrada.Correo(correo);
        var errorContrasena = PoliticaContrasena.Validar(contrasena);

        var errores = new[]
        {
            errorNombre is null ? null : $"ADMIN_NOMBRE: {errorNombre}",
            errorCorreo is null ? null : $"ADMIN_CORREO: {errorCorreo}",
            errorContrasena is null ? null : $"ADMIN_CONTRASENA: {errorContrasena}",
        }.Where(e => e is not null).ToArray();
        if (errores.Length > 0)
            throw new InvalidOperationException("No se puede crear el Administrador inicial. " + string.Join(" ", errores));

        await using var alcance = servicios.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<ControlAccesoDbContext>();
        var reloj = alcance.ServiceProvider.GetRequiredService<TimeProvider>();

        if (await db.Usuarios.AnyAsync(u => u.Correo == correoValido, ct))
            return $"El Administrador inicial ({correoValido}) ya existe.";

        var administrador = Usuario.CrearAdministrador(nombreValido!, correoValido!, reloj.GetUtcNow());
        administrador.CambiarHash(Secretos.HashDeContrasena(contrasena!));
        db.Usuarios.Add(administrador);
        await db.SaveChangesAsync(ct);
        return $"Administrador inicial creado: {correoValido}.";
    }
}
