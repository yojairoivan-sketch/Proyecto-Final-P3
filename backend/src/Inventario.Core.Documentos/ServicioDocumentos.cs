using System.Text.RegularExpressions;
using Inventario.Core.ControlAcceso;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventario.Core.Documentos;

/// <summary>Lo que se muestra de un documento: los datos del registro, nunca el nombre interno del archivo.</summary>
public sealed record DocumentoVista(
    int Id,
    string Nombre,
    string Tipo,
    long TamanoBytes,
    DateTimeOffset SubidoEn,
    int SubidoPorId,
    string SubidoPorNombre,
    DateTimeOffset? EliminadoEn);

/// <summary>Un documento listo para descargar. Quien lo recibe cierra el contenido.</summary>
public sealed record DocumentoDescargado(DocumentoVista Documento, Stream Contenido);

/// <summary>
/// Manejador de documentos (pieza 3 del Core). Todas las operaciones exigen sesión. Quién es el usuario se le pregunta
/// a Control de acceso por <see cref="IUsuarioActual"/>, y la regla «solo el dueño o un Administrador» vive aquí,
/// en el servidor: llamar a la operación directamente también se rechaza (RD-06).
/// </summary>
public interface IServicioDocumentos
{
    /// <summary>RF-DOC-01: sube un archivo y registra nombre, tipo, tamaño, fecha y quién lo subió.</summary>
    Task<Resultado<DocumentoVista>> SubirAsync(string? nombre, string? tipo, Stream? contenido, CancellationToken ct = default);

    /// <summary>
    /// RF-DOC-02: los documentos que el usuario puede ver, los más nuevos primero. Nunca los eliminados ni, para un Estándar,
    /// los de otros. Un Administrador ve los de todos y, si lo pide, también los eliminados.
    /// </summary>
    Task<Resultado<IReadOnlyList<DocumentoVista>>> ListarAsync(bool incluirEliminados = false, CancellationToken ct = default);

    /// <summary>RF-DOC-01: el registro de un documento con todos sus datos. Solo para su dueño o un Administrador.</summary>
    Task<Resultado<DocumentoVista>> ConsultarAsync(int id, CancellationToken ct = default);

    /// <summary>RF-DOC-03: el contenido del documento. Solo su dueño o un Administrador lo descargan.</summary>
    Task<Resultado<DocumentoDescargado>> DescargarAsync(int id, CancellationToken ct = default);

    /// <summary>RF-DOC-04: lo elimina de forma lógica. Deja de listarse, pero el registro y el archivo se conservan.</summary>
    Task<Resultado<DocumentoVista>> EliminarAsync(int id, CancellationToken ct = default);
}

