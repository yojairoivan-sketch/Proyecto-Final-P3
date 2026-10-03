using Inventario.Core.ColaCorreos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Enviador de la cola de correos (RF-NOT-09). Uso:
//   Inventario.Enviador              una pasada: envía los pendientes y termina
//   Inventario.Enviador --vigilar N  repite la pasada cada N segundos (30 si no se indica)

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

var cadena = builder.Configuration.GetConnectionString("Inventario");
if (string.IsNullOrWhiteSpace(cadena))
    return Fallar("Falta la cadena de conexión (variable ConnectionStrings__Inventario).");

ConfiguracionSmtp smtp;
try
{
    // RF-NOT-13: las credenciales del servidor de correo salen solo de variables de entorno.
    smtp = ConfiguracionSmtp.DesdeVariables(nombre => builder.Configuration[nombre]);
}
catch (InvalidOperationException ex)
{
    return Fallar(ex.Message);
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddColaCorreos(cadena);
builder.Services.AddProcesadorCola(smtp);

using var host = builder.Build();

var posicion = Array.IndexOf(args, "--vigilar");
var vigilar = posicion >= 0;
var segundos = vigilar && posicion + 1 < args.Length && int.TryParse(args[posicion + 1], out var n) && n > 0 ? n : 30;

using var cancelar = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancelar.Cancel();
};

Console.WriteLine($"Servidor SMTP: {smtp}");

try
{
    do
    {
        await using (var alcance = host.Services.CreateAsyncScope())
        {
            var procesador = alcance.ServiceProvider.GetRequiredService<ProcesadorCola>();
            var resumen = await procesador.ProcesarPendientesAsync(cancelar.Token);
            Console.WriteLine(resumen is { Enviados: 0, ConError: 0 }
                ? $"[{Hora()}] No hay correos pendientes."
                : $"[{Hora()}] Enviados: {resumen.Enviados}. Con error (siguen pendientes): {resumen.ConError}.");
        }

        if (vigilar)
            await Task.Delay(TimeSpan.FromSeconds(segundos), cancelar.Token);
    }
    while (vigilar);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Enviador detenido.");
}
catch (Exception ex)
{
    return Fallar($"No se pudo procesar la cola: {ex.Message}");
}

return 0;

static string Hora() => $"{TimeProvider.System.GetUtcNow():HH:mm:ss} UTC";

static int Fallar(string mensaje)
{
    Console.Error.WriteLine(mensaje);
    return 1;
}
