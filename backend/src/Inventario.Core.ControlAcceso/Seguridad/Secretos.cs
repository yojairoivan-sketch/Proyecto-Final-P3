using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Inventario.Core.ControlAcceso.Seguridad;

/// <summary>Generación de tokens y hash de contraseñas. Nada de esto sale de la pieza.</summary>
internal static class Secretos
{
    private static readonly PasswordHasher<Usuario> Hasher = new();

    /// <summary>Hash de una contraseña que nadie conoce: sirve para igualar el tiempo de respuesta.</summary>
    private static readonly Lazy<string> HashFicticio = new(() => Hasher.HashPassword(null!, NuevoToken()));

    /// <summary>32 bytes aleatorios en Base64 URL: es lo que viaja en el enlace o en la credencial.</summary>
    public static string NuevoToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Lo que se guarda en la base: SHA-256 del token. Quien lea la tabla no puede usarlo.</summary>
    public static string HashDeToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>RD-05: PBKDF2 con sal aleatoria. Dos usuarios con la misma contraseña tienen hashes distintos.</summary>
    public static string HashDeContrasena(string contrasena) => Hasher.HashPassword(null!, contrasena);

    /// <summary>Compara la contraseña con el hash. Indica además si conviene recalcular el hash.</summary>
    public static (bool Correcta, bool Rehacer) Verificar(string hash, string contrasena)
    {
        var resultado = Hasher.VerifyHashedPassword(null!, hash, contrasena);
        return (resultado != PasswordVerificationResult.Failed, resultado == PasswordVerificationResult.SuccessRehashNeeded);
    }

    /// <summary>Hace el mismo trabajo que una verificación real cuando el correo no existe.</summary>
    public static void VerificarContraHashFicticio(string contrasena) =>
        Hasher.VerifyHashedPassword(null!, HashFicticio.Value, contrasena);
}
