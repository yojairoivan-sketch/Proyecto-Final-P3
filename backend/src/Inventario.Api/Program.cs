using System.Text.Json.Serialization;
using Inventario.Api.Errores;

var builder = WebApplication.CreateBuilder(args);

// RD-11: una sola fuente de hora para todo el sistema. Todas las fechas se guardan en UTC.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AgregarErroresControlados();
builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UsarErroresControlados();

app.MapGet("/api/salud", (TimeProvider reloj) => Results.Ok(new { estado = "ok", hora = reloj.GetUtcNow() }));

app.Run();
