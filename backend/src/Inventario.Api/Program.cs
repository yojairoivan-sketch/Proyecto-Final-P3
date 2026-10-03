using System.Text.Json.Serialization;
using Inventario.Api.Acceso;
using Inventario.Api.Errores;
using Inventario.Core.ColaCorreos;
using Inventario.Core.ControlAcceso;

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


var app = builder.Build();

await app.Services.MigrarColaCorreosAsync();
await app.Services.MigrarControlAccesoAsync();

app.UsarErroresControlados();

app.MapGet("/api/salud", (TimeProvider reloj) => Results.Ok(new { estado = "ok", hora = reloj.GetUtcNow() }));
app.MapearCuentas();
app.MapearSesion();

app.Run();
