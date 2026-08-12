using System.Diagnostics;
using System.Text.Json;
using QuickConvert.Core.Messaging;

var caller = ResolveCaller(args);
string? requestId = null;

try
{
    var json = await NativeMessageProtocol.ReadAsync(
        Console.OpenStandardInput(), CancellationToken.None);
    if (json is null)
        return 0;

    requestId = TryReadRequestId(json);
    var validation = NativeMessageValidator.Validate(json, caller);
    if (!validation.IsValid)
    {
        await ReplyAsync(requestId, validation.Code);
        return 0;
    }

    var response = await SendToApplicationAsync(IpcEnvelope.ForDownload(validation.Request!));
    await ReplyAsync(requestId, response.Code);
    return 0;
}
catch (Exception exception) when (
    exception is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
{
    await ReplyAsync(requestId, "invalid_request");
    return 1;
}

static string? ResolveCaller(IReadOnlyList<string> arguments)
{
    foreach (var argument in arguments)
    {
        if (argument.StartsWith("chrome-extension://", StringComparison.Ordinal))
            return argument;
        if (string.Equals(argument, "quickconvert@local", StringComparison.Ordinal))
            return argument;
    }

    return null;
}

static string? TryReadRequestId(string json)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("requestId", out var id) ? id.GetString() : null;
    }
    catch (JsonException)
    {
        return null;
    }
}

static async Task<IpcResponse> SendToApplicationAsync(IpcEnvelope envelope)
{
    var client = new SingleInstanceIpcClient(QuickConvertPipeName.ForCurrentUser());
    using var firstAttempt = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
    try
    {
        return await client.SendAsync(envelope, firstAttempt.Token);
    }
    catch (Exception exception) when (exception is IOException or OperationCanceledException)
    {
        var appPath = Path.Combine(AppContext.BaseDirectory, "QuickConvert.exe");
        if (!File.Exists(appPath))
            return new IpcResponse(false, "app_unavailable");

        Process.Start(new ProcessStartInfo
        {
            FileName = appPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "--background" }
        });

        await Task.Delay(350);
        using var retry = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            return await client.SendAsync(envelope, retry.Token);
        }
        catch (Exception retryException) when (
            retryException is IOException or OperationCanceledException)
        {
            return new IpcResponse(false, "app_unavailable");
        }
    }
}

static Task ReplyAsync(string? requestId, string code)
{
    var json = JsonSerializer.Serialize(
        new { requestId, code },
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
    return NativeMessageProtocol.WriteAsync(
        Console.OpenStandardOutput(), json, CancellationToken.None);
}
