namespace Inventario.Core.Documentos;

/// <summary>Dónde vive el contenido de los documentos. La pieza solo guarda, abre y borra por el nombre que ella generó.</summary>
public interface IAlmacenArchivos
{
    /// <summary>Copia el contenido a un archivo nuevo y devuelve cuántos bytes se escribieron.</summary>
    Task<long> GuardarAsync(string archivo, Stream contenido, CancellationToken ct = default);

    /// <summary>El contenido para leerlo, o null si el archivo no está.</summary>
    Stream? Abrir(string archivo);

    bool Existe(string archivo);

    /// <summary>Solo para deshacer una subida que no llegó a registrarse. La eliminación de un documento es lógica y no lo llama.</summary>
    void Borrar(string archivo);
}

/// <summary>
/// Guarda cada documento como un archivo dentro de una carpeta. En el contenedor esa carpeta es el volumen con nombre
/// «documentos» (RF-DOC-05): los archivos sobreviven a borrar y volver a crear el contenedor.
/// </summary>
internal sealed class AlmacenEnDisco : IAlmacenArchivos
{
    private readonly string carpeta;

    public AlmacenEnDisco(OpcionesDocumentos opciones)
    {
        carpeta = Path.GetFullPath(opciones.Carpeta);
        Directory.CreateDirectory(carpeta);
    }

    public async Task<long> GuardarAsync(string archivo, Stream contenido, CancellationToken ct = default)
    {
        await using var destino = new FileStream(Ruta(archivo), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await contenido.CopyToAsync(destino, ct);
        return destino.Length;
    }

    public Stream? Abrir(string archivo) =>
        Existe(archivo) ? new FileStream(Ruta(archivo), FileMode.Open, FileAccess.Read, FileShare.Read) : null;

    public bool Existe(string archivo) => File.Exists(Ruta(archivo));

    public void Borrar(string archivo)
    {
        if (Existe(archivo))
            File.Delete(Ruta(archivo));
    }

    /// <summary>El nombre lo genera la pieza; aun así se comprueba que no salga de la carpeta.</summary>
    private string Ruta(string archivo)
    {
        var ruta = Path.GetFullPath(Path.Combine(carpeta, archivo));
        return Path.GetDirectoryName(ruta) == carpeta.TrimEnd(Path.DirectorySeparatorChar)
            ? ruta
            : throw new InvalidOperationException("El nombre del archivo en el almacén no es válido.");
    }
}
