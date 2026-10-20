namespace Inventario.Core.ControlAcceso;

/// <summary>
/// Interfaz provista por Control de acceso a Gestión de permisos: «desde ahora este usuario puede ejecutar esta operación».
/// El permiso queda en Control de acceso, que es quien responde qué puede hacer cada usuario;
/// Gestión de permisos nunca escribe en estas tablas.
/// </summary>
public interface IOtorgamientoPermisos
{
    /// <summary>Otorga el permiso. Si el usuario ya lo tenía no hace nada, así que repetirlo no duplica.</summary>
    Task OtorgarAsync(int usuarioId, string permiso, CancellationToken ct = default);
}
