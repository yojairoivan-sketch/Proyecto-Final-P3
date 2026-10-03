using Inventario.Core.ColaCorreos;
using Inventario.Core.ControlAcceso.Seguridad;
using Inventario.Core.ControlAcceso.Validacion;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventario.Core.ControlAcceso.Cuentas;

/// <summary>Registro de usuarios y activación de cuentas.</summary>
public interface IServicioCuentas
{
    /// <summary>RF-CA-01, 02, 14 y 15: crea la cuenta inactiva y encola el enlace de activación.</summary>
    Task<Resultado> RegistrarAsync(string? nombre, string? correo, string? contrasena, CancellationToken ct = default);
}

internal sealed class ServicioCuentas(
    ControlAccesoDbContext db,
    IColaCorreos cola,
    TimeProvider reloj,
    OpcionesControlAcceso opciones) : IServicioCuentas
{
    public async Task<Resultado> RegistrarAsync(string? nombre, string? correo, string? contrasena, CancellationToken ct = default)
    {
        var (nombreValido, errorNombre) = ValidacionEntrada.Nombre(nombre);
        var (correoValido, errorCorreo) = ValidacionEntrada.Correo(correo);
        var errorContrasena = PoliticaContrasena.Validar(contrasena);

        var errores = new[] { errorNombre, errorCorreo, errorContrasena }.Where(e => e is not null).ToArray();
        if (errores.Length > 0)
            return Resultado.Falla(TipoFallo.Validacion, string.Join(" ", errores));

        if (await db.Usuarios.AnyAsync(u => u.Correo == correoValido, ct))
            return CorreoDuplicado();

        var ahora = reloj.GetUtcNow();
        var usuario = Usuario.Registrar(nombreValido!, correoValido!, ahora);
        usuario.CambiarHash(Secretos.HashDeContrasena(contrasena!));
        db.Usuarios.Add(usuario);

        var token = Secretos.NuevoToken();
        var activacion = TokenActivacion.Emitir(usuario, Secretos.HashDeToken(token), ahora, opciones.VigenciaActivacion);
        db.TokensActivacion.Add(activacion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos registros simultáneos con el mismo correo: el índice único decide.
            return CorreoDuplicado();
        }

        await EncolarActivacionAsync(usuario, token, activacion.VenceEn, ct);
        return Resultado.Ok("Cuenta creada. Te enviamos un correo con el enlace para activarla.");
    }

    private async Task EncolarActivacionAsync(Usuario usuario, string token, DateTimeOffset venceEn, CancellationToken ct)
    {
        var enlace = Correos.Enlace(opciones, "activar", "token", token);
        var (asunto, cuerpo) = Correos.Activacion(usuario.Nombre, enlace, venceEn);
        await cola.EncolarAsync(usuario.Correo, asunto, cuerpo, ct);
    }

    private static Resultado CorreoDuplicado() =>
        Resultado.Falla(TipoFallo.Conflicto, "Ya existe una cuenta con ese correo.");
}
