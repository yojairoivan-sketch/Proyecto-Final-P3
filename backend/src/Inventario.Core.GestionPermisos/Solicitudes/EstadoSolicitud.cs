namespace Inventario.Core.GestionPermisos.Solicitudes;

/// <summary>
/// Los estados de una solicitud de permiso, declarados en este único lugar.
/// Qué transiciones existen entre ellos se declara en <see cref="TransicionesSolicitud"/>.
/// Esta máquina es del Core y no comparte código con la del módulo de negocio (RF-NEG-09).
/// </summary>
public enum EstadoSolicitud
{
    /// <summary>Recién creada: espera que un Administrador la resuelva.</summary>
    Pendiente,

    /// <summary>Un Administrador la aprobó; el sistema la aplica enseguida.</summary>
    Aprobada,

    /// <summary>El permiso ya se le otorgó al solicitante. Terminal.</summary>
    Aplicada,

    /// <summary>Un Administrador la rechazó con un motivo. Terminal.</summary>
    Rechazada,
}
