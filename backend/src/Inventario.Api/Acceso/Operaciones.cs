using System.Reflection;
using System.Security.Claims;
using Inventario.Core.ControlAcceso;

namespace Inventario.Api.Acceso;

/// <summary>Una operación del sistema y los roles que pueden ejecutarla. Sin roles, es pública.</summary>
/// <param name="Solicitable">
/// Si no es null, la operación se puede pedir con una solicitud de permiso, y este texto dice qué permite.
/// Aprobada la solicitud, el solicitante la ejecuta aunque su rol no alcance (RF-GP-05).
/// </param>
public sealed record Operacion(string Nombre, IReadOnlyList<string> Roles, string? Solicitable = null)
{
    public bool EsPublica => Roles.Count == 0;

    public bool EsSolicitable => Solicitable is not null;

    /// <summary>La ejecuta quien tiene uno de sus roles o, si es solicitable, quien recibió el permiso.</summary>
    public bool LaPuedeEjecutar(ClaimsPrincipal usuario) =>
        Roles.Any(usuario.IsInRole) || (EsSolicitable && usuario.HasClaim(AutenticacionSesion.ClaimPermiso, Nombre));
}

/// <summary>
/// RF-CA-05: el único lugar donde se declara qué rol exige cada operación.
/// Cada endpoint dice cuál es con <c>.Requiere(Operaciones.X)</c>; si alguno no lo dice, la API no arranca.
/// La tabla vive en el host y no en el Core para que también pueda listar operaciones del módulo de negocio
/// sin que el Core dependa de él (RD-03).
/// </summary>
public static class Operaciones
{
    private static readonly string[] Publica = [];
    private static readonly string[] ConSesion = [Rol.Administrador, Rol.Estandar];
    private static readonly string[] SoloAdministrador = [Rol.Administrador];

    // Públicas: no piden sesión.
    public static readonly Operacion ConsultarSalud = new(nameof(ConsultarSalud), Publica);
    public static readonly Operacion Registrarse = new(nameof(Registrarse), Publica);
    public static readonly Operacion ActivarCuenta = new(nameof(ActivarCuenta), Publica);
    public static readonly Operacion ReenviarActivacion = new(nameof(ReenviarActivacion), Publica);
    public static readonly Operacion IniciarSesion = new(nameof(IniciarSesion), Publica);
    public static readonly Operacion SolicitarRecuperacion = new(nameof(SolicitarRecuperacion), Publica);
    public static readonly Operacion RestablecerContrasena = new(nameof(RestablecerContrasena), Publica);

    // Cualquier usuario con sesión, sea Administrador o Estándar.
    public static readonly Operacion ConsultarMiCuenta = new(nameof(ConsultarMiCuenta), ConSesion);
    public static readonly Operacion CerrarSesion = new(nameof(CerrarSesion), ConSesion);
    public static readonly Operacion CambiarMiContrasena = new(nameof(CambiarMiContrasena), ConSesion);
    public static readonly Operacion ConsultarPermisosSolicitables = new(nameof(ConsultarPermisosSolicitables), ConSesion);
    public static readonly Operacion CrearSolicitudPermiso = new(nameof(CrearSolicitudPermiso), ConSesion);
    public static readonly Operacion ConsultarMisSolicitudes = new(nameof(ConsultarMisSolicitudes), ConSesion);
    public static readonly Operacion ConsultarSolicitud = new(nameof(ConsultarSolicitud), ConSesion);

    // Solo Administrador. Las que dicen qué permiten se pueden pedir con una solicitud de permiso.
    // CambiarRol y las de solicitudes no: un Estándar nunca cambia roles (RF-CA-08) ni ve solicitudes ajenas (RF-GP-02).
    // DesactivarUsuario tampoco, porque podría dejar el sistema sin Administradores activos.
    public static readonly Operacion ListarUsuarios = new(nameof(ListarUsuarios), SoloAdministrador,
        Solicitable: "Ver la lista de usuarios con su rol y su estado.");
    public static readonly Operacion CambiarRol = new(nameof(CambiarRol), SoloAdministrador);
    public static readonly Operacion DesactivarUsuario = new(nameof(DesactivarUsuario), SoloAdministrador);
    public static readonly Operacion ReactivarUsuario = new(nameof(ReactivarUsuario), SoloAdministrador,
        Solicitable: "Devolverle el acceso a un usuario desactivado.");
    public static readonly Operacion ForzarRestablecimiento = new(nameof(ForzarRestablecimiento), SoloAdministrador);
    public static readonly Operacion ConsultarSolicitudesPendientes = new(nameof(ConsultarSolicitudesPendientes), SoloAdministrador);
    public static readonly Operacion AprobarSolicitud = new(nameof(AprobarSolicitud), SoloAdministrador);
    public static readonly Operacion RechazarSolicitud = new(nameof(RechazarSolicitud), SoloAdministrador);

    /// <summary>Todas las operaciones declaradas arriba.</summary>
    public static IReadOnlyList<Operacion> Todas { get; } = typeof(Operaciones)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(campo => campo.FieldType == typeof(Operacion))
        .Select(campo => (Operacion)campo.GetValue(null)!)
        .ToList();

    /// <summary>El catálogo que recibe Gestión de permisos: las operaciones que se pueden pedir.</summary>
    public static IReadOnlyList<Operacion> Solicitables { get; } = Todas.Where(o => o.EsSolicitable).ToList();
}
