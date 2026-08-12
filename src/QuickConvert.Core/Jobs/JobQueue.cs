using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickConvert.Core.Jobs;

public sealed class QueuedJob : INotifyPropertyChanged
{
    private JobStatus _status = JobStatus.Queued;
    private string _progressText = "Oczekuje";
    private IReadOnlyList<string> _outputPaths = [];
    private string? _errorCode;

    internal QueuedJob(
        string description,
        string kind,
        Func<IProgress<string>, CancellationToken, Task<JobExecutionResult>> execute)
    {
        Id = Guid.NewGuid().ToString("N");
        Description = description;
        Kind = kind;
        Execute = execute;
    }

    public string Id { get; }
    public string Description { get; }
    public string Kind { get; }
    public JobStatus Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => Set(ref _progressText, value);
    }

    public IReadOnlyList<string> OutputPaths
    {
        get => _outputPaths;
        private set => Set(ref _outputPaths, value);
    }

    public string? ErrorCode
    {
        get => _errorCode;
        private set => Set(ref _errorCode, value);
    }

    public string ErrorMessage => Status == JobStatus.Failed
        ? ToolErrorClassifier.Classify(ErrorCode) == "tool_failed"
            ? ErrorCode switch
            {
                "source_missing" => "Nie znaleziono pliku źródłowego.",
                "unsupported_format" => "Format nie jest obsługiwany.",
                "network_error" => "Sprawdź połączenie z internetem.",
                "media_unavailable" => "Materiał jest niedostępny.",
                "disk_full" => "Brak wolnego miejsca na dysku.",
                _ => "Przetwarzanie nie powiodło się."
            }
            : ErrorCode ?? string.Empty
        : string.Empty;

    internal Func<IProgress<string>, CancellationToken, Task<JobExecutionResult>> Execute { get; }
    internal CancellationTokenSource Cancellation { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void MarkRunning() => Update(JobStatus.Running, "Przetwarzanie…", [], null);

    internal void Report(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ProgressText = FriendlyProgress(value);
    }

    internal void Complete(JobExecutionResult result)
    {
        var status = result.Success
            ? JobStatus.Completed
            : result.ErrorCode == "canceled" ? JobStatus.Canceled : JobStatus.Failed;
        var text = status switch
        {
            JobStatus.Completed => "Gotowe",
            JobStatus.Canceled => "Anulowano",
            _ => "Niepowodzenie"
        };
        Update(status, text, result.OutputPaths, result.ErrorCode);
    }

    internal void Cancel() => Cancellation.Cancel();

    private void Update(
        JobStatus status,
        string progress,
        IReadOnlyList<string> outputs,
        string? errorCode)
    {
        Status = status;
        ProgressText = progress;
        OutputPaths = outputs;
        ErrorCode = errorCode;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
    }

    private static string FriendlyProgress(string value)
    {
        if (value.Contains("progress=end", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("100%", StringComparison.OrdinalIgnoreCase))
            return "Finalizowanie…";
        if (value.StartsWith("[download]", StringComparison.OrdinalIgnoreCase))
            return value.Replace("[download]", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return "Przetwarzanie…";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class JobQueue : IAsyncDisposable
{
    private readonly ConcurrentQueue<QueuedJob> _queue = new();
    private readonly ConcurrentDictionary<string, QueuedJob> _knownJobs = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _idleGate = new();
    private TaskCompletionSource _idle = CompletedSource();
    private int _pending;
    private readonly Task _worker;

    public JobQueue()
    {
        _worker = ProcessAsync();
    }

    public event EventHandler<QueuedJob>? JobAdded;
    public event EventHandler<QueuedJob>? JobChanged;

    public QueuedJob Enqueue(
        string description,
        string kind,
        Func<IProgress<string>, CancellationToken, Task<JobExecutionResult>> execute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(execute);

        var job = new QueuedJob(description, kind, execute);
        job.PropertyChanged += (_, _) => JobChanged?.Invoke(this, job);
        lock (_idleGate)
        {
            if (_pending++ == 0)
                _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _queue.Enqueue(job);
        _knownJobs[job.Id] = job;
        JobAdded?.Invoke(this, job);
        _signal.Release();
        return job;
    }

    public bool Cancel(string id)
    {
        if (!_knownJobs.TryGetValue(id, out var job))
            return false;
        job.Cancel();
        return true;
    }

    public Task WhenIdleAsync(CancellationToken cancellationToken)
    {
        lock (_idleGate)
            return _idle.Task.WaitAsync(cancellationToken);
    }

    private async Task ProcessAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_queue.TryDequeue(out var job))
                continue;

            job.MarkRunning();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _shutdown.Token, job.Cancellation.Token);
            try
            {
                var progress = new Progress<string>(job.Report);
                var result = await job.Execute(progress, linked.Token).ConfigureAwait(false);
                job.Complete(result);
            }
            catch (OperationCanceledException)
            {
                job.Complete(JobExecutionResult.Failed("canceled"));
            }
            catch (Exception exception)
            {
                job.Complete(JobExecutionResult.Failed("unexpected_error", exception.Message));
            }
            finally
            {
                _knownJobs.TryRemove(job.Id, out _);
                lock (_idleGate)
                {
                    if (--_pending == 0)
                        _idle.TrySetResult();
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _signal.Release();
        try
        {
            await _worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        _shutdown.Dispose();
        _signal.Dispose();
    }

    private static TaskCompletionSource CompletedSource()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
