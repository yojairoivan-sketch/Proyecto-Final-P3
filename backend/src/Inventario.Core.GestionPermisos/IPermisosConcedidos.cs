namespace Inventario.Core.GestionPermisos;

/// <summary>
/// Interfaz provista por Gestión de permisos al módulo de negocio: «¿este usuario tiene aprobado este permiso?».
/// Responde con las solicitudes de la pieza; quien pregunta no sabe nada de sus estados (RF-NEG-09).
/// </summary>
public interface IPermisosConcedidos
{
    /// <summary>True si un Administrador aprobó una solicitud de ese usuario por ese permiso y el sistema ya la aplicó.</summary>
    Task<bool> EstaConcedidoAsync(int usuarioId, string permiso, CancellationToken ct = default);
}
