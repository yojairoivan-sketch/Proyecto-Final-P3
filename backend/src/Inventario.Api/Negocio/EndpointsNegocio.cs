using Inventario.Api.Acceso;
using Inventario.Core.ControlAcceso;
using Inventario.Negocio.Existencias;
using Inventario.Negocio.Ordenes;
using Inventario.Negocio.Productos;
using Inventario.Negocio.Proveedores;

namespace Inventario.Api.Negocio;

/// <summary>
/// Módulo de negocio: proveedores, productos, órdenes de compra y movimientos de existencia. Endpoints delgados:
/// las reglas y la máquina de estados viven en los servicios del negocio; la exigencia de rol de cada uno está en Operaciones.
/// </summary>
public static class EndpointsNegocio
{
    public sealed record NuevoProveedor(string? Nombre, string? Telefono, string? Correo);

    public sealed record NuevoProducto(string? Nombre, string? Sku, string? UnidadMedida, decimal? PuntoReorden);

    public sealed record NuevaOrden(int? ProveedorId);

    public sealed record NuevaLinea(int? ProductoId, decimal? Cantidad, decimal? CostoUnitario);

    public sealed record CambioDeEstado(string? Hacia);

    public sealed record Salida(decimal? Cantidad, string? Motivo);

    public sealed record Ajuste(decimal? ExistenciaContada, string? Motivo);

    public static IEndpointRouteBuilder MapearNegocio(this IEndpointRouteBuilder app)
    {
        var proveedores = app.MapGroup("/api/proveedores");

        proveedores.MapGet("/", async (IServicioProveedores servicio, CancellationToken ct) =>
            (await servicio.ListarAsync(ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarProveedores);

        proveedores.MapPost("/", async (NuevoProveedor nuevo, IServicioProveedores servicio, CancellationToken ct) =>
            Responder("proveedor", await servicio.RegistrarAsync(nuevo.Nombre, nuevo.Telefono, nuevo.Correo, ct), StatusCodes.Status201Created))
            .Requiere(Operaciones.RegistrarProveedor);

        var productos = app.MapGroup("/api/productos");

        productos.MapGet("/", async (IServicioProductos servicio, CancellationToken ct) =>
            (await servicio.ListarAsync(ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarProductos);

        productos.MapPost("/", async (NuevoProducto nuevo, IServicioProductos servicio, CancellationToken ct) =>
            Responder("producto", await servicio.RegistrarAsync(nuevo.Nombre, nuevo.Sku, nuevo.UnidadMedida, nuevo.PuntoReorden, ct),
                StatusCodes.Status201Created))
            .Requiere(Operaciones.RegistrarProducto);

        productos.MapGet("/stock-bajo", async (IServicioProductos servicio, CancellationToken ct) =>
            (await servicio.ListarStockBajoAsync(ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarStockBajo);

        productos.MapGet("/{id:int}/movimientos", async (int id, IServicioExistencias servicio, CancellationToken ct) =>
            (await servicio.ListarMovimientosAsync(id, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarMovimientos);

        productos.MapPost("/{id:int}/salidas", async (int id, Salida salida, IServicioExistencias servicio, CancellationToken ct) =>
            Responder("movimiento", await servicio.RegistrarSalidaAsync(id, salida.Cantidad, salida.Motivo, ct), StatusCodes.Status201Created))
            .Requiere(Operaciones.RegistrarSalida);

        productos.MapPost("/{id:int}/ajuste", async (int id, Ajuste ajuste, IServicioExistencias servicio, CancellationToken ct) =>
            Responder("movimiento", await servicio.AjustarAsync(id, ajuste.ExistenciaContada, ajuste.Motivo, ct), StatusCodes.Status201Created))
            .Requiere(Operaciones.AjustarExistencia);

        var ordenes = app.MapGroup("/api/ordenes");

        ordenes.MapGet("/", async (string? estado, IServicioOrdenes servicio, CancellationToken ct) =>
            (await servicio.ListarAsync(estado, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarOrdenesCompra);

        ordenes.MapPost("/", async (NuevaOrden nueva, IServicioOrdenes servicio, CancellationToken ct) =>
            Responder("orden", await servicio.CrearAsync(nueva.ProveedorId, ct), StatusCodes.Status201Created))
            .Requiere(Operaciones.CrearOrdenCompra);

        ordenes.MapGet("/{id:int}", async (int id, IServicioOrdenes servicio, CancellationToken ct) =>
            (await servicio.ConsultarAsync(id, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarOrdenesCompra);

        ordenes.MapPost("/{id:int}/lineas", async (int id, NuevaLinea linea, IServicioOrdenes servicio, CancellationToken ct) =>
            Responder("orden", await servicio.AgregarLineaAsync(id, linea.ProductoId, linea.Cantidad, linea.CostoUnitario, ct)))
            .Requiere(Operaciones.AgregarLineaOrden);

        ordenes.MapPost("/{id:int}/estado", async (int id, CambioDeEstado cambio, IServicioOrdenes servicio, CancellationToken ct) =>
            Responder("orden", await servicio.CambiarEstadoAsync(id, cambio.Hacia, ct)))
            .Requiere(Operaciones.CambiarEstadoOrden);

        return app;
    }

    /// <summary>Éxito: el mensaje y cómo quedó lo que se tocó, bajo <paramref name="clave"/>. Fallo: el ProblemDetails de siempre.</summary>
    private static IResult Responder<T>(string clave, Resultado<T> resultado, int estadoExito = StatusCodes.Status200OK) =>
        resultado.Exito
            ? Results.Json(new Dictionary<string, object?> { ["mensaje"] = resultado.Mensaje, [clave] = resultado.Valor }, statusCode: estadoExito)
            : resultado.ComoRespuesta();
}
