using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ControlAcceso;

/// <summary>A quién se le puede avisar algo: el id, el nombre y el correo de un usuario. Sin hashes ni tokens.</summary>
public sealed record UsuarioContacto(int Id, string Nombre, string Correo);

/// <summary>
/// Interfaz provista por Control de acceso a Notificaciones: «¿quiénes son los Administradores activos?» y
/// «¿cuál es el correo de este usuario?». Las otras piezas nunca leen las tablas de usuarios.
/// </summary>
public interface IDirectorioUsuarios
{
    /// <summary>Los Administradores con la cuenta activa, en orden de id.</summary>
    Task<IReadOnlyList<UsuarioContacto>> AdministradoresActivosAsync(CancellationToken ct = default);

    /// <summary>El usuario, esté activo o no; null si no existe.</summary>
    Task<UsuarioContacto?> BuscarAsync(int usuarioId, CancellationToken ct = default);
}

internal sealed class DirectorioUsuarios(ControlAccesoDbContext db) : IDirectorioUsuarios
{
    public async Task<IReadOnlyList<UsuarioContacto>> AdministradoresActivosAsync(CancellationToken ct = default) =>
        await db.Usuarios.AsNoTracking()
            .Where(u => u.RolId == Rol.IdAdministrador && u.Activo)
            .OrderBy(u => u.Id)
            .Select(u => new UsuarioContacto(u.Id, u.Nombre, u.Correo))
            .ToListAsync(ct);

    public Task<UsuarioContacto?> BuscarAsync(int usuarioId, CancellationToken ct = default) =>
        db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new UsuarioContacto(u.Id, u.Nombre, u.Correo))
            .SingleOrDefaultAsync(ct);
}
