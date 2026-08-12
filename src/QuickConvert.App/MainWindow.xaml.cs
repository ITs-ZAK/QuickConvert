using System.ComponentModel;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace QuickConvert.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        _viewModel.JobFinished += ViewModelOnJobFinished;

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "QuickConvert",
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreWindow();
        _notifyIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add("Otwórz", null, (_, _) => RestoreWindow());
        _notifyIcon.ContextMenuStrip.Items.Add("Zamknij", null, (_, _) =>
        {
            _allowClose = true;
            Close();
        });
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose && _viewModel.HasActiveJobs)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.Visible = true;
            _notifyIcon.ShowBalloonTip(
                1500,
                "QuickConvert",
                "Zadania nadal działają w tle.",
                Forms.ToolTipIcon.Info);
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HasActiveJobs) && _viewModel.HasActiveJobs)
            _notifyIcon.Visible = true;
        if (e.PropertyName == nameof(MainViewModel.HasActiveJobs) &&
            !_viewModel.HasActiveJobs &&
            !IsVisible)
        {
            _notifyIcon.ShowBalloonTip(
                2000,
                "QuickConvert",
                "Wszystkie zadania zostały zakończone.",
                Forms.ToolTipIcon.Info);
            _allowClose = true;
            Close();
        }
    }

    private void ViewModelOnJobFinished(QuickConvert.Core.Jobs.QueuedJob job)
    {
        _notifyIcon.Visible = true;
        var message = job.Status == QuickConvert.Core.Jobs.JobStatus.Completed
            ? $"Gotowe: {job.Description}"
            : $"Niepowodzenie: {job.Description}";
        _notifyIcon.ShowBalloonTip(2000, "QuickConvert", message, Forms.ToolTipIcon.Info);
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _notifyIcon.Visible = false;
    }
}
