namespace Inventario.Core.GestionPermisos;

/// <summary>
/// Lo que Gestión de permisos deja en el historial de auditoría (RF-AUD-02): quién aprobó o rechazó qué solicitud y cuándo.
/// Las acciones se llaman como las operaciones del host que las provocan.
/// </summary>
public static class AuditoriaSolicitudes
{
    /// <summary>La entidad de estos registros. El identificador es el id de la solicitud.</summary>
    public const string Entidad = "SolicitudPermiso";

    public const string AprobarSolicitud = "AprobarSolicitud";
    public const string RechazarSolicitud = "RechazarSolicitud";
}
