using Inventario.Core.Auditoria;

namespace Inventario.Core.ControlAcceso;

/// <summary>Quién hizo la petición. No incluye hashes ni tokens.</summary>
public sealed record UsuarioAutenticado(int Id, string Nombre, string Correo, string Rol, long SesionId)
{
    public bool EsAdministrador => Rol == ControlAcceso.Rol.Administrador;

    /// <summary>Cómo aparece en el historial de auditoría: su id, su correo y el rol con el que actuó.</summary>
    public QuienActua ComoQuienActua() => new(Id, Correo, Rol);

    /// <summary>Operaciones que puede ejecutar además de las de su rol, por solicitudes aprobadas (RF-GP-05).</summary>
    public IReadOnlyList<string> Permisos { get; init; } = [];
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
