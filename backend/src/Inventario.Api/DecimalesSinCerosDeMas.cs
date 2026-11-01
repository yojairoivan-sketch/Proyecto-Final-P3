using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inventario.Api;

/// <summary>
/// Escribe los decimales sin ceros de más: 8500 y no 8500.0000, que es lo que da multiplicar dos numeric(12,2).
/// Al leer solo acepta números, así que un texto en un campo numérico es un 400 y no se adivina su valor.
/// </summary>
public sealed class DecimalesSinCerosDeMas : JsonConverter<decimal>
{
    // Dividir por 1 con tantos ceros deja el decimal con la menor cantidad de decimales que lo representa.
    private const decimal Normalizador = 1.0000000000000000000000000000m;

    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number ? reader.GetDecimal() : throw new JsonException("Se esperaba un número.");

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value / Normalizador);
}
