namespace Inventario.Core.ControlAcceso;

public sealed class Usuario
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";

    /// <summary>Único y siempre en minúsculas.</summary>
    public string Correo { get; private set; } = "";

    /// <summary>Hash PBKDF2 con sal (RD-05). Nunca la contraseña.</summary>
    public string HashContrasena { get; private set; } = "";

    /// <summary>Todo usuario tiene exactamente un rol (RF-CA-04).</summary>
    public int RolId { get; private set; }
    public Rol Rol { get; private set; } = null!;

    public bool Activo { get; private set; }

    /// <summary>Cuándo abrió el enlace de activación. Nulo mientras la cuenta está pendiente.</summary>
    public DateTimeOffset? ActivadoEn { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    private Usuario()
    {
    }

    /// <summary>El usuario nace Estándar e inactivo (RF-CA-15).</summary>
    internal static Usuario Registrar(string nombre, string correo, DateTimeOffset ahora) => new()
    {
        Nombre = nombre,
        Correo = correo,
        RolId = Rol.IdEstandar,
        Activo = false,
        CreadoEn = ahora,
    };

    internal void CambiarHash(string hash) => HashContrasena = hash;

    internal void Activar(DateTimeOffset ahora)
    {
        Activo = true;
        ActivadoEn ??= ahora;
    }

    public bool PendienteDeActivacion => !Activo && ActivadoEn is null;

    public string Estado => Activo ? "Activo" : ActivadoEn is null ? "Pendiente de activación" : "Desactivado";
}
