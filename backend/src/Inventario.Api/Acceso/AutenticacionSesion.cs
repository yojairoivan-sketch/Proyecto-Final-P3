using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Inventario.Core.ControlAcceso;
using Inventario.Core.ControlAcceso.Sesiones;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Inventario.Api.Acceso;

/// <summary>
/// Lee la credencial «Authorization: Bearer ...» y la valida contra Control de acceso en cada petición:
/// una sesión cerrada, vencida o de un usuario desactivado deja de servir de inmediato.
/// </summary>
public sealed class AutenticacionSesion(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory logger,
    UrlEncoder codificador,
    IServicioSesiones sesiones,
    IProblemDetailsService problemas)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, codificador)
{
    public const string Esquema = "Sesion";
    public const string ClaimSesion = "sesion";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cabecera = Request.Headers.Authorization.ToString();
        if (!cabecera.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var usuario = await sesiones.ValidarAsync(cabecera["Bearer ".Length..], Context.RequestAborted);
        if (usuario is null)
            return AuthenticateResult.Fail("Sesión no válida.");

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Email, usuario.Correo),
            new(ClaimTypes.Role, usuario.Rol),
            new(ClaimSesion, usuario.SesionId.ToString(CultureInfo.InvariantCulture)),
        ];
        var identidad = new ClaimsIdentity(claims, Esquema, ClaimTypes.Name, ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema));
    }

    /// <summary>401 explícito con ProblemDetails.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var traeCredencial = Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        return Escribir(StatusCodes.Status401Unauthorized, traeCredencial
            ? "La sesión no es válida o ya terminó. Inicia sesión de nuevo."
            : "Necesitas iniciar sesión para usar esta operación.");
    }

    /// <summary>
    /// RF-CA-06: rechazo explícito cuando el rol no alcanza. Dice qué operación era y qué rol exige,
    /// también cuando la petición se arma a mano sin pasar por la interfaz.
    /// </summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        var operacion = Context.GetEndpoint()?.Metadata.GetMetadata<Operacion>();
        var rol = Context.User.FindFirstValue(ClaimTypes.Role);
        return Escribir(StatusCodes.Status403Forbidden, operacion is null
            ? "Tu rol no tiene permiso para usar esta operación."
            : $"La operación {operacion.Nombre} es solo para: {string.Join(", ", operacion.Roles)}. Tu rol es {rol}.");
    }

    private async Task Escribir(int estado, string detalle)
    {
        Response.StatusCode = estado;
        await problemas.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = Context,
            ProblemDetails = { Status = estado, Detail = detalle },
        });
    }
}

/// <summary>Implementación HTTP de la interfaz provista IUsuarioActual: arma el usuario desde la credencial ya validada.</summary>
public sealed class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    public UsuarioAutenticado? Usuario
    {
        get
        {
            var principal = accesor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            return new UsuarioAutenticado(
                int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture),
                principal.FindFirstValue(ClaimTypes.Name)!,
                principal.FindFirstValue(ClaimTypes.Email)!,
                principal.FindFirstValue(ClaimTypes.Role)!,
                long.Parse(principal.FindFirstValue(AutenticacionSesion.ClaimSesion)!, CultureInfo.InvariantCulture));
        }
    }
}
