using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Windows.Input;
using Microsoft.Win32;
using QuickConvert.Core.Conversion;
using QuickConvert.Core.Jobs;
using QuickConvert.Core.Messaging;

namespace QuickConvert.App;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly JobQueue _queue = new();
    private readonly ConversionEngine _conversion;
    private readonly DownloadEngine _downloader;
    private readonly JsonHistoryStore _historyStore;
    private readonly string _logPath;
    private readonly SynchronizationContext _uiContext;
    private readonly HashSet<string> _recordedJobs = [];
    private readonly Dictionary<QueuedJob, Action> _retryActions = [];
    private string[] _selectedPaths = [];
    private string _selectionTitle = "Wybierz pliki albo użyj prawego przycisku myszy";
    private string _animationWarning = string.Empty;
    private string _updateMessage = string.Empty;
    private string? _updateUrl;

    public MainViewModel()
    {
        _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        var baseDirectory = AppContext.BaseDirectory;
        var ffmpeg = ResolveTool(baseDirectory, "ffmpeg.exe");
        var ytDlp = ResolveTool(baseDirectory, "yt-dlp.exe");
        var runner = new SystemProcessRunner();
        _conversion = new ConversionEngine(ffmpeg, runner);
        _downloader = new DownloadEngine(ytDlp, runner);

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickConvert");
        _historyStore = new JsonHistoryStore(Path.Combine(dataDirectory, "history.json"));
        _logPath = Path.Combine(dataDirectory, "logs", "quickconvert.log");
        DownloadDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "QuickConvert");

        ConvertCommand = new RelayCommand(
            parameter => StartConversion(parameter as string),
            parameter => parameter is string && _selectedPaths.Length > 0);
        SelectFilesCommand = new RelayCommand(_ => SelectFiles());
        CancelCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is QueuedJob job)
                    _queue.Cancel(job.Id);
            });
        OpenOutputCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is QueuedJob { OutputPaths.Count: > 0 } job)
                    OpenInExplorer(job.OutputPaths[0]);
            });
        RetryCommand = new RelayCommand(parameter =>
        {
            if (parameter is QueuedJob job &&
                job.Status is JobStatus.Failed or JobStatus.Canceled &&
                _retryActions.TryGetValue(job, out var retry))
                retry();
        });
        OpenUpdateCommand = new RelayCommand(
            _ => OpenUpdate(),
            _ => _updateUrl is not null);
        OpenLogCommand = new RelayCommand(_ => OpenLog());
        ClearLocalDataCommand = new RelayCommand(async _ => await ClearLocalDataAsync());

        _queue.JobAdded += (_, job) => _uiContext.Post(_ =>
        {
            Jobs.Insert(0, job);
            OnPropertyChanged(nameof(HasActiveJobs));
        }, null);
        _queue.JobChanged += (_, job) => _uiContext.Post(async _ =>
        {
            OnPropertyChanged(nameof(HasActiveJobs));
            if (job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Canceled &&
                _recordedJobs.Add(job.Id))
            {
                var entry = new JobHistoryEntry(
                    job.Id, job.Kind, job.Description, job.Status, DateTimeOffset.Now);
                await _historyStore.AddAsync(entry);
                History.Insert(0, entry);
                while (History.Count > 50)
                    History.RemoveAt(History.Count - 1);
                await AppendLogAsync(job);
                JobFinished?.Invoke(job);
            }
        }, null);

        _ = LoadHistoryAsync();
        _ = CheckUpdatesAsync(dataDirectory, ytDlp);
    }

    public ObservableCollection<string> CompatibleFormats { get; } = [];
    public ObservableCollection<QueuedJob> Jobs { get; } = [];
    public ObservableCollection<JobHistoryEntry> History { get; } = [];
    public ICommand ConvertCommand { get; }
    public ICommand SelectFilesCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenOutputCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand OpenUpdateCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand ClearLocalDataCommand { get; }
    public string DownloadDirectory { get; }

    public string SelectionTitle
    {
        get => _selectionTitle;
        private set => Set(ref _selectionTitle, value);
    }

    public string AnimationWarning
    {
        get => _animationWarning;
        private set => Set(ref _animationWarning, value);
    }

    public bool HasActiveJobs => Jobs.Any(job => job.Status is JobStatus.Queued or JobStatus.Running);

    public string UpdateMessage
    {
        get => _updateMessage;
        private set => Set(ref _updateMessage, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<QueuedJob>? JobFinished;

    public static string GetFriendlyError(string? code) => code switch
    {
        "source_missing" => "Nie znaleziono pliku źródłowego.",
        "unsupported_format" => "Wybrany format nie jest obsługiwany.",
        "unsupported_url" => "Ten adres nie wskazuje obsługiwanego filmu.",
        "network_error" => "Sprawdź połączenie z internetem.",
        "media_unavailable" => "Materiał jest niedostępny lub prywatny.",
        "disk_full" => "Brak wolnego miejsca na dysku.",
        "canceled" => "Zadanie anulowano.",
        "tool_failed" => "Narzędzie nie mogło przetworzyć materiału.",
        null => string.Empty,
        _ => "Wystąpił nieoczekiwany błąd."
    };

    public Task HandleEnvelopeAsync(IpcEnvelope envelope)
    {
        switch (envelope.Operation)
        {
            case "convert" when envelope.Convert is not null:
                LoadFiles(envelope.Convert.Paths, append: true);
                break;
            case "download" when envelope.Download is not null:
                EnqueueDownload(envelope.Download);
                break;
        }
        return Task.CompletedTask;
    }

    private void SelectFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "Wybierz pliki do konwersji",
            Filter = "Obsługiwane media|*.mp3;*.m4a;*.aac;*.wav;*.flac;*.ogg;*.opus;*.wma;*.mp4;*.mkv;*.webm;*.mov;*.avi;*.wmv;*.m4v;*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp;*.tiff;*.tif|Wszystkie pliki|*.*"
        };
        if (dialog.ShowDialog() == true)
            LoadFiles(dialog.FileNames);
    }

    private void LoadFiles(IEnumerable<string> paths, bool append = false)
    {
        var incoming = paths.Where(File.Exists);
        _selectedPaths = (append ? _selectedPaths.Concat(incoming) : incoming)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SelectionTitle = _selectedPaths.Length switch
        {
            0 => "Nie znaleziono obsługiwanych plików",
            1 => Path.GetFileName(_selectedPaths[0]),
            _ => $"Wybrano {_selectedPaths.Length} plików"
        };

        CompatibleFormats.Clear();
        foreach (var format in FormatCatalog.GetCompatibleOutputs(_selectedPaths))
            CompatibleFormats.Add(format.ToUpperInvariant());
        AnimationWarning = _selectedPaths.Any(path =>
                Path.GetExtension(path) is ".gif" or ".webp")
            ? "Przy formacie statycznym zostanie użyta pierwsza klatka."
            : string.Empty;
        (ConvertCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void StartConversion(string? format)
    {
        if (string.IsNullOrWhiteSpace(format) || _selectedPaths.Length == 0)
            return;
        var request = new ConvertFilesRequest(
            _selectedPaths.ToArray(),
            format.ToLowerInvariant(),
            ConversionPreset.Default,
            OutputDirectoryMode.Adjacent);
        EnqueueWithRetry(
            SelectionTitle,
            "convert",
            (progress, token) => _conversion.ConvertAsync(request, progress, token));
    }

    private void EnqueueDownload(DownloadMediaRequest request)
    {
        EnqueueWithRetry(
            $"YouTube • {request.MediaType.ToUpperInvariant()} • {request.MaxResolution}",
            "download",
            (progress, token) => _downloader.DownloadAsync(
                request, DownloadDirectory, progress, token));
    }

    private void EnqueueWithRetry(
        string description,
        string kind,
        Func<IProgress<string>, CancellationToken, Task<JobExecutionResult>> execute)
    {
        void Retry()
        {
            var job = _queue.Enqueue(description, kind, execute);
            _retryActions[job] = Retry;
        }
        Retry();
    }

    private async Task LoadHistoryAsync()
    {
        var entries = await _historyStore.LoadAsync();
        _uiContext.Post(_ =>
        {
            foreach (var entry in entries)
                History.Add(entry);
        }, null);
    }

    private async Task CheckUpdatesAsync(string dataDirectory, string ytDlpPath)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);
        var coordinator = new UpdateCoordinator(
            Path.Combine(dataDirectory, "updates.json"), ytDlpPath);
        var release = await coordinator.CheckAsync(version, CancellationToken.None);
        if (release is null)
            return;
        _uiContext.Post(_ =>
        {
            _updateUrl = release.Url;
            UpdateMessage = $"Dostępna wersja {release.Version}.";
            (OpenUpdateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }, null);
    }

    private static string ResolveTool(string baseDirectory, string name)
    {
        var bundled = Path.Combine(baseDirectory, "tools", name);
        return File.Exists(bundled) ? bundled : name;
    }

    private static void OpenInExplorer(string path)
    {
        if (!File.Exists(path))
            return;
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add($"/select,{path}");
        Process.Start(startInfo);
    }

    private void OpenUpdate()
    {
        if (_updateUrl is null)
            return;
        Process.Start(new ProcessStartInfo(_updateUrl) { UseShellExecute = true });
    }

    private async Task AppendLogAsync(QueuedJob job)
    {
        var directory = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var line = $"{DateTimeOffset.Now:O}\t{job.Kind}\t{job.Status}\t{job.ErrorCode ?? "-"}\t{job.Description}{Environment.NewLine}";
        await File.AppendAllTextAsync(_logPath, line);
    }

    private void OpenLog()
    {
        var directory = Path.GetDirectoryName(_logPath)!;
        Directory.CreateDirectory(directory);
        if (!File.Exists(_logPath))
            File.WriteAllText(_logPath, "QuickConvert — dziennik lokalny bez adresów URL i argumentów narzędzi." + Environment.NewLine);
        OpenInExplorer(_logPath);
    }

    private async Task ClearLocalDataAsync()
    {
        await _historyStore.ClearAsync();
        History.Clear();
        if (File.Exists(_logPath))
            File.Delete(_logPath);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public ValueTask DisposeAsync() => _queue.DisposeAsync();
}
