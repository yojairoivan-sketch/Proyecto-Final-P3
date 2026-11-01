using Inventario.Core.ControlAcceso;

namespace Inventario.Negocio;

/// <summary>Quién hace una operación del negocio, según lo que Control de acceso dice del usuario de la sesión.</summary>
public sealed record Responsable(int UsuarioId, string Nombre, string Rol)
{
    public bool EsAdministrador => Rol == Core.ControlAcceso.Rol.Administrador;

    public static Responsable De(UsuarioAutenticado usuario) => new(usuario.Id, usuario.Nombre, usuario.Rol);
}
