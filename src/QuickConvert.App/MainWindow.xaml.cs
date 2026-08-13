using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace QuickConvert.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Icon _trayIcon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _trayMenu;
    private readonly DispatcherTimer _completionExitTimer;
    private bool _allowClose;
    private bool _completionExitPending;
    private bool _cleanedUp;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        _viewModel.JobFinished += ViewModelOnJobFinished;

        _trayIcon = LoadTrayIcon();
        _trayMenu = new Forms.ContextMenuStrip();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "QuickConvert",
            Visible = false,
            ContextMenuStrip = _trayMenu
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreWindow();
        _trayMenu.Items.Add("Otwórz", null, (_, _) => RestoreWindow());
        _trayMenu.Items.Add("Zamknij", null, (_, _) =>
        {
            _allowClose = true;
            Close();
        });

        _completionExitTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _completionExitTimer.Tick += CompletionExitTimerOnTick;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.TryApply(new WindowInteropHelper(this).Handle);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            switch (BackgroundBehaviorPolicy.GetCloseAction(
                        _viewModel.HasActiveJobs,
                        _viewModel.RunInBackgroundDuringJobs))
            {
                case WindowCloseAction.HideToTray:
                    e.Cancel = true;
                    Hide();
                    UpdateTrayVisibility();
                    _notifyIcon.ShowBalloonTip(
                        1500,
                        "QuickConvert",
                        "Zadania nadal działają w tle.",
                        Forms.ToolTipIcon.Info);
                    return;
                case WindowCloseAction.KeepVisible:
                    e.Cancel = true;
                    RestoreWindow();
                    return;
            }
        }

        CleanupTrayResources();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RunInBackgroundDuringJobs))
        {
            if (_viewModel.HasActiveJobs &&
                !_viewModel.RunInBackgroundDuringJobs &&
                !IsVisible)
                RestoreWindow();
            else
                UpdateTrayVisibility();
            return;
        }

        if (e.PropertyName != nameof(MainViewModel.HasActiveJobs))
            return;

        if (_viewModel.HasActiveJobs)
        {
            CancelPendingExit();
            UpdateTrayVisibility();
            return;
        }

        if (!IsVisible && _viewModel.RunInBackgroundDuringJobs)
        {
            _notifyIcon.Visible = true;
            _notifyIcon.ShowBalloonTip(
                2000,
                "QuickConvert",
                "Wszystkie zadania zostały zakończone.",
                Forms.ToolTipIcon.Info);
            BeginCompletionExit();
            return;
        }

        UpdateTrayVisibility();
    }

    private void ViewModelOnJobFinished(QuickConvert.Core.Jobs.QueuedJob job)
    {
        if (IsVisible || !_viewModel.RunInBackgroundDuringJobs || _completionExitPending)
            return;

        _notifyIcon.Visible = true;
        var message = job.Status == QuickConvert.Core.Jobs.JobStatus.Completed
            ? $"Gotowe: {job.Description}"
            : $"Niepowodzenie: {job.Description}";
        _notifyIcon.ShowBalloonTip(2000, "QuickConvert", message, Forms.ToolTipIcon.Info);
    }

    internal void RestoreWindow()
    {
        CancelPendingExit();
        Show();
        WindowState = WindowState.Normal;
        Activate();
        UpdateTrayVisibility();
    }

    private void UpdateTrayVisibility()
    {
        if (_cleanedUp)
            return;
        _notifyIcon.Visible = _completionExitPending ||
            BackgroundBehaviorPolicy.ShouldShowTray(
                _viewModel.HasActiveJobs,
                _viewModel.RunInBackgroundDuringJobs);
    }

    private void BeginCompletionExit()
    {
        _completionExitPending = true;
        _completionExitTimer.Stop();
        _completionExitTimer.Start();
    }

    private void CancelPendingExit()
    {
        _completionExitTimer.Stop();
        _completionExitPending = false;
    }

    private void CompletionExitTimerOnTick(object? sender, EventArgs e)
    {
        CancelPendingExit();
        _allowClose = true;
        Close();
    }

    private void CleanupTrayResources()
    {
        if (_cleanedUp)
            return;
        _cleanedUp = true;
        CancelPendingExit();
        _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        _viewModel.JobFinished -= ViewModelOnJobFinished;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip = null;
        _notifyIcon.Dispose();
        _trayMenu.Dispose();
        _trayIcon.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        var uri = new Uri(
            "pack://application:,,,/Assets/quickconvert.ico",
            UriKind.Absolute);
        var resource = System.Windows.Application.GetResourceStream(uri) ??
            throw new InvalidOperationException("Nie znaleziono ikony QuickConvert w zasobach aplikacji.");
        using var stream = resource.Stream;
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
