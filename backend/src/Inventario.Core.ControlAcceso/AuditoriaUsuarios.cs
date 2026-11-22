namespace Inventario.Core.ControlAcceso;

/// <summary>
/// Lo que Control de acceso deja en el historial de auditoría (RF-AUD-04 y 05). Las acciones se llaman como las operaciones
/// del host que las provocan. Ningún valor guarda la contraseña, su hash ni el código de recuperación.
/// </summary>
public static class AuditoriaUsuarios
{
    /// <summary>La entidad de todos estos registros. El identificador es el id del usuario afectado.</summary>
    public const string Entidad = "Usuario";

    public const string CambiarRol = "CambiarRol";
    public const string DesactivarUsuario = "DesactivarUsuario";
    public const string ReactivarUsuario = "ReactivarUsuario";

    /// <summary>Un Administrador anula la contraseña y le envía al usuario un código para definir otra.</summary>
    public const string ForzarRestablecimiento = "ForzarRestablecimiento";

    /// <summary>El usuario define una contraseña nueva con un código (lo haya pedido él o un Administrador).</summary>
    public const string RestablecerContrasena = "RestablecerContrasena";

    /// <summary>El usuario con sesión cambia su contraseña escribiendo la actual.</summary>
    public const string CambiarContrasena = "CambiarContrasena";

    // Lo que se escribe en el campo «contrasena» de los valores: dice qué pasó con ella sin guardarla.
    internal const string ContrasenaAnterior = "la anterior (no se guarda)";
    internal const string ContrasenaNueva = "una nueva (no se guarda)";
    internal const string ContrasenaAnulada = "anulada hasta que el usuario defina otra con el código que recibe por correo";
}
