using Inventario.Core.ControlAcceso;

namespace Inventario.Core.GestionPermisos.Solicitudes;

/// <summary>
/// Entidad SolicitudPermiso de los Requerimientos del Core: un usuario pide poder ejecutar una operación
/// que su rol no le da, y un Administrador la aprueba o la rechaza.
/// </summary>
public sealed class SolicitudPermiso
{
    public int Id { get; private set; }

    /// <summary>Id del usuario en Control de acceso. Nombre y correo se copian al crearla para mostrarlos sin leer sus tablas.</summary>
    public int SolicitanteId { get; private set; }
    public string SolicitanteNombre { get; private set; } = "";
    public string SolicitanteCorreo { get; private set; } = "";

    /// <summary>El permiso solicitado: el nombre de la operación que quiere poder ejecutar.</summary>
    public string Permiso { get; private set; } = "";

    /// <summary>Por qué lo pide. Opcional.</summary>
    public string? Justificacion { get; private set; }

    public EstadoSolicitud Estado { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>El aprobador: el Administrador que la aprobó o la rechazó (RF-GP-03).</summary>
    public int? ResueltaPorId { get; private set; }
    public string? ResueltaPorNombre { get; private set; }

    /// <summary>Fecha de resolución: cuándo se aprobó o se rechazó.</summary>
    public DateTimeOffset? ResueltaEn { get; private set; }

    /// <summary>Motivo del rechazo (RF-GP-04). Nulo si no se rechazó.</summary>
    public string? MotivoRechazo { get; private set; }

    /// <summary>Cuándo el sistema otorgó el permiso (RF-GP-05).</summary>
    public DateTimeOffset? AplicadaEn { get; private set; }

    /// <summary>Sube con cada transición. Si dos Administradores la resuelven a la vez, el segundo en guardar falla.</summary>
    public int Version { get; private set; }

    private SolicitudPermiso()
    {
    }

    /// <summary>RF-GP-01: la solicitud nace en Pendiente.</summary>
    internal static SolicitudPermiso Crear(UsuarioAutenticado solicitante, string permiso, string? justificacion, DateTimeOffset ahora) => new()
    {
        SolicitanteId = solicitante.Id,
        SolicitanteNombre = solicitante.Nombre,
        SolicitanteCorreo = solicitante.Correo,
        Permiso = permiso,
        Justificacion = justificacion,
        Estado = EstadoSolicitud.Pendiente,
        CreadaEn = ahora,
    };

    /// <summary>
    /// Cambia el estado consultando únicamente <see cref="TransicionesSolicitud"/> (RD-04).
    /// Si se rechaza, la solicitud queda exactamente como estaba (RF-GP-06).
    /// </summary>
    internal Resultado CambiarEstado(EstadoSolicitud hacia, IntentoTransicion intento) =>
        TransicionesSolicitud.Resolver(this, hacia, intento);

    // Solo las llama la tabla de transiciones.
    internal void RegistrarResolucion(Actor administrador, DateTimeOffset ahora, string? motivo)
    {
        ResueltaPorId = administrador.UsuarioId;
        ResueltaPorNombre = administrador.Nombre;
        ResueltaEn = ahora;
        MotivoRechazo = motivo;
    }

    internal void RegistrarAplicacion(DateTimeOffset ahora) => AplicadaEn = ahora;

    internal void PasarA(EstadoSolicitud hacia)
    {
        Estado = hacia;
        Version++;
    }
}
