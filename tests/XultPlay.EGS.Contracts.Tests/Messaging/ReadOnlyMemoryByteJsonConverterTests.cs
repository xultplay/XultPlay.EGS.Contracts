using System;
using System.Text.Json;
using XultPlay.EGS.Contracts.Messaging;
using Xunit;

namespace XultPlay.EGS.Contracts.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="ReadOnlyMemoryByteJsonConverter"/>.
/// Covers API Reference §8 tests 6 through 10, plus defensive interop tests.
/// Naming follows GameBackend Coding Standards v1.0 §7.2:
/// MethodName_Scenario_ExpectedResult.
/// </summary>
public sealed class ReadOnlyMemoryByteJsonConverterTests
{
    // Cached options instances per CA1869 — avoid allocating new
    // JsonSerializerOptions on every serialization call.
    private static readonly JsonSerializerOptions _optionsWithConverter = BuildOptionsWithConverter();
    private static readonly JsonSerializerOptions _optionsBuiltIn = new();

    private static JsonSerializerOptions BuildOptionsWithConverter()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ReadOnlyMemoryByteJsonConverter());
        return options;
    }

    /// <summary>
    /// Deterministic, reproducible pseudo-random byte fill that does NOT use
    /// <see cref="Random"/> (which would trip CA5394). This is a simple
    /// linear-congruential generator with a fixed seed — adequate for
    /// "non-trivial byte pattern" use in round-trip tests.
    /// </summary>
    private static void FillDeterministicPseudoRandom(byte[] buffer, uint seed)
    {
        // Numerical Recipes LCG constants (Park & Miller / Knuth).
        uint state = seed;
        for (var i = 0; i < buffer.Length; i++)
        {
            state = (state * 1664525U) + 1013904223U;
            buffer[i] = (byte)(state >> 24);
        }
    }

    [Fact]
    public void Write_EmptyMemory_ProducesEmptyJsonString()
    {
        // Arrange — §8.6: empty memory round-trips through "".
        var input = ReadOnlyMemory<byte>.Empty;

        // Act.
        var json = JsonSerializer.Serialize(input, _optionsWithConverter);

        // Assert.
        Assert.Equal("\"\"", json);
    }

    [Fact]
    public void Read_EmptyJsonString_ReturnsEmptyMemory()
    {
        // Arrange — §8.6: empty memory round-trips through "".
        const string json = "\"\"";

        // Act.
        var result = JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter);

        // Assert.
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void RoundTrip_EmptyMemory_RemainsEmpty()
    {
        // Arrange — §8.6: empty memory round-trips through "".
        var input = ReadOnlyMemory<byte>.Empty;

        // Act.
        var json = JsonSerializer.Serialize(input, _optionsWithConverter);
        var output = JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter);

        // Assert.
        Assert.True(output.IsEmpty);
    }

    [Fact]
    public void Read_NullToken_ReturnsEmptyMemory()
    {
        // Arrange — §8.7: null JSON token deserializes to Empty.
        const string json = "null";

        // Act.
        var result = JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter);

        // Assert.
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Read_InvalidBase64_ThrowsJsonException()
    {
        // Arrange — §8.8: invalid base64 throws JsonException.
        // "@@@@" — '@' is outside the base64 alphabet.
        const string json = "\"@@@@\"";

        // Act + Assert.
        var ex = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter));

        Assert.Contains("base64", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void Read_MalformedBase64Padding_ThrowsJsonException()
    {
        // Arrange — §8.8 (extra): valid alphabet but invalid length without
        // canonical padding. Convert.FromBase64String rejects this.
        const string json = "\"AQ\"";

        // Act + Assert.
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter));
    }

    [Fact]
    public void Read_NonStringNonNullToken_ThrowsJsonException()
    {
        // Arrange — §8.8 (extra): a JSON number is not a valid representation.
        const string json = "12345";

        // Act + Assert.
        var ex = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter));

        // Don't pin to the exact wording — just confirm the message tells
        // the caller what token type was seen.
        Assert.Contains("but got", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTrip_SixtyFourKilobytePayload_PreservesAllBytes()
    {
        // Arrange — §8.9: 64 KB payload round-trips without truncation.
        var input = new byte[64 * 1024];

        // Deterministic non-trivial fill so any truncation or padding shift is detectable.
        for (var i = 0; i < input.Length; i++)
        {
            input[i] = (byte)(i % 256);
        }

        // Act.
        var json = JsonSerializer.Serialize<ReadOnlyMemory<byte>>(input, _optionsWithConverter);
        var output = JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter);

        // Assert.
        Assert.Equal(input.Length, output.Length);
        Assert.True(input.AsSpan().SequenceEqual(output.Span));
    }

    [Fact]
    public void RoundTrip_DeterministicPseudoRandomBytes_PreservesEveryByte()
    {
        // Arrange — §8.10: pseudo-random binary bytes round-trip byte-for-byte.
        // Uses a deterministic LCG with a fixed seed (not System.Random) so the
        // test is reproducible AND does not trip CA5394.
        var input = new byte[4096];
        FillDeterministicPseudoRandom(input, seed: 0x5EED_CAFEU);

        // Act.
        var json = JsonSerializer.Serialize<ReadOnlyMemory<byte>>(input, _optionsWithConverter);
        var output = JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter);

        // Assert.
        Assert.True(input.AsSpan().SequenceEqual(output.Span));
    }

    [Fact]
    public void RoundTrip_FullByteRangeCoverage_PreservesEveryByte()
    {
        // Arrange — §8.10 (extra): explicit 0..255 coverage. Verifies that
        // high bytes (0x80..0xFF) survive — catches any accidental UTF-8 or
        // ASCII coercion in the encoding path.
        var input = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            input[i] = (byte)i;
        }

        // Act.
        var json = JsonSerializer.Serialize<ReadOnlyMemory<byte>>(input, _optionsWithConverter);
        var output = JsonSerializer.Deserialize<ReadOnlyMemory<byte>>(json, _optionsWithConverter);

        // Assert.
        Assert.True(input.AsSpan().SequenceEqual(output.Span));
    }

    [Fact]
    public void Write_KnownByteSequence_ProducesCanonicalBase64()
    {
        // Arrange — defensive: prove the wire format matches the canonical
        // example from the .NET 8 docs. {1, 2, 3} -> "AQID".
        var input = new byte[] { 1, 2, 3 };

        // Act.
        var json = JsonSerializer.Serialize<ReadOnlyMemory<byte>>(input, _optionsWithConverter);

        // Assert.
        Assert.Equal("\"AQID\"", json);
    }

    [Fact]
    public void Write_MatchesBuiltInReadOnlyMemoryByteFormat()
    {
        // Arrange — defensive: feed the same bytes through this converter
        // AND through System.Text.Json's built-in .NET 8 ReadOnlyMemory<byte>
        // support. Wire bytes must match exactly. This is the early-warning
        // sentinel for any future drift against XultPlay.RedisStream.
        var input = new byte[] { 0x00, 0x7F, 0x80, 0xFF, 0x42 };

        // Act.
        var jsonOurs = JsonSerializer.Serialize<ReadOnlyMemory<byte>>(input, _optionsWithConverter);
        var jsonBuiltIn = JsonSerializer.Serialize<ReadOnlyMemory<byte>>(input, _optionsBuiltIn);

        // Assert.
        Assert.Equal(jsonBuiltIn, jsonOurs);
    }
}
