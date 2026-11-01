using Inventario.Core.ControlAcceso;
using Inventario.Core.GestionPermisos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Negocio.Existencias;

public sealed record MovimientoVista(
    long Id,
    int ProductoId,
    TipoMovimiento Tipo,
    decimal Cantidad,
    decimal ExistenciaResultante,
    string Motivo,
    int? OrdenDeCompraId,
    int UsuarioId,
    string UsuarioNombre,
    DateTimeOffset RegistradoEn);

/// <summary>
/// Funcionalidad 5: las salidas de mercancía y el historial de movimientos de un producto, y funcionalidad 6:
/// el ajuste por conteo físico, que depende de un permiso de Gestión de permisos.
/// </summary>
public interface IServicioExistencias
{
    /// <summary>Resta lo que salió (una venta, una merma). La existencia nunca queda negativa.</summary>
    Task<Resultado<MovimientoVista>> RegistrarSalidaAsync(int productoId, decimal? cantidad, string? motivo, CancellationToken ct = default);

    /// <summary>
    /// Deja la existencia en lo que se contó. Lo hace un Administrador, o un usuario a quien un Administrador le aprobó
    /// el permiso <see cref="PermisosNegocio.AjustarExistencia"/> en Gestión de permisos.
    /// </summary>
    Task<Resultado<MovimientoVista>> AjustarAsync(int productoId, decimal? existenciaContada, string? motivo, CancellationToken ct = default);

    /// <summary>Todos los movimientos del producto, del más viejo al más nuevo.</summary>
    Task<Resultado<IReadOnlyList<MovimientoVista>>> ListarMovimientosAsync(int productoId, CancellationToken ct = default);
}

internal sealed class ServicioExistencias(
    InventarioDbContext db,
    IUsuarioActual usuarioActual,
    IPermisosConcedidos permisos,
    TimeProvider reloj) : IServicioExistencias
{
    public async Task<Resultado<MovimientoVista>> RegistrarSalidaAsync(
        int productoId, decimal? cantidad, string? motivo, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return Fallos.SinSesion<MovimientoVista>();

        var errorCantidad = ValidacionNegocio.Numero(cantidad, "la cantidad", ValidacionNegocio.CantidadMaxima, admiteCero: false);
        var (motivoValido, errorMotivo) = ValidacionNegocio.Texto(motivo, "el motivo", MovimientoExistencia.LargoMaximoMotivo);
        if (ValidacionNegocio.Rechazo<MovimientoVista>(errorCantidad, errorMotivo) is { } rechazo)
            return rechazo;

        if (await db.Productos.SingleOrDefaultAsync(p => p.Id == productoId, ct) is not { } producto)
            return NoExiste();
        if (producto.RegistrarSalida(cantidad!.Value, motivoValido!, Responsable.De(usuario), reloj.GetUtcNow()) is { } problema)
            return Resultado<MovimientoVista>.Falla(TipoFallo.Conflicto, problema);

        return await GuardarAsync(producto, $"Salida registrada. Existencia de {producto.Nombre}: {producto.Existencia:0.##}.", ct);
    }

    public async Task<Resultado<MovimientoVista>> AjustarAsync(
        int productoId, decimal? existenciaContada, string? motivo, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return Fallos.SinSesion<MovimientoVista>();

        // Un Administrador ajusta por su rol; cualquier otro usuario, solo si Gestión de permisos dice que se lo aprobaron.
        if (!usuario.EsAdministrador && !await permisos.EstaConcedidoAsync(usuario.Id, PermisosNegocio.AjustarExistencia, ct))
            return Resultado<MovimientoVista>.Falla(TipoFallo.Prohibido, SinPermisoDeAjuste(usuario.Rol));

        var errorContada = ValidacionNegocio.Numero(existenciaContada, "la existencia contada", ValidacionNegocio.CantidadMaxima, admiteCero: true);
        var (motivoValido, errorMotivo) = ValidacionNegocio.Texto(motivo, "el motivo", MovimientoExistencia.LargoMaximoMotivo);
        if (ValidacionNegocio.Rechazo<MovimientoVista>(errorContada, errorMotivo) is { } rechazo)
            return rechazo;

        if (await db.Productos.SingleOrDefaultAsync(p => p.Id == productoId, ct) is not { } producto)
            return NoExiste();
        if (producto.Ajustar(existenciaContada!.Value, motivoValido!, Responsable.De(usuario), reloj.GetUtcNow()) is { } problema)
            return Resultado<MovimientoVista>.Falla(TipoFallo.Conflicto, problema);

        return await GuardarAsync(producto, $"Existencia ajustada. Existencia de {producto.Nombre}: {producto.Existencia:0.##}.", ct);
    }

    public async Task<Resultado<IReadOnlyList<MovimientoVista>>> ListarMovimientosAsync(int productoId, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<IReadOnlyList<MovimientoVista>>();

        if (!await db.Productos.AnyAsync(p => p.Id == productoId, ct))
            return Fallos.NoExiste<IReadOnlyList<MovimientoVista>>("El producto");

        var movimientos = await db.Movimientos.AsNoTracking()
            .Where(m => m.ProductoId == productoId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);
        return Resultado<IReadOnlyList<MovimientoVista>>.Ok(movimientos.Select(Vista).ToList());
    }

    public static string SinPermisoDeAjuste(string rol) =>
        $"Ajustar la existencia es solo para: Administrador. Tu rol es {rol}. " +
        $"Puedes pedir el permiso {PermisosNegocio.AjustarExistencia} con una solicitud; cuando un Administrador la apruebe, podrás hacerlo.";

    private async Task<Resultado<MovimientoVista>> GuardarAsync(Productos.Producto producto, string mensaje, CancellationToken ct) =>
        await Fallos.GuardarAsync<MovimientoVista>(db, ct) ?? Resultado<MovimientoVista>.Ok(Vista(producto.Movimientos[^1]), mensaje);

    private static Resultado<MovimientoVista> NoExiste() => Fallos.NoExiste<MovimientoVista>("El producto");

    private static MovimientoVista Vista(MovimientoExistencia m) => new(
        m.Id, m.ProductoId, m.Tipo, m.Cantidad, m.ExistenciaResultante, m.Motivo, m.OrdenDeCompraId, m.UsuarioId, m.UsuarioNombre, m.RegistradoEn);
}
