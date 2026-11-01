using Inventario.Core.GestionPermisos;

namespace Inventario.Negocio;

/// <summary>
/// Los permisos del negocio que se pueden pedir a Gestión de permisos. El host los suma al catálogo de la pieza,
/// así el Core sigue sin conocer al negocio (RD-03).
/// </summary>
public static class PermisosNegocio
{
    /// <summary>Corregir la existencia después de un conteo físico. Lo tiene el Administrador por su rol; los demás, si se lo aprueban.</summary>
    public const string AjustarExistencia = nameof(AjustarExistencia);

    public static IReadOnlyList<PermisoSolicitable> Solicitables { get; } =
    [
        new(AjustarExistencia, "Corregir la existencia de un producto después de un conteo físico."),
    ];
}
