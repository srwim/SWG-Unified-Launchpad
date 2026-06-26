using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using SwgLaunchpad.Core;
using SwgLaunchpad.Core.Models;
using SwgLaunchpad.Core.Services;

namespace SwgLaunchpad.App.ViewModels;

public sealed class ServerViewModel : ViewModelBase
{
    private readonly LaunchpadPaths _paths;
    private readonly ServerInstaller _installer;
    private readonly LaunchService _launchService;
    private readonly NewsService _newsService;
    private readonly PopulationService _populationService;
    private readonly GameFolderService _folderService = new();
    private readonly CredentialStore _credentialStore;
    private CancellationTokenSource? _cts;

    public ServerManifest Manifest { get; }

    /// <summary>True when the user added this server themselves (not from the moderated registry).</summary>
    public bool IsCustom { get; }

    /// <summary>The manifest source (URL/path) — used to remove a custom server.</summary>
    public string Source { get; }

    public event Action<ServerViewModel>? RemoveRequested;

    public ServerViewModel(ServerManifest manifest, string source, bool isCustom, LaunchpadPaths paths,
        ServerInstaller installer, LaunchService launchService,
        NewsService newsService, PopulationService populationService)
    {
        Manifest = manifest;
        Source = source;
        IsCustom = isCustom;
        _paths = paths;
        _installer = installer;
        _launchService = launchService;
        _newsService = newsService;
        _populationService = populationService;
        _credentialStore = new CredentialStore(paths);

        InstallOrUpdateCommand = new RelayCommand(async () => await InstallOrUpdateAsync(false), () => !IsBusy);
        VerifyCommand = new RelayCommand(async () => await InstallOrUpdateAsync(true), () => !IsBusy);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        PlayCommand = new RelayCommand(Play, () => !IsBusy && IsInstalled);
        SettingsCommand = new RelayCommand(OpenSettings, () => IsInstalled);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => IsInstalled);
        RemoveCommand = new RelayCommand(() => RemoveRequested?.Invoke(this), () => IsCustom && !IsBusy);
        WebsiteCommand = new RelayCommand(OpenWebsite, () => !string.IsNullOrWhiteSpace(Manifest.Website));
        DiscordCommand = new RelayCommand(() => OpenUrl(Manifest.Discord), () => !string.IsNullOrWhiteSpace(Manifest.Discord));
        AccountCommand = new RelayCommand(OpenAccount, () => RequiresLogin);

