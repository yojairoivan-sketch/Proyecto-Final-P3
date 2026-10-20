using System.Text.Json.Serialization;
using Inventario.Api;
using Inventario.Api.Acceso;
using Inventario.Api.Errores;
using Inventario.Api.Permisos;
using Inventario.Core.ColaCorreos;
using Inventario.Core.ControlAcceso;
using Inventario.Core.ControlAcceso.Usuarios;
using Inventario.Core.GestionPermisos;
using Inventario.Negocio;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

var cadenaConexion = builder.Configuration.GetConnectionString("Inventario")
    ?? throw new InvalidOperationException("Falta la cadena de conexión (variable ConnectionStrings__Inventario).");

// RD-11: una sola fuente de hora para todo el sistema. Todas las fechas se guardan en UTC, con la precisión de la base.
builder.Services.AddSingleton<TimeProvider>(new RelojDelSistema());
builder.Services.AgregarErroresControlados();
builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Piezas del Core. La API solo encola: nunca habla con el servidor SMTP (RF-NOT-08).
builder.Services.AddColaCorreos(cadenaConexion);
builder.Services.AddControlAcceso(cadenaConexion, opciones =>
    opciones.UrlPublica = builder.Configuration["URL_PUBLICA"] ?? opciones.UrlPublica);
// Lo que se puede pedir sale de la tabla de operaciones del host, así el Core no conoce las del negocio (RD-03).
builder.Services.AddGestionPermisos(cadenaConexion, opciones =>
    opciones.PermisosSolicitables = Operaciones.Solicitables.Select(o => new PermisoSolicitable(o.Nombre, o.Solicitable!)).ToList());

// Módulo de negocio: depende del Core, nunca al revés (RD-03).
builder.Services.AddNegocio(cadenaConexion);

// La credencial de sesión se valida en cada petición contra Control de acceso.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();
builder.Services.AddAuthentication(AutenticacionSesion.Esquema)
    .AddScheme<AuthenticationSchemeOptions, AutenticacionSesion>(AutenticacionSesion.Esquema, null);
builder.Services.AddAuthorization(opciones =>
    // Defensa extra: lo que no declare nada exige sesión. VerificarQueTodoEndpointDeclareSuOperacion impide que pase.
    opciones.FallbackPolicy = new AuthorizationPolicyBuilder(AutenticacionSesion.Esquema).RequireAuthenticatedUser().Build());

var app = builder.Build();

app.UsarErroresControlados();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/salud", (TimeProvider reloj) => Results.Ok(new { estado = "ok", hora = reloj.GetUtcNow() }))
    .Requiere(Operaciones.ConsultarSalud);
app.MapearCuentas();
app.MapearSesion();
app.MapearUsuarios();
app.MapearContrasenas();
app.MapearSolicitudes();

app.VerificarQueTodoEndpointDeclareSuOperacion();

await app.Services.MigrarColaCorreosAsync();
await app.Services.MigrarControlAccesoAsync();
await app.Services.MigrarGestionPermisosAsync();
await app.Services.MigrarNegocioAsync();
app.Logger.LogInformation("{Mensaje}", await app.Services.AsegurarAdministradorInicialAsync(
    builder.Configuration["ADMIN_NOMBRE"], builder.Configuration["ADMIN_CORREO"], builder.Configuration["ADMIN_CONTRASENA"]));
// Si la aplicación se detuvo justo después de aprobar una solicitud, aquí se termina de aplicar (RF-GP-05, RD-09).
if (await app.Services.AplicarSolicitudesAprobadasAsync() is > 0 and var aplicadas)
    app.Logger.LogInformation("Se aplicaron {Cantidad} solicitudes de permiso que habían quedado aprobadas.", aplicadas);

app.Run();
