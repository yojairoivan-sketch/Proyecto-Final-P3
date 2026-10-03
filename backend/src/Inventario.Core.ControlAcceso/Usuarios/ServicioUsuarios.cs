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
}

internal sealed class ServicioUsuarios(ControlAccesoDbContext db) : IServicioUsuarios
{
    public async Task<IReadOnlyList<UsuarioListado>> ListarAsync(CancellationToken ct = default)
    {
        var usuarios = await db.Usuarios.AsNoTracking().Include(u => u.Rol).OrderBy(u => u.Id).ToListAsync(ct);
        return usuarios.Select(u => new UsuarioListado(u.Id, u.Nombre, u.Correo, u.Rol.Nombre, u.Estado, u.CreadoEn)).ToList();
    }
}
