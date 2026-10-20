namespace Inventario.Core.GestionPermisos;

/// <summary>Un permiso que se puede pedir: el nombre de la operación y una frase que dice qué permite.</summary>
public sealed record PermisoSolicitable(string Nombre, string Descripcion);

public sealed class OpcionesGestionPermisos
{
    /// <summary>
    /// El catálogo de permisos que se pueden pedir. Lo arma el host con sus operaciones: así el Core no conoce
    /// las operaciones del módulo de negocio (RD-03).
    /// </summary>
    public IReadOnlyList<PermisoSolicitable> PermisosSolicitables { get; set; } = [];
}
