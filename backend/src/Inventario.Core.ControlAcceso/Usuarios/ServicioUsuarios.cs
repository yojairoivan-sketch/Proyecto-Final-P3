using Inventario.Core.Auditoria;
using Inventario.Core.ControlAcceso.Sesiones;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ControlAcceso.Usuarios;

/// <summary>Lo que se muestra de un usuario en el listado. Nunca incluye hashes ni tokens (RF-CA-21).</summary>
public sealed record UsuarioListado(int Id, string Nombre, string Correo, string Rol, string Estado, DateTimeOffset CreadoEn);

/// <summary>
/// Administración de usuarios. Quién puede llamar a cada operación lo decide el host (RF-CA-05);
/// aquí solo viven las reglas del dominio.
/// </summary>
public interface IServicioUsuarios
{
    /// <summary>RF-CA-21: todos los usuarios con su rol y su estado.</summary>
    Task<IReadOnlyList<UsuarioListado>> ListarAsync(CancellationToken ct = default);

    /// <summary>RF-CA-08: cambia el rol de un usuario. Nunca deja el sistema sin un Administrador activo.</summary>
    Task<Resultado> CambiarRolAsync(int usuarioId, string? rol, CancellationToken ct = default);

    /// <summary>
    /// RF-CA-20: el usuario deja de poder iniciar sesión y sus sesiones abiertas se revocan.
    /// Un Administrador no puede desactivarse a sí mismo.
    /// </summary>
    Task<Resultado> DesactivarAsync(int administradorId, int usuarioId, CancellationToken ct = default);

    /// <summary>RF-CA-20: devuelve el acceso a un usuario desactivado.</summary>
    Task<Resultado> ReactivarAsync(int usuarioId, CancellationToken ct = default);
}

/// <summary>
/// Cada cambio (rol, desactivar, reactivar) deja su registro en el historial de auditoría con el valor anterior y el nuevo
/// (RF-AUD-04). El registro se guarda junto con el cambio: si uno falla, no queda ninguno.
/// </summary>
internal sealed class ServicioUsuarios(ControlAccesoDbContext db, TimeProvider reloj, IUsuarioActual usuarioActual) : IServicioUsuarios
{
    public async Task<IReadOnlyList<UsuarioListado>> ListarAsync(CancellationToken ct = default)
    {
        var usuarios = await db.Usuarios.AsNoTracking().Include(u => u.Rol).OrderBy(u => u.Id).ToListAsync(ct);
        return usuarios.Select(u => new UsuarioListado(u.Id, u.Nombre, u.Correo, u.Rol.Nombre, u.Estado, u.CreadoEn)).ToList();
    }

    public async Task<Resultado> CambiarRolAsync(int usuarioId, string? rol, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } quien)
            return SinSesion();
        if (Rol.IdDe(rol) is not { } rolId)
            return Resultado.Falla(TipoFallo.Validacion, $"El rol debe ser {Rol.Administrador} o {Rol.Estandar}.");

        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null)
            return NoExiste();
        if (usuario.RolId == rolId)
            return Resultado.Ok("El usuario ya tenía ese rol.");

        if (usuario.RolId == Rol.IdAdministrador && usuario.Activo
            && await db.Usuarios.CountAsync(u => u.RolId == Rol.IdAdministrador && u.Activo, ct) == 1)
            return Resultado.Falla(TipoFallo.Conflicto, "No se puede quitar el rol al último Administrador activo.");

        var anterior = Rol.NombreDe(usuario.RolId);
        usuario.CambiarRol(rolId);
        db.Auditar(quien.ComoQuienActua(), AuditoriaUsuarios.CambiarRol, AuditoriaUsuarios.Entidad, usuario.Id,
            new { rol = anterior }, new { rol = Rol.NombreDe(rolId) }, reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return Resultado.Ok($"Rol cambiado a {Rol.NombreDe(rolId)}.");
    }

    public async Task<Resultado> DesactivarAsync(int administradorId, int usuarioId, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } quien)
            return SinSesion();
        if (usuarioId == administradorId)
            return Resultado.Falla(TipoFallo.Conflicto, "Un Administrador no puede desactivarse a sí mismo.");

        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null)
            return NoExiste();
        if (usuario.PendienteDeActivacion)
            return Resultado.Falla(TipoFallo.Conflicto, "La cuenta todavía no se ha activado; no hay nada que desactivar.");
        if (!usuario.Activo)
            return Resultado.Ok("El usuario ya estaba desactivado.");

        // El cambio, la revocación de sesiones y el registro de auditoría van en la misma transacción.
        var ahora = reloj.GetUtcNow();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        usuario.Desactivar();
        await db.SaveChangesAsync(ct);
        var cerradas = await db.RevocarSesionesDeAsync(usuario.Id, ahora, ct);
        db.Auditar(quien.ComoQuienActua(), AuditoriaUsuarios.DesactivarUsuario, AuditoriaUsuarios.Entidad, usuario.Id,
            new { estado = "Activo" }, new { estado = "Desactivado", sesionesCerradas = cerradas }, ahora);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return Resultado.Ok("Usuario desactivado. Sus sesiones abiertas dejaron de servir.");
    }

    public async Task<Resultado> ReactivarAsync(int usuarioId, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } quien)
            return SinSesion();

        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null)
            return NoExiste();
        if (usuario.Activo)
            return Resultado.Ok("El usuario ya estaba activo.");
        if (usuario.PendienteDeActivacion)
            return Resultado.Falla(TipoFallo.Conflicto, "La cuenta nunca se activó: el usuario debe abrir su enlace de activación.");

        usuario.Reactivar();
        db.Auditar(quien.ComoQuienActua(), AuditoriaUsuarios.ReactivarUsuario, AuditoriaUsuarios.Entidad, usuario.Id,
            new { estado = "Desactivado" }, new { estado = "Activo" }, reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return Resultado.Ok("Usuario reactivado. Ya puede iniciar sesión.");
    }

    private static Resultado NoExiste() => Resultado.Falla(TipoFallo.NoEncontrado, "El usuario no existe.");

    private static Resultado SinSesion() => Resultado.Falla(TipoFallo.NoAutenticado, "Inicia sesión para administrar usuarios.");
}