        _status = IsInstalled ? "Installed" : "Not installed";
        _ = RefreshLiveInfoAsync();
    }

    public string Name => Manifest.Name;
    public string Era => Manifest.Era;
    public string Description => Manifest.Description;
    public string ServerDir => _paths.ServerDir(Manifest.ServerId);
    public bool IsInstalled => _folderService.IsInstalled(ServerDir, Manifest.Client.Executable);
    public string InstallButtonText => IsInstalled ? "Update" : "Install";

    private string _status;
    public string Status { get => _status; set => Set(ref _status, value); }

    private double _progress;
    public double Progress { get => _progress; set => Set(ref _progress, value); }

    private string? _newsHeadline;
    public string? NewsHeadline { get => _newsHeadline; set => Set(ref _newsHeadline, value); }

    private string _populationText = "";
    public string PopulationText { get => _populationText; set => Set(ref _populationText, value); }

    private ServerOnlineState _onlineState = ServerOnlineState.Unknown;
    public ServerOnlineState OnlineState
    {
        get => _onlineState;
        set { if (Set(ref _onlineState, value)) { Raise(nameof(StatusDotBrush)); Raise(nameof(OnlineText)); } }
    }

    public Brush StatusDotBrush => OnlineState switch
    {
        ServerOnlineState.Online => (Brush)Application.Current.Resources["OnlineBrush"],
        ServerOnlineState.Offline => (Brush)Application.Current.Resources["OfflineBrush"],
        _ => (Brush)Application.Current.Resources["UnknownBrush"],
    };

    public string OnlineText => OnlineState switch
    {
        ServerOnlineState.Online => "Online",
        ServerOnlineState.Offline => "Offline",
        ServerOnlineState.Checking => "Checking…",
        _ => "",
    };

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (!Set(ref _isBusy, value)) return;
            InstallOrUpdateCommand.RaiseCanExecuteChanged();
            VerifyCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            PlayCommand.RaiseCanExecuteChanged();
            SettingsCommand.RaiseCanExecuteChanged();
            OpenFolderCommand.RaiseCanExecuteChanged();
            RemoveCommand.RaiseCanExecuteChanged();
        }
    }

    public RelayCommand InstallOrUpdateCommand { get; }
    public RelayCommand VerifyCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand PlayCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand WebsiteCommand { get; }
    public RelayCommand DiscordCommand { get; }
    public RelayCommand AccountCommand { get; }

    public bool RequiresLogin => Manifest.Auth.RequiresLauncherLogin;
    public bool HasAccount => _credentialStore.Has(Manifest.ServerId);
    public string LoggingIntoText => $"You are logging into: {Manifest.Name}";
    public string AccountButtonText => HasAccount ? "Account ✓" : "Account";

    /// <summary>Ping, population, and news — fire-and-forget, refreshed on load.</summary>
    public async Task RefreshLiveInfoAsync()
    {
        OnlineState = ServerOnlineState.Checking;
        var pingTask = ServerStatusService.CheckAsync(Manifest.Login.Host, Manifest.Login.Port);
        var popTask = _populationService.GetPopulationAsync(Manifest.StatusUrl);
        var newsTask = _newsService.GetLatestAsync(Manifest.NewsUrl);

        OnlineState = await pingTask;

        var pop = await popTask;
        PopulationText = pop is int n ? $"{n:N0} online" : "";

        var news = await newsTask;
        NewsHeadline = news is null ? null : $"News: {news.Title}";
    }

    private async Task InstallOrUpdateAsync(bool verifyAll)
    {
        IsBusy = true;
        Progress = 0;
        _cts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();
        long lastBytes = 0;
        double lastSeconds = 0, speedMbps = 0;
        try
        {
            var progress = new Progress<PatchProgress>(p =>
            {
                Progress = p.TotalBytes > 0 ? 100.0 * p.DownloadedBytes / p.TotalBytes : 0;
                var sec = sw.Elapsed.TotalSeconds;
                if (sec - lastSeconds >= 1.0)
                {
                    speedMbps = (p.DownloadedBytes - lastBytes) / (sec - lastSeconds) / (1024 * 1024);
                    lastBytes = p.DownloadedBytes;
                    lastSeconds = sec;
                }
                Status = $"Downloading {p.CompletedFiles}/{p.TotalFiles}  •  {FormatBytes(p.DownloadedBytes)} of {FormatBytes(p.TotalBytes)}" +
                         (speedMbps > 0.01 ? $"  •  {speedMbps:F1} MB/s" : "");
            });
            var status = new Progress<string>(s => Status = s);

            await _installer.InstallOrUpdateAsync(Manifest, verifyAll, progress, status, _cts.Token);
            Progress = 100;
            Raise(nameof(IsInstalled));
            Raise(nameof(InstallButtonText));
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled — partial files are re-verified on next update.";
        }
        catch (Exception ex)
        {
            Status = "Error: " + FirstMessage(ex);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }

    private void Play()
    {
        try
        {
            ServerCredentials? creds = null;
            if (RequiresLogin)
            {
                creds = _credentialStore.Load(Manifest.ServerId);
                if (creds is null)
                {
                    OpenAccount();
                    creds = _credentialStore.Load(Manifest.ServerId);
                    if (creds is null) { Status = "Sign in to play on this server."; return; }
                }
            }
            _launchService.Launch(Manifest.ServerId, ServerDir, Manifest, creds);
            var n = _launchService.RunningCount(Manifest.ServerId);
            Status = n > 1 ? $"Running ({n} instances)" : "Running";
        }
        catch (Exception ex)
        {
            Status = "Error: " + FirstMessage(ex);
        }
    }

    private void OpenAccount()
    {
        var window = new LoginWindow(_credentialStore, Manifest) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
        Raise(nameof(HasAccount));
        Raise(nameof(AccountButtonText));
    }

    private void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Status = "Error: " + FirstMessage(ex); }
    }

    private void OpenSettings()
    {
        try
        {
            var window = new SettingsWindow(ServerDir, Manifest.Name) { Owner = Application.Current.MainWindow };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            Status = "Error: " + FirstMessage(ex);
        }
    }

    private void OpenFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ServerDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = "Error: " + FirstMessage(ex);
        }
    }

    private void OpenWebsite()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Manifest.Website!) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = "Error: " + FirstMessage(ex);
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F0} KB",
        _ => $"{bytes} B",
    };

    private static string FirstMessage(Exception ex) =>
        ex is AggregateException agg && agg.InnerExceptions.Count > 0
            ? agg.InnerExceptions[0].Message
            : ex.Message;
}
