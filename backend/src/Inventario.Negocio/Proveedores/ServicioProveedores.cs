using Inventario.Core.ControlAcceso;
using Inventario.Core.ControlAcceso.Validacion;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Negocio.Proveedores;

public sealed record ProveedorVista(int Id, string Nombre, string? Telefono, string? Correo, DateTimeOffset CreadoEn);

/// <summary>
/// Funcionalidad 1: los proveedores a los que se les compra. Quién puede llamar a cada operación lo decide el host
/// (RF-CA-05); aquí se exige la sesión y se validan los datos.
/// </summary>
public interface IServicioProveedores
{
    Task<Resultado<ProveedorVista>> RegistrarAsync(string? nombre, string? telefono, string? correo, CancellationToken ct = default);

    Task<Resultado<IReadOnlyList<ProveedorVista>>> ListarAsync(CancellationToken ct = default);
}

internal sealed class ServicioProveedores(InventarioDbContext db, IUsuarioActual usuarioActual, TimeProvider reloj) : IServicioProveedores
{
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoTelefono = 30;

    public async Task<Resultado<ProveedorVista>> RegistrarAsync(string? nombre, string? telefono, string? correo, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<ProveedorVista>();

        var (nombreValido, errorNombre) = ValidacionNegocio.Texto(nombre, "el nombre", LargoMaximoNombre);
        var (telefonoValido, errorTelefono) = ValidacionNegocio.TextoOpcional(telefono, "el teléfono", LargoMaximoTelefono);
        if (errorTelefono is null && telefonoValido?.Any(c => !char.IsAsciiDigit(c) && !" +-()".Contains(c)) == true)
            errorTelefono = "El teléfono solo admite números, espacios y los signos + - ( ).";
        var (correoValido, errorCorreo) = string.IsNullOrWhiteSpace(correo) ? (null, null) : ValidacionEntrada.Correo(correo);
        if (ValidacionNegocio.Rechazo<ProveedorVista>(errorNombre, errorTelefono, errorCorreo) is { } rechazo)
            return rechazo;

        var proveedor = Proveedor.Crear(nombreValido!, telefonoValido, correoValido, reloj.GetUtcNow());
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync(ct);
        return Resultado<ProveedorVista>.Ok(Vista(proveedor), $"Proveedor {proveedor.Nombre} registrado.");
    }

    public async Task<Resultado<IReadOnlyList<ProveedorVista>>> ListarAsync(CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is null)
            return Fallos.SinSesion<IReadOnlyList<ProveedorVista>>();

        var proveedores = await db.Proveedores.AsNoTracking().OrderBy(p => p.Nombre).ThenBy(p => p.Id).ToListAsync(ct);
        return Resultado<IReadOnlyList<ProveedorVista>>.Ok(proveedores.Select(Vista).ToList());
    }

    private static ProveedorVista Vista(Proveedor p) => new(p.Id, p.Nombre, p.Telefono, p.Correo, p.CreadoEn);
}
