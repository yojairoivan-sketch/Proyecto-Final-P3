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