internal sealed partial class ServicioDocumentos(
    DocumentosDbContext db,
    IAlmacenArchivos almacen,
    IUsuarioActual usuarioActual,
    TimeProvider reloj,
    OpcionesDocumentos opciones,
    ILogger<ServicioDocumentos> registro) : IServicioDocumentos
{
    public const int LargoMaximoNombre = 255;
    public const int LargoMaximoTipo = 100;
    public const string TipoGenerico = "application/octet-stream";

    public async Task<Resultado<DocumentoVista>> SubirAsync(string? nombre, string? tipo, Stream? contenido, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return SinSesion<DocumentoVista>();

        // RD-07: un archivo ausente o vacío se rechaza con un mensaje, nunca con una excepción.
        if (contenido is null)
            return Resultado<DocumentoVista>.Falla(TipoFallo.Validacion,
                "Adjunta el archivo que quieres subir (campo «archivo» de un formulario multipart/form-data).");

        var archivo = $"{Guid.NewGuid():N}";
        long tamano;
        try
        {
            // Se copia como mucho un byte más del máximo: así se sabe si se pasó sin leer un archivo enorme entero.
            await using var limitado = new LecturaLimitada(contenido, opciones.TamanoMaximoBytes + 1);
            tamano = await almacen.GuardarAsync(archivo, limitado, ct);
        }
        catch
        {
            almacen.Borrar(archivo);
            throw;
        }

        if (tamano == 0 || tamano > opciones.TamanoMaximoBytes)
        {
            almacen.Borrar(archivo);
            return Resultado<DocumentoVista>.Falla(TipoFallo.Validacion, tamano == 0
                ? "El archivo está vacío. Sube un archivo con contenido."
                : $"El archivo pasa del máximo de {opciones.TamanoMaximoBytes / (1024 * 1024)} MB. No se guardó.");
        }

        var documento = Documento.Registrar(Nombre(nombre), Tipo(tipo), tamano, archivo, usuario, reloj.GetUtcNow());
        db.Documentos.Add(documento);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Sin registro, el archivo no le sirve a nadie: se deshace la subida completa.
            almacen.Borrar(archivo);
            throw;
        }

        return Resultado<DocumentoVista>.Ok(Vista(documento), $"Documento «{documento.Nombre}» subido.");
    }

    public async Task<Resultado<IReadOnlyList<DocumentoVista>>> ListarAsync(bool incluirEliminados = false, CancellationToken ct = default)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return SinSesion<IReadOnlyList<DocumentoVista>>();

        if (incluirEliminados && !usuario.EsAdministrador)
            return Resultado<IReadOnlyList<DocumentoVista>>.Falla(TipoFallo.Prohibido,
                "Solo un Administrador puede ver los documentos eliminados.");

        var consulta = db.Documentos.AsNoTracking();
        if (!incluirEliminados)
            consulta = consulta.Where(d => d.EliminadoEn == null);
        if (!usuario.EsAdministrador)
            consulta = consulta.Where(d => d.SubidoPorId == usuario.Id);

        var documentos = await consulta.OrderByDescending(d => d.Id).ToListAsync(ct);
        return Resultado<IReadOnlyList<DocumentoVista>>.Ok(documentos.Select(Vista).ToList());
    }

    public async Task<Resultado<DocumentoVista>> ConsultarAsync(int id, CancellationToken ct = default)
    {
        var (documento, rechazo) = await BuscarAsync<DocumentoVista>(id, "consultarlo", soloLectura: true, ct);
        return rechazo ?? Resultado<DocumentoVista>.Ok(Vista(documento!));
    }

    public async Task<Resultado<DocumentoDescargado>> DescargarAsync(int id, CancellationToken ct = default)
    {
        var (documento, rechazo) = await BuscarAsync<DocumentoDescargado>(id, "descargarlo", soloLectura: true, ct);
        if (rechazo is not null)
            return rechazo;

        if (almacen.Abrir(documento!.Archivo) is not { } contenido)
        {
            registro.LogError("El archivo del documento {Id} no está en el almacén.", documento.Id);
            return Resultado<DocumentoDescargado>.Falla(TipoFallo.NoEncontrado,
                $"El archivo del documento {id} no está en el almacenamiento. Avisa a un Administrador.");
        }

        return Resultado<DocumentoDescargado>.Ok(new DocumentoDescargado(Vista(documento), contenido));
    }

    public async Task<Resultado<DocumentoVista>> EliminarAsync(int id, CancellationToken ct = default)
    {
        var (documento, rechazo) = await BuscarAsync<DocumentoVista>(id, "eliminarlo", soloLectura: false, ct);
        if (rechazo is not null)
            return rechazo;

        documento!.Eliminar(usuarioActual.Usuario!, reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return Resultado<DocumentoVista>.Ok(Vista(documento),
            $"Documento «{documento.Nombre}» eliminado. Ya no aparece en el listado; su registro se conserva.");
    }

    /// <summary>
    /// El documento que se pide, si existe, no está eliminado y el usuario actual es su dueño o un Administrador.
    /// Si no, el rechazo que corresponde.
    /// </summary>
    private async Task<(Documento? Documento, Resultado<T>? Rechazo)> BuscarAsync<T>(
        int id, string accion, bool soloLectura, CancellationToken ct)
    {
        if (usuarioActual.Usuario is not { } usuario)
            return (null, SinSesion<T>());

        var documentos = soloLectura ? db.Documentos.AsNoTracking() : db.Documentos;
        var documento = await documentos.SingleOrDefaultAsync(d => d.Id == id && d.EliminadoEn == null, ct);
        if (documento is null)
            return (null, Resultado<T>.Falla(TipoFallo.NoEncontrado, $"El documento {id} no existe o fue eliminado."));

        if (!documento.LoPuedeManejar(usuario))
            return (null, Resultado<T>.Falla(TipoFallo.Prohibido,
                $"El documento {id} no es tuyo. Solo quien lo subió o un Administrador puede {accion}."));

        return (documento, null);
    }

    /// <summary>Solo el nombre, sin carpetas (un navegador viejo puede mandar «C:\...\factura.pdf»), sin caracteres de control y recortado.</summary>
    private static string Nombre(string? nombre)
    {
        var limpio = new string((nombre ?? "").Split('/', '\\')[^1].Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (limpio is "" or "." or "..")
            return "documento";
        if (limpio.Length <= LargoMaximoNombre)
            return limpio;

        // Se recorta el final del nombre, pero no la extensión.
        var extension = Path.GetExtension(limpio);
        extension = extension.Length > 20 ? "" : extension;
        return limpio[..(LargoMaximoNombre - extension.Length)] + extension;
    }

    /// <summary>El tipo que mandó el cliente si tiene forma «tipo/subtipo»; si no, el genérico.</summary>
    private static string Tipo(string? tipo)
    {
        var limpio = tipo?.Trim().ToLowerInvariant() ?? "";
        return limpio.Length <= LargoMaximoTipo && FormaDeTipo().IsMatch(limpio) ? limpio : TipoGenerico;
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9!#$&^_.+-]*/[a-z0-9][a-z0-9!#$&^_.+-]*$")]
    private static partial Regex FormaDeTipo();

    private static Resultado<T> SinSesion<T>() =>
        Resultado<T>.Falla(TipoFallo.NoAutenticado, "Necesitas iniciar sesión para usar esta operación.");

    internal static DocumentoVista Vista(Documento d) =>
        new(d.Id, d.Nombre, d.Tipo, d.TamanoBytes, d.SubidoEn, d.SubidoPorId, d.SubidoPorNombre, d.EliminadoEn);

    /// <summary>Lee del contenido original como mucho <c>limite</c> bytes y luego se comporta como si se hubiera acabado.</summary>
    private sealed class LecturaLimitada(Stream original, long limite) : Stream
    {
        private long leidos;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => leidos; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var permitido = (int)Math.Min(buffer.Length, limite - leidos);
            if (permitido <= 0)
                return 0;
            var n = original.Read(buffer[..permitido]);
            leidos += n;
            return n;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var permitido = (int)Math.Min(buffer.Length, limite - leidos);
            if (permitido <= 0)
                return 0;
            var n = await original.ReadAsync(buffer[..permitido], ct);
            leidos += n;
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
