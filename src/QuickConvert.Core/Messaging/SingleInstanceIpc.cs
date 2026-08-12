using QuickConvert.Core.Jobs;
using System.IO.Pipes;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace QuickConvert.Core.Messaging;

public sealed record IpcEnvelope(
    int Version,
    string Operation,
    DownloadMediaRequest? Download,
    ConvertFilesRequest? Convert)
{
    public static IpcEnvelope ForDownload(DownloadMediaRequest request) =>
        new(1, "download", request, null);

    public static IpcEnvelope ForConvert(ConvertFilesRequest request) =>
        new(1, "convert", null, request);

    public static IpcEnvelope ForActivate() =>
        new(1, "activate", null, null);
}

public sealed record IpcResponse(bool Accepted, string Code);

public sealed class SingleInstanceIpcServer
{
    private readonly string _pipeName;

    public SingleInstanceIpcServer(string pipeName)
    {
        _pipeName = pipeName;
    }

    public async Task ReceiveOnceAsync(
        Func<IpcEnvelope, Task<IpcResponse>> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        await using var server = new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        var message = await NativeMessageProtocol.ReadAsync(server, cancellationToken).ConfigureAwait(false);
        var envelope = message is null
            ? null
            : JsonSerializer.Deserialize<IpcEnvelope>(message, JsonOptions);
        var response = envelope is { Version: 1 }
            ? await handler(envelope).ConfigureAwait(false)
            : new IpcResponse(false, "invalid_request");
        await NativeMessageProtocol.WriteAsync(
            server, JsonSerializer.Serialize(response, JsonOptions), cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static class QuickConvertPipeName
{
    public static string ForCurrentUser()
    {
        var identity = $"{Environment.UserDomainName}\\{Environment.UserName}|{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"QuickConvert.{Convert.ToHexString(hash.AsSpan(0, 12))}";
    }
}

public sealed class SingleInstanceIpcClient
{
    private readonly string _pipeName;

    public SingleInstanceIpcClient(string pipeName)
    {
        _pipeName = pipeName;
    }

    public async Task<IpcResponse> SendAsync(
        IpcEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        await using var client = new NamedPipeClientStream(
            ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await NativeMessageProtocol.WriteAsync(
            client, JsonSerializer.Serialize(envelope, JsonOptions), cancellationToken).ConfigureAwait(false);
        var message = await NativeMessageProtocol.ReadAsync(client, cancellationToken).ConfigureAwait(false);
        return message is null
            ? new IpcResponse(false, "app_unavailable")
            : JsonSerializer.Deserialize<IpcResponse>(message, JsonOptions) ??
              new IpcResponse(false, "invalid_response");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
