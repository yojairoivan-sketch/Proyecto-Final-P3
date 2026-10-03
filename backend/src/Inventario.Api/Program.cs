using System.Text.Json.Serialization;
using Inventario.Api.Errores;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AgregarErroresControlados();
builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UsarErroresControlados();

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" }));

app.Run();
