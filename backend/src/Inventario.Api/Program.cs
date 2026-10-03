using System.Text.Json.Serialization;
using Inventario.Api.Acceso;
using Inventario.Api.Errores;
using Inventario.Core.ColaCorreos;
using Inventario.Core.ControlAcceso;
using Inventario.Core.ControlAcceso.Usuarios;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

var cadenaConexion = builder.Configuration.GetConnectionString("Inventario")
    ?? throw new InvalidOperationException("Falta la cadena de conexión (variable ConnectionStrings__Inventario).");

// RD-11: una sola fuente de hora para todo el sistema. Todas las fechas se guardan en UTC.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AgregarErroresControlados();
builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Piezas del Core. La API solo encola: nunca habla con el servidor SMTP (RF-NOT-08).
builder.Services.AddColaCorreos(cadenaConexion);
builder.Services.AddControlAcceso(cadenaConexion, opciones =>
    opciones.UrlPublica = builder.Configuration["URL_PUBLICA"] ?? opciones.UrlPublica);

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

app.VerificarQueTodoEndpointDeclareSuOperacion();

await app.Services.MigrarColaCorreosAsync();
await app.Services.MigrarControlAccesoAsync();
app.Logger.LogInformation("{Mensaje}", await app.Services.AsegurarAdministradorInicialAsync(
    builder.Configuration["ADMIN_NOMBRE"], builder.Configuration["ADMIN_CORREO"], builder.Configuration["ADMIN_CONTRASENA"]));

app.Run();
