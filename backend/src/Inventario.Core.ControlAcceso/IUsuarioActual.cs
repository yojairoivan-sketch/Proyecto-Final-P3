namespace Inventario.Core.ControlAcceso;

/// <summary>Quién hizo la petición. No incluye hashes ni tokens.</summary>
public sealed record UsuarioAutenticado(int Id, string Nombre, string Correo, string Rol, long SesionId)
{
    public bool EsAdministrador => Rol == ControlAcceso.Rol.Administrador;
}

