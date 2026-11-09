namespace Inventario.Core.Documentos;

public sealed class OpcionesDocumentos
{
    public const long DiezMegas = 10 * 1024 * 1024;

    /// <summary>Carpeta donde se guardan los archivos. En compose es el volumen «documentos» (variable DOCUMENTOS_CARPETA).</summary>
    public string Carpeta { get; set; } = "documentos";

    /// <summary>Tamaño máximo de un documento. Uno más grande se rechaza sin guardar nada.</summary>
    public long TamanoMaximoBytes { get; set; } = DiezMegas;
}
