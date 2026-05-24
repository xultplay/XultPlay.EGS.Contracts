using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XultPlay.EGS.Contracts.Messaging;

/// <summary>
/// <see cref="JsonConverter{T}"/> that serializes <see cref="ReadOnlyMemory{T}"/> of
/// <see cref="byte"/> as a standard base64-encoded JSON string, and deserializes the
/// same form back to <see cref="ReadOnlyMemory{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This converter exists so payload-bearing fields across the XultPlay EGS fleet
/// share one canonical wire encoding. It MUST produce byte-identical output to the
/// format used by <c>XultPlay.RedisStream.Serialization.JsonMessageSerializer</c>;
/// see the API Reference §4 and §8 test #11.
/// </para>
/// <para>
/// Behavior contract:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>Writes via <see cref="Utf8JsonWriter.WriteBase64StringValue(System.ReadOnlySpan{byte})"/>
///     with standard padding (no base64url, no padding strip). This method
///     bypasses the configured <c>JavaScriptEncoder</c>, so <c>+</c> and
///     <c>/</c> in the base64 output appear literally rather than as
///     <c>\u002B</c> / <c>\u002F</c> escapes. This matches what the .NET 8
///     built-in <see cref="ReadOnlyMemory{T}"/> serialization (and
///     <c>XultPlay.RedisStream</c>) produces.</description>
///   </item>
///   <item>
///     <description>Reads a JSON string via <see cref="Convert.FromBase64String(string)"/>
///     into a freshly allocated <see cref="byte"/> array wrapped in
///     <see cref="ReadOnlyMemory{T}"/>.</description>
///   </item>
///   <item>
///     <description>An empty memory writes as the JSON empty string <c>""</c>.</description>
///   </item>
///   <item>
///     <description>A JSON <c>null</c> token deserializes to
///     <see cref="ReadOnlyMemory{T}.Empty"/>. (<see cref="ReadOnlyMemory{T}"/> is a
///     value type and cannot itself be null.)</description>
///   </item>
///   <item>
///     <description>Any other token type, or malformed base64 input, throws
///     <see cref="JsonException"/>.</description>
///   </item>
/// </list>
/// <para>
/// Registration: apply on a per-field basis with
/// <c>[property: JsonConverter(typeof(ReadOnlyMemoryByteJsonConverter))]</c>, or
/// add an instance to <see cref="JsonSerializerOptions.Converters"/> for ad-hoc
/// serialization.
/// </para>
/// </remarks>
public sealed class ReadOnlyMemoryByteJsonConverter : JsonConverter<ReadOnlyMemory<byte>>
{
    /// <inheritdoc/>
    public override ReadOnlyMemory<byte> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                "Expected a base64-encoded string or null for ReadOnlyMemory<byte>, but got "
                + reader.TokenType
                + ".");
        }

        var encoded = reader.GetString();
        if (string.IsNullOrEmpty(encoded))
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            throw new JsonException(
                "ReadOnlyMemory<byte> value was not a valid base64 string.",
                ex);
        }
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        ReadOnlyMemory<byte> value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // Utf8JsonWriter.WriteBase64StringValue base64-encodes the bytes
        // directly into the UTF-8 output buffer WITHOUT routing the result
        // through the JavaScriptEncoder. This is critical for wire
        // compatibility with XultPlay.RedisStream: the built-in .NET 8
        // serialization path for ReadOnlyMemory<byte> uses this same method
        // internally, so '+' and '/' inside the base64 output remain literal
        // (not escaped to \u002B / \u002F as they would be if we routed
        // through WriteStringValue(string)). Empty memory produces "".
        writer.WriteBase64StringValue(value.Span);
    }
}
