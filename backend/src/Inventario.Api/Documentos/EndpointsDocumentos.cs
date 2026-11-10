using Inventario.Api.Acceso;
using Inventario.Core.ControlAcceso;
using Inventario.Core.Documentos;

namespace Inventario.Api.Documentos;

/// <summary>
/// Manejador de documentos. Endpoints delgados: la regla «solo el dueño o un Administrador» y las validaciones del archivo
/// viven en <see cref="IServicioDocumentos"/>; aquí solo se lee el formulario y se traduce el resultado a HTTP.
/// </summary>
public static class EndpointsDocumentos
{
    /// <summary>Lo que se acepta de más sobre el máximo del archivo: los encabezados del formulario multipart.</summary>
    private const long MargenFormulario = 64 * 1024;

    public static IEndpointRouteBuilder MapearDocumentos(this IEndpointRouteBuilder app)
    {
        var documentos = app.MapGroup("/api/documentos");

        documentos.MapPost("/", Subir).Requiere(Operaciones.SubirDocumento);

        documentos.MapGet("/", async (bool? eliminados, IServicioDocumentos servicio, CancellationToken ct) =>
            (await servicio.ListarAsync(eliminados ?? false, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarDocumentos);

        documentos.MapGet("/{id:int}", async (int id, IServicioDocumentos servicio, CancellationToken ct) =>
            (await servicio.ConsultarAsync(id, ct)).ComoRespuesta())
            .Requiere(Operaciones.ConsultarDocumentos);

        documentos.MapGet("/{id:int}/contenido", Descargar).Requiere(Operaciones.DescargarDocumento);

        documentos.MapDelete("/{id:int}", async (int id, IServicioDocumentos servicio, CancellationToken ct) =>
            Responder(await servicio.EliminarAsync(id, ct)))
            .Requiere(Operaciones.EliminarDocumento);

        return app;
    }

    /// <summary>
    /// Recibe el archivo en un formulario multipart/form-data, en el campo «archivo» (si el formulario trae un solo archivo,
    /// se acepta con cualquier nombre de campo). Lo que no es un formulario o viene roto se rechaza con 400, nunca con 500.
    /// </summary>
    private static async Task<IResult> Subir(HttpRequest peticion, IServicioDocumentos servicio, OpcionesDocumentos opciones, CancellationToken ct)
    {
        if (peticion.ContentLength > opciones.TamanoMaximoBytes + MargenFormulario)
            return Problema(StatusCodes.Status413PayloadTooLarge,
                $"El archivo pasa del máximo de {opciones.TamanoMaximoBytes / (1024 * 1024)} MB. No se guardó.");

        if (!peticion.HasFormContentType)
            return Problema(StatusCodes.Status400BadRequest,
                "Envía el archivo en un formulario multipart/form-data, en el campo «archivo».");

        IFormCollection formulario;
        try
        {
            formulario = await peticion.ReadFormAsync(ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Problema(StatusCodes.Status413PayloadTooLarge,
                $"El archivo pasa del máximo de {opciones.TamanoMaximoBytes / (1024 * 1024)} MB. No se guardó.");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return Problema(StatusCodes.Status400BadRequest,
                "El formulario no se pudo leer. Envía el archivo en un formulario multipart/form-data, en el campo «archivo».");
        }

        var archivo = formulario.Files.GetFile("archivo") ?? (formulario.Files.Count == 1 ? formulario.Files[0] : null);
        await using var contenido = archivo?.OpenReadStream();
        return Responder(await servicio.SubirAsync(archivo?.FileName, archivo?.ContentType, contenido, ct), StatusCodes.Status201Created);
    }

    private static async Task<IResult> Descargar(int id, HttpResponse respuesta, IServicioDocumentos servicio, CancellationToken ct)
    {
        var resultado = await servicio.DescargarAsync(id, ct);
        if (!resultado.Exito)
            return resultado.ComoRespuesta();

        // Siempre como descarga (Content-Disposition: attachment) y sin que el navegador adivine el tipo:
        // un archivo subido nunca se ejecuta dentro de la aplicación.
        respuesta.Headers.XContentTypeOptions = "nosniff";
        var (documento, contenido) = resultado.Valor!;
        return Results.File(contenido, documento.Tipo, documento.Nombre);
    }

    /// <summary>Éxito: el mensaje y el documento. Fallo: el ProblemDetails de siempre.</summary>
    private static IResult Responder(Resultado<DocumentoVista> resultado, int estadoExito = StatusCodes.Status200OK) =>
        resultado.Exito
            ? Results.Json(new { mensaje = resultado.Mensaje, documento = resultado.Valor }, statusCode: estadoExito)
            : resultado.ComoRespuesta();

    private static IResult Problema(int estado, string detalle) => Results.Problem(detail: detalle, statusCode: estado);
}
