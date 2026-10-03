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
}

internal sealed class ServicioUsuarios(ControlAccesoDbContext db) : IServicioUsuarios
{
    public async Task<IReadOnlyList<UsuarioListado>> ListarAsync(CancellationToken ct = default)
    {
        var usuarios = await db.Usuarios.AsNoTracking().Include(u => u.Rol).OrderBy(u => u.Id).ToListAsync(ct);
        return usuarios.Select(u => new UsuarioListado(u.Id, u.Nombre, u.Correo, u.Rol.Nombre, u.Estado, u.CreadoEn)).ToList();
    }

    public async Task<Resultado> CambiarRolAsync(int usuarioId, string? rol, CancellationToken ct = default)
    {
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

        usuario.CambiarRol(rolId);
        await db.SaveChangesAsync(ct);
        return Resultado.Ok($"Rol cambiado a {(rolId == Rol.IdAdministrador ? Rol.Administrador : Rol.Estandar)}.");
    }

    private static Resultado NoExiste() => Resultado.Falla(TipoFallo.NoEncontrado, "El usuario no existe.");
}
