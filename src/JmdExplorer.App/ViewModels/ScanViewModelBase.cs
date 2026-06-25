using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;

namespace JmdExplorer.App.ViewModels;

/// <summary>
/// Base for pages that run a cancellable, progress-reporting scan against the session.
/// </summary>
public abstract partial class ScanViewModelBase : ObservableObject, IPageViewModel
{
    protected readonly AppSession Session;
    protected readonly IAppLogger Logger;

    private CancellationTokenSource? _cts;

    protected ScanViewModelBase(AppSession session, IAppLogger logger)
    {
        Session = session;
        Logger = logger;
        Session.FileLoaded += (_, _) => OnFileLoaded();
    }

    public abstract string Title { get; }
    public abstract string Glyph { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasFile => Session.Context is not null;

    protected virtual void OnFileLoaded()
    {
        OnPropertyChanged(nameof(HasFile));
        StatusMessage = null;
        Progress = 0;
    }

    /// <summary>Runs <paramref name="work"/> with a fresh cancellation token and progress.</summary>
    protected async Task RunScanAsync(Func<CancellationToken, IProgress<double>, Task> work, string runningMessage)
    {
        if (Session.Context is null)
        {
            StatusMessage = "Load a file first.";
            return;
        }
        if (IsBusy) return;

        _cts = new CancellationTokenSource();
        IsBusy = true;
        Progress = 0;
        StatusMessage = runningMessage;
        var progress = new Progress<double>(p => Progress = p * 100);

        try
        {
            await work(_cts.Token, progress);
            StatusMessage = _cts.IsCancellationRequested ? "Cancelled." : "Done.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            Logger.Error($"Scan failed in {Title}", ex);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
