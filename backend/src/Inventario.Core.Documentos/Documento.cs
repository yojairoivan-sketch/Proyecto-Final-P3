using Inventario.Core.ControlAcceso;

namespace Inventario.Core.Documentos;

/// <summary>
/// Entidad Documento de los Requerimientos del Core: un archivo subido por un usuario. El registro guarda los datos
/// del archivo y quién lo subió; el contenido vive en el almacén de archivos (un volumen, fuera de la imagen: RF-DOC-05).
/// </summary>
public sealed class Documento
{
    public int Id { get; private set; }

    /// <summary>Nombre del archivo tal como lo subió el usuario, sin carpetas.</summary>
    public string Nombre { get; private set; } = "";

    /// <summary>Tipo de contenido (MIME), por ejemplo application/pdf.</summary>
    public string Tipo { get; private set; } = "";

    public long TamanoBytes { get; private set; }

    public DateTimeOffset SubidoEn { get; private set; }

    /// <summary>Quién lo subió: su id en Control de acceso. El nombre se copia para mostrarlo sin leer sus tablas.</summary>
    public int SubidoPorId { get; private set; }
    public string SubidoPorNombre { get; private set; } = "";

    /// <summary>
    /// Nombre con el que se guardó en el almacén. Lo genera el sistema (un GUID), nunca sale del nombre que mandó el usuario:
    /// así nadie puede escribir ni leer fuera de la carpeta de documentos.
    /// </summary>
    public string Archivo { get; private set; } = "";

    /// <summary>RF-DOC-04: la eliminación es lógica. El registro y el archivo se conservan; solo se marca cuándo y quién.</summary>
    public DateTimeOffset? EliminadoEn { get; private set; }
    public int? EliminadoPorId { get; private set; }

    public bool Eliminado => EliminadoEn is not null;

    private Documento()
    {
    }

    internal static Documento Registrar(
        string nombre, string tipo, long tamanoBytes, string archivo, UsuarioAutenticado quien, DateTimeOffset ahora) => new()
    {
        Nombre = nombre,
        Tipo = tipo,
        TamanoBytes = tamanoBytes,
        Archivo = archivo,
        SubidoPorId = quien.Id,
        SubidoPorNombre = quien.Nombre,
        SubidoEn = ahora,
    };

    /// <summary>Solo su dueño o un Administrador lo descargan o lo eliminan (RF-DOC-03, RD-06).</summary>
    internal bool LoPuedeManejar(UsuarioAutenticado usuario) => usuario.EsAdministrador || SubidoPorId == usuario.Id;

    internal void Eliminar(UsuarioAutenticado quien, DateTimeOffset ahora)
    {
        EliminadoEn = ahora;
        EliminadoPorId = quien.Id;
    }
}
