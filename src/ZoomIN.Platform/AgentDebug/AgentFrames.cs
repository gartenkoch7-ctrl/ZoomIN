using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ZoomIN.Core.AgentDebug;

namespace ZoomIN.Platform.AgentDebug;

/// <summary>
/// Frames on the KI-Debug pipe (ADR-0027): 4 bytes length (little endian), then UTF-8 JSON. The
/// caller owns the stream; nothing here opens files or pipes (ADR-0025).
/// </summary>
public static class AgentFrames
{
    public static Task WriteRequestAsync(Stream stream, AgentRequest request, CancellationToken cancellationToken) =>
        WriteAsync(stream, request, AgentJsonContext.Default.AgentRequest, cancellationToken);

    public static Task WriteResponseAsync(Stream stream, AgentResponse response, CancellationToken cancellationToken) =>
        WriteAsync(stream, response, AgentJsonContext.Default.AgentResponse, cancellationToken);

    /// <returns>Null when the other side closed the pipe before a new frame began.</returns>
    public static Task<AgentRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken) =>
        ReadAsync(stream, AgentJsonContext.Default.AgentRequest, cancellationToken);

    /// <returns>Null when the other side closed the pipe before a new frame began.</returns>
    public static Task<AgentResponse?> ReadResponseAsync(Stream stream, CancellationToken cancellationToken) =>
        ReadAsync(stream, AgentJsonContext.Default.AgentResponse, cancellationToken);

    private static async Task WriteAsync<T>(Stream stream, T value, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var body = JsonSerializer.SerializeToUtf8Bytes(value, type);
        if (body.Length > AgentProtocol.MaxFrameBytes)
        {
            throw new InvalidDataException("Agent frame too large");
        }

        var length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, body.Length);
        await stream.WriteAsync(length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T?> ReadAsync<T>(Stream stream, JsonTypeInfo<T> type, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(stream);
        var length = new byte[4];
        var first = await stream.ReadAsync(length.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        if (first == 0)
        {
            return null;
        }

        await stream.ReadExactlyAsync(length.AsMemory(1), cancellationToken).ConfigureAwait(false);
        var size = BinaryPrimitives.ReadInt32LittleEndian(length);
        if (size <= 0 || size > AgentProtocol.MaxFrameBytes)
        {
            throw new InvalidDataException("Agent frame has an invalid length");
        }

        var body = new byte[size];
        await stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, type) ?? throw new InvalidDataException("Agent frame is empty");
    }
}
