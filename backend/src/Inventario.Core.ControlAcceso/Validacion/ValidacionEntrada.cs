using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Inventario.Core.ControlAcceso.Validacion;

/// <summary>
/// RD-07: toda entrada externa se valida antes de usarse. Un dato vacío o mal formado
/// produce un mensaje controlado, nunca una excepción.
/// </summary>
public static partial class ValidacionEntrada
{
    public const int LargoMaximoNombre = 100;
    public const int LargoMaximoCorreo = 254;

    /// <summary>Devuelve el correo normalizado (sin espacios y en minúsculas) o un mensaje de error.</summary>
    public static (string? Correo, string? Error) Correo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return (null, "El correo es obligatorio.");

        var correo = valor.Trim().ToLowerInvariant();
        var valido = correo.Length <= LargoMaximoCorreo
            && FormaDeCorreo().IsMatch(correo)
            && MailAddress.TryCreate(correo, out var direccion)
            && direccion.Address == correo;

        return valido ? (correo, null) : (null, "El correo no tiene un formato válido.");
    }

    public static (string? Nombre, string? Error) Nombre(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return (null, "El nombre es obligatorio.");

        var nombre = valor.Trim();
        return nombre.Length > LargoMaximoNombre
            ? (null, $"El nombre no puede pasar de {LargoMaximoNombre} caracteres.")
            : (nombre, null);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s.]{2,}$")]
    private static partial Regex FormaDeCorreo();
}
