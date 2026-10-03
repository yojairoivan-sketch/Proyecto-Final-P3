namespace Inventario.Core.ControlAcceso;

/// <summary>Quién hizo la petición. No incluye hashes ni tokens.</summary>
public sealed record UsuarioAutenticado(int Id, string Nombre, string Correo, string Rol, long SesionId)
{
    public bool EsAdministrador => Rol == ControlAcceso.Rol.Administrador;
}

/// <summary>
/// Interfaz provista por Control de acceso: «¿quién es y qué rol tiene?».
/// Las demás piezas y el módulo de negocio preguntan aquí; nunca leen las tablas de usuarios.
/// </summary>
public interface IUsuarioActual
{
    /// <summary>El usuario de la petición en curso, o null si no hay una sesión válida.</summary>
    UsuarioAutenticado? Usuario { get; }
}
