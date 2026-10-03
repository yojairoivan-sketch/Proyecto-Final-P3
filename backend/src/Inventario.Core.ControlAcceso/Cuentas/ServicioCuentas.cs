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

    /// <summary>RF-CA-16: consume el enlace (un solo uso, con vencimiento) y activa la cuenta.</summary>
    Task<Resultado> ActivarAsync(string? token, CancellationToken ct = default);

    /// <summary>RF-CA-17: invalida el enlace anterior y encola uno nuevo. Responde igual exista o no el correo.</summary>
    Task<Resultado> ReenviarActivacionAsync(string? correo, CancellationToken ct = default);
}

internal sealed class ServicioCuentas(
    ControlAccesoDbContext db,
    IColaCorreos cola,
    TimeProvider reloj,
    OpcionesControlAcceso opciones) : IServicioCuentas
{
    public const string RespuestaReenvio =
        "Si el correo corresponde a una cuenta pendiente de activación, te enviamos un enlace nuevo.";

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

    public async Task<Resultado> ActivarAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Resultado.Falla(TipoFallo.Validacion, "Falta el token de activación.");

        var hash = Secretos.HashDeToken(token.Trim());
        var activacion = await db.TokensActivacion.Include(t => t.Usuario).SingleOrDefaultAsync(t => t.HashCodigo == hash, ct);
        var ahora = reloj.GetUtcNow();

        if (activacion is null)
            return Resultado.Falla(TipoFallo.Validacion, "El enlace de activación no es válido.");
        if (activacion.Usado)
            return EnlaceUsado();
        if (activacion.InvalidadoEn is not null)
            return Resultado.Falla(TipoFallo.Validacion,
                "Este enlace ya no sirve porque se pidió uno nuevo. Usa el enlace del correo más reciente.");
        if (ahora >= activacion.VenceEn)
            return Resultado.Falla(TipoFallo.Validacion, "Este enlace de activación venció. Pide uno nuevo.");

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        // Se consume con una actualización condicional: si dos peticiones llegan a la vez, solo una gana.
        var consumidos = await db.TokensActivacion
            .Where(t => t.Id == activacion.Id && !t.Usado && t.InvalidadoEn == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.Usado, true).SetProperty(x => x.UsadoEn, ahora), ct);
        if (consumidos == 0)
            return EnlaceUsado();

        activacion.Usuario.Activar(ahora);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return Resultado.Ok("Cuenta activada. Ya puedes iniciar sesión.");
    }

    public async Task<Resultado> ReenviarActivacionAsync(string? correo, CancellationToken ct = default)
    {
        var (correoValido, error) = ValidacionEntrada.Correo(correo);
        if (error is not null)
            return Resultado.Falla(TipoFallo.Validacion, error);

        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Correo == correoValido, ct);
        if (usuario is { PendienteDeActivacion: true })
        {
            var ahora = reloj.GetUtcNow();
            await db.TokensActivacion
                .Where(t => t.UsuarioId == usuario.Id && !t.Usado && t.InvalidadoEn == null)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.InvalidadoEn, ahora), ct);

            var token = Secretos.NuevoToken();
            var activacion = TokenActivacion.Emitir(usuario, Secretos.HashDeToken(token), ahora, opciones.VigenciaActivacion);
            db.TokensActivacion.Add(activacion);
            await db.SaveChangesAsync(ct);
            await EncolarActivacionAsync(usuario, token, activacion.VenceEn, ct);
        }

        // Misma respuesta en todos los casos: no revela qué correos están registrados.
        return Resultado.Ok(RespuestaReenvio);
    }

    private async Task EncolarActivacionAsync(Usuario usuario, string token, DateTimeOffset venceEn, CancellationToken ct)
    {
        var enlace = Correos.Enlace(opciones, "activar", "token", token);
        var (asunto, cuerpo) = Correos.Activacion(usuario.Nombre, enlace, venceEn);
        await cola.EncolarAsync(usuario.Correo, asunto, cuerpo, ct);
    }

    private static Resultado CorreoDuplicado() =>
        Resultado.Falla(TipoFallo.Conflicto, "Ya existe una cuenta con ese correo.");

    private static Resultado EnlaceUsado() =>
        Resultado.Falla(TipoFallo.Validacion, "Este enlace de activación ya se usó. Si ya activaste tu cuenta, inicia sesión.");
}
