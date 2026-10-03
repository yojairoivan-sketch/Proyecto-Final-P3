using Inventario.Core.ColaCorreos;
using Inventario.Core.ControlAcceso.Seguridad;
using Inventario.Core.ControlAcceso.Validacion;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.ControlAcceso.Contrasenas;

/// <summary>Recuperación, restablecimiento y cambio de contraseña.</summary>
public interface IServicioContrasenas
{
    /// <summary>
    /// RF-CA-09 y RF-CA-10: si el correo es de una cuenta activa, emite un código de un solo uso que vence
    /// en 30 minutos y lo encola. La respuesta es la misma exista o no el correo.
    /// </summary>
    Task<Resultado> SolicitarRecuperacionAsync(string? correo, CancellationToken ct = default);

    /// <summary>
    /// RF-CA-11: con un código válido define la contraseña nueva (guardada con hash). Un código usado,
    /// vencido o reemplazado se rechaza y la contraseña no cambia.
    /// </summary>
    Task<Resultado> RestablecerAsync(string? codigo, string? contrasenaNueva, CancellationToken ct = default);
}

internal sealed class ServicioContrasenas(
    ControlAccesoDbContext db,
    IColaCorreos cola,
    TimeProvider reloj,
    OpcionesControlAcceso opciones) : IServicioContrasenas
{
    public const string RespuestaRecuperacion =
        "Si el correo corresponde a una cuenta activa, te enviamos un código para definir una contraseña nueva.";

    public async Task<Resultado> SolicitarRecuperacionAsync(string? correo, CancellationToken ct = default)
    {
        var (correoValido, error) = ValidacionEntrada.Correo(correo);
        if (error is not null)
            return Resultado.Falla(TipoFallo.Validacion, error);

        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Correo == correoValido, ct);
        if (usuario is { Activo: true })
        {
            var (codigo, venceEn) = await EmitirCodigoAsync(usuario, ct);
            var enlace = Correos.Enlace(opciones, "restablecer", "codigo", codigo);
            var (asunto, cuerpo) = Correos.Recuperacion(usuario.Nombre, enlace, codigo, venceEn);
            await cola.EncolarAsync(usuario.Correo, asunto, cuerpo, ct);
        }

        // Misma respuesta en todos los casos: el flujo no revela qué correos están registrados.
        return Resultado.Ok(RespuestaRecuperacion);
    }

    public async Task<Resultado> RestablecerAsync(string? codigo, string? contrasenaNueva, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Resultado.Falla(TipoFallo.Validacion, "Falta el código de recuperación.");

        var hash = Secretos.HashDeToken(codigo.Trim());
        var registro = await db.CodigosRecuperacion.Include(c => c.Usuario).SingleOrDefaultAsync(c => c.HashCodigo == hash, ct);
        var ahora = reloj.GetUtcNow();

        if (registro is null)
            return Resultado.Falla(TipoFallo.Validacion, "El código de recuperación no es válido.");
        if (registro.Usado)
            return CodigoUsado();
        if (registro.InvalidadoEn is not null)
            return Resultado.Falla(TipoFallo.Validacion,
                "Este código ya no sirve porque se pidió uno nuevo. Usa el del correo más reciente.");
        if (ahora >= registro.VenceEn)
            return Resultado.Falla(TipoFallo.Validacion, "El código de recuperación venció. Pide uno nuevo.");

        // RF-CA-14 también aplica aquí. Si falla, el código no se gasta.
        if (PoliticaContrasena.Validar(contrasenaNueva) is { } error)
            return Resultado.Falla(TipoFallo.Validacion, error);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var consumidos = await db.CodigosRecuperacion
            .Where(c => c.Id == registro.Id && !c.Usado && c.InvalidadoEn == null)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.Usado, true).SetProperty(x => x.UsadoEn, ahora), ct);
        if (consumidos == 0)
            return CodigoUsado();

        var usuario = registro.Usuario;
        usuario.CambiarHash(Secretos.HashDeContrasena(contrasenaNueva!));
        usuario.RegistrarInicioCorrecto();
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return Resultado.Ok("Contraseña cambiada. Inicia sesión con la nueva.");
    }

    private static Resultado CodigoUsado() =>
        Resultado.Falla(TipoFallo.Validacion, "Este código de recuperación ya se usó. Pide uno nuevo si lo necesitas.");

    /// <summary>Invalida los códigos anteriores sin usar y emite uno nuevo.</summary>
    private async Task<(string Codigo, DateTimeOffset VenceEn)> EmitirCodigoAsync(Usuario usuario, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await db.CodigosRecuperacion
            .Where(c => c.UsuarioId == usuario.Id && !c.Usado && c.InvalidadoEn == null)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.InvalidadoEn, ahora), ct);

        var codigo = Secretos.NuevoToken();
        var registro = CodigoRecuperacion.Emitir(usuario, Secretos.HashDeToken(codigo), ahora, opciones.VigenciaCodigoRecuperacion);
        db.CodigosRecuperacion.Add(registro);
        await db.SaveChangesAsync(ct);
        return (codigo, registro.VenceEn);
    }
}
