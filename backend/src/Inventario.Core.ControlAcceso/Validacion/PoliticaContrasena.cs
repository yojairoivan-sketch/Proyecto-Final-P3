namespace Inventario.Core.ControlAcceso.Validacion;

/// <summary>
/// RF-CA-14: política mínima de contraseña. Se aplica en el registro y en todo cambio de contraseña.
/// </summary>
public static class PoliticaContrasena
{
    public const int LargoMinimo = 8;
    public const int LargoMaximo = 128;

    public const string Descripcion = "De 8 a 128 caracteres, con letras y números.";

    /// <summary>Devuelve null si la contraseña cumple, o el mensaje que explica qué falta.</summary>
    public static string? Validar(string? contrasena)
    {
        if (string.IsNullOrEmpty(contrasena))
            return "La contraseña es obligatoria.";
        if (contrasena.Length < LargoMinimo)
            return $"La contraseña debe tener al menos {LargoMinimo} caracteres.";
        if (contrasena.Length > LargoMaximo)
            return $"La contraseña no puede pasar de {LargoMaximo} caracteres.";
        if (!contrasena.Any(char.IsLetter) || !contrasena.Any(char.IsDigit))
            return "La contraseña debe combinar letras y números.";
        return null;
    }
}
