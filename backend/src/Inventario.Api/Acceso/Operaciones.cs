using Inventario.Core.ControlAcceso;

namespace Inventario.Api.Acceso;

/// <summary>Una operación del sistema y los roles que pueden ejecutarla. Sin roles, es pública.</summary>
public sealed record Operacion(string Nombre, IReadOnlyList<string> Roles)
{
    public bool EsPublica => Roles.Count == 0;
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

    // Solo Administrador.
    public static readonly Operacion ListarUsuarios = new(nameof(ListarUsuarios), SoloAdministrador);
    public static readonly Operacion CambiarRol = new(nameof(CambiarRol), SoloAdministrador);
    public static readonly Operacion DesactivarUsuario = new(nameof(DesactivarUsuario), SoloAdministrador);
    public static readonly Operacion ReactivarUsuario = new(nameof(ReactivarUsuario), SoloAdministrador);
}
