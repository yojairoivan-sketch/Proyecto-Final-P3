using System.Globalization;
using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Ordenes;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Negocio.Cotizaciones;

/// <summary>Una orden de compra valorada en otra moneda con la tasa del día.</summary>
public sealed record CotizacionVista(
    int OrdenId,
    string Proveedor,
    EstadoOrdenCompra Estado,
    string MonedaOrden,
    decimal Total,
    string Moneda,
    decimal Tasa,
    decimal TotalConvertido,
    DateTimeOffset TasaActualizadaEn,
    string Fuente);

/// <summary>
/// Funcionalidad 8: cotizar una orden de compra en otra moneda, por ejemplo para pagarle a un proveedor del exterior en dólares.
/// Los costos del inventario se registran en pesos dominicanos (DOP); la tasa sale de una API externa (<see cref="ITasasDeCambio"/>).
/// Si la API no está configurada o no responde, la cotización se rechaza con un mensaje y lo demás sigue funcionando.
/// </summary>
public interface IServicioCotizaciones
{
    Task<Resultado<CotizacionVista>> CotizarOrdenAsync(int ordenId, string? moneda, CancellationToken ct = default);
}

internal sealed class ServicioCotizaciones(InventarioDbContext db, IUsuarioActual usuarioActual, ITasasDeCambio tasas) : IServicioCotizaciones
{
    /// <summary>La moneda en que se registran los costos del inventario.</summary>
    public const string MonedaLocal = "DOP";

    public async Task<Resultado<CotizacionVista>> CotizarOrdenAsync(int ordenId, string? moneda, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<CotizacionVista>();

        var codigo = moneda?.Trim().ToUpperInvariant() ?? "";
        if (codigo.Length != 3 || !codigo.All(char.IsAsciiLetterUpper))
            return Resultado<CotizacionVista>.Falla(TipoFallo.Validacion,
                "Indica la moneda con su código de tres letras (moneda), por ejemplo USD o EUR.");
        if (codigo == MonedaLocal)
            return Resultado<CotizacionVista>.Falla(TipoFallo.Validacion,
                $"Los costos de la orden ya están en {MonedaLocal}. Elige otra moneda, por ejemplo USD o EUR.");

        var orden = await db.OrdenesDeCompra.AsNoTracking()
            .Include(o => o.Proveedor)
            .Include(o => o.Lineas)
            .SingleOrDefaultAsync(o => o.Id == ordenId, ct);
        if (orden is null)
            return Fallos.NoExiste<CotizacionVista>("La orden de compra");

        // Lo único que viene de afuera es la tasa; el total y la cuenta son del negocio.
        var tasa = await tasas.ObtenerAsync(MonedaLocal, codigo, ct);
        if (!tasa.Exito)
            return Fallos.Como<CotizacionVista>(tasa);

        var total = orden.Total;
        var convertido = Math.Round(total * tasa.Valor!.Valor, 2, MidpointRounding.AwayFromZero);
        var cotizacion = new CotizacionVista(orden.Id, orden.Proveedor.Nombre, orden.Estado, MonedaLocal, total,
            codigo, tasa.Valor.Valor, convertido, tasa.Valor.ActualizadaEn, tasas.Fuente);

        return Resultado<CotizacionVista>.Ok(cotizacion, string.Create(CultureInfo.InvariantCulture,
            $"La orden {orden.Id} suma {total:#,0.00} {MonedaLocal}, unos {convertido:#,0.00} {codigo} con la tasa de {tasas.Fuente}."));
    }
}
