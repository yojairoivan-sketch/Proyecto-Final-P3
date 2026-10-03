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

    /// <summary>Fallos de contraseña seguidos (RF-CA-19). Vuelve a cero con un inicio correcto o al bloquear.</summary>
    public int IntentosFallidos { get; private set; }

    public DateTimeOffset? BloqueadoHasta { get; private set; }

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

    /// <summary>El primer Administrador nace activo: no hay nadie que le mande un enlace.</summary>
    internal static Usuario CrearAdministrador(string nombre, string correo, DateTimeOffset ahora) => new()
    {
        Nombre = nombre,
        Correo = correo,
        RolId = Rol.IdAdministrador,
        Activo = true,
        ActivadoEn = ahora,
        CreadoEn = ahora,
    };

    internal void CambiarHash(string hash) => HashContrasena = hash;

    internal void CambiarRol(int rolId) => RolId = rolId;

    internal void Desactivar() => Activo = false;

    /// <summary>Solo para quien ya había activado su cuenta; una pendiente se activa con su enlace.</summary>
    internal void Reactivar() => Activo = true;

    internal void Activar(DateTimeOffset ahora)
    {
        Activo = true;
        ActivadoEn ??= ahora;
    }

    internal void RegistrarIntentoFallido(DateTimeOffset ahora, int maximo, TimeSpan bloqueo)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= maximo)
        {
            BloqueadoHasta = ahora + bloqueo;
            IntentosFallidos = 0;
        }
    }

    internal void RegistrarInicioCorrecto()
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    public bool EstaBloqueado(DateTimeOffset ahora) => BloqueadoHasta > ahora;

    public bool PendienteDeActivacion => !Activo && ActivadoEn is null;

    public string Estado => Activo ? "Activo" : ActivadoEn is null ? "Pendiente de activación" : "Desactivado";
}
