namespace Inventario.Core.Documentos;

/// <summary>
/// Lo que el Manejador de documentos deja en el historial de auditoría (RF-DOC-06, RF-AUD-03): cada subida y cada eliminación.
/// Las acciones se llaman como las operaciones del host que las provocan.
/// </summary>
public static class AuditoriaDocumentos
{
    /// <summary>La entidad de estos registros. El identificador es el id del documento.</summary>
    public const string Entidad = "Documento";

    public const string SubirDocumento = "SubirDocumento";
    public const string EliminarDocumento = "EliminarDocumento";

    internal const string Vigente = "Vigente";
    internal const string Eliminado = "Eliminado";
}
