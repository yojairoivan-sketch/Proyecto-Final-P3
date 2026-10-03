using Inventario.Core.ControlAcceso.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ControlAcceso.Sesiones;

public sealed record SesionIniciada(string Token, DateTimeOffset VenceEn, UsuarioAutenticado Usuario);

/// <summary>Inicio, validación y cierre de sesión.</summary>
public interface IServicioSesiones
{
    /// <summary>
    /// RF-CA-03: abre una sesión con correo y contraseña. Un correo inexistente y una contraseña
    /// incorrecta dan el mismo rechazo.
    /// </summary>
    Task<Resultado<SesionIniciada>> IniciarAsync(string? correo, string? contrasena, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el dueño de la credencial si la sesión no está cerrada ni vencida y el usuario sigue activo.
    /// Se llama en cada petición.
    /// </summary>
    Task<UsuarioAutenticado?> ValidarAsync(string token, CancellationToken ct = default);

    /// <summary>RF-CA-18: la credencial cerrada deja de servir.</summary>
    Task<Resultado> CerrarAsync(long sesionId, CancellationToken ct = default);
}

internal sealed class ServicioSesiones(
    ControlAccesoDbContext db,
    TimeProvider reloj,
    OpcionesControlAcceso opciones) : IServicioSesiones
{
    public async Task<Resultado<SesionIniciada>> IniciarAsync(string? correo, string? contrasena, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrEmpty(contrasena))
            return Resultado<SesionIniciada>.Falla(TipoFallo.Validacion, "Escribe tu correo y tu contraseña.");

        var correoNormalizado = correo.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.Include(u => u.Rol).SingleOrDefaultAsync(u => u.Correo == correoNormalizado, ct);

        if (usuario is null)
        {
            // Mismo trabajo y misma respuesta que con una contraseña incorrecta: no se revela qué dato falló.
            Secretos.VerificarContraHashFicticio(contrasena);
            return CredencialesIncorrectas();
        }

        var (correcta, rehacerHash) = Secretos.Verificar(usuario.HashContrasena, contrasena);
        if (!correcta)
            return CredencialesIncorrectas();

        if (!usuario.Activo)
        {
            return Resultado<SesionIniciada>.Falla(TipoFallo.CuentaInactiva, usuario.PendienteDeActivacion
                ? "La cuenta no está activa. Ábrela con el enlace que te enviamos por correo."
                : "La cuenta está desactivada. Habla con un administrador.");
        }

        if (rehacerHash)
            usuario.CambiarHash(Secretos.HashDeContrasena(contrasena));

        var ahora = reloj.GetUtcNow();
        var token = Secretos.NuevoToken();
        var sesion = Sesion.Abrir(usuario, Secretos.HashDeToken(token), ahora, opciones.VigenciaSesion);
        db.Sesiones.Add(sesion);
        await db.SaveChangesAsync(ct);

        return Resultado<SesionIniciada>.Ok(new SesionIniciada(token, sesion.VenceEn, Autenticado(usuario, sesion.Id)));
    }

    public async Task<UsuarioAutenticado?> ValidarAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var hash = Secretos.HashDeToken(token.Trim());
        var ahora = reloj.GetUtcNow();
        var sesion = await db.Sesiones
            .AsNoTracking()
            .Include(s => s.Usuario).ThenInclude(u => u.Rol)
            .SingleOrDefaultAsync(s => s.HashToken == hash && s.RevocadaEn == null && s.VenceEn > ahora && s.Usuario.Activo, ct);

        return sesion is null ? null : Autenticado(sesion.Usuario, sesion.Id);
    }

    public async Task<Resultado> CerrarAsync(long sesionId, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        await db.Sesiones
            .Where(s => s.Id == sesionId && s.RevocadaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadaEn, ahora), ct);
        return Resultado.Ok("Sesión cerrada.");
    }

    private static UsuarioAutenticado Autenticado(Usuario usuario, long sesionId) =>
        new(usuario.Id, usuario.Nombre, usuario.Correo, usuario.Rol.Nombre, sesionId);

    private static Resultado<SesionIniciada> CredencialesIncorrectas() =>
        Resultado<SesionIniciada>.Falla(TipoFallo.NoAutenticado, "Correo o contraseña incorrectos.");
}
