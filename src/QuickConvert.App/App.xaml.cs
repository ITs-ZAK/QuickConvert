using System.IO;
using System.Windows;
using QuickConvert.Core.Jobs;
using QuickConvert.Core.Messaging;

namespace QuickConvert.App;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private CancellationTokenSource? _ipcCancellation;
    private MainViewModel? _viewModel;
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, @"Local\QuickConvert.Application", out var isPrimary);
        _ownsMutex = isPrimary;
        var initialEnvelope = ParseArguments(e.Args);

        if (!isPrimary)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await new SingleInstanceIpcClient(QuickConvertPipeName.ForCurrentUser())
                    .SendAsync(initialEnvelope ?? IpcEnvelope.ForActivate(), timeout.Token);
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException)
            {
                System.Windows.MessageBox.Show(
                    "Nie udało się połączyć z uruchomionym QuickConvert.",
                    "QuickConvert",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            Shutdown();
            return;
        }

        _viewModel = new MainViewModel();
        _window = new MainWindow(_viewModel);
        MainWindow = _window;
        _ipcCancellation = new CancellationTokenSource();
        _ = RunIpcLoopAsync(_ipcCancellation.Token);

        var background = e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        if (initialEnvelope is not null)
            await HandleEnvelopeAsync(initialEnvelope);
        if (!background || initialEnvelope is not null)
            ShowMainWindow();
    }

    private async Task RunIpcLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var server = new SingleInstanceIpcServer(QuickConvertPipeName.ForCurrentUser());
                await server.ReceiveOnceAsync(async envelope =>
                {
                    var dispatched = Dispatcher.InvokeAsync(() => HandleEnvelopeAsync(envelope));
                    await dispatched.Task.Unwrap();
                    return new IpcResponse(true, "accepted");
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                await Task.Delay(150, cancellationToken);
            }
        }
    }

    private async Task HandleEnvelopeAsync(IpcEnvelope envelope)
    {
        if (_viewModel is null)
            return;

        await _viewModel.SettingsLoaded;
        await _viewModel.HandleEnvelopeAsync(envelope);
        if (BackgroundBehaviorPolicy.ShouldShowForEnvelope(
                envelope.Operation,
                _viewModel.RunInBackgroundDuringJobs))
            ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_window is null)
            return;
        _window.RestoreWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _ipcCancellation?.Cancel();
        if (_viewModel is not null)
            _viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _ipcCancellation?.Dispose();
        if (_ownsMutex)
            _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static IpcEnvelope? ParseArguments(IReadOnlyList<string> arguments)
    {
        var shellIndex = Array.FindIndex(arguments.ToArray(), value =>
            string.Equals(value, "--shell", StringComparison.OrdinalIgnoreCase));
        if (shellIndex < 0 || shellIndex == arguments.Count - 1)
            return null;

        var paths = arguments.Skip(shellIndex + 1)
            .Where(File.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return paths.Length == 0
            ? null
            : IpcEnvelope.ForConvert(new ConvertFilesRequest(
                paths, string.Empty, ConversionPreset.Balanced, OutputDirectoryMode.Adjacent));
    }
}
