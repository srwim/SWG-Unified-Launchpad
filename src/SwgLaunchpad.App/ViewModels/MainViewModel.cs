using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SwgLaunchpad.Core;
using SwgLaunchpad.Core.Models;
using SwgLaunchpad.Core.Services;

namespace SwgLaunchpad.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly LaunchpadPaths _paths = LaunchpadPaths.Default();
    private readonly RegistryClient _registryClient = new();
    private readonly LaunchService _launchService = new();
    private readonly NewsService _newsService = new();
    private readonly PopulationService _populationService = new();
    private readonly ServerInstaller _installer;
    private readonly List<ServerViewModel> _allServers = new();
    private LauncherSettings _settings;

    public ObservableCollection<ServerViewModel> Servers { get; } = new();
    public ObservableCollection<string> EraFilters { get; } = new() { "All eras" };

    public MainViewModel()
    {
        _installer = new ServerInstaller(_paths, _registryClient, new PatchService());
        _settings = LauncherSettings.Load(_paths.SettingsFile);
        _baseClientPath = Directory.Exists(_paths.BaseClientDir) ? _paths.BaseClientDir : (_settings.BaseClientSource ?? "");
        _registrySource = _settings.RegistrySource;
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !IsLoading);
        SaveBasePathCommand = new RelayCommand(SaveBasePath);
        AddServerCommand = new RelayCommand(AddServer);
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
    }

    public LauncherSettings Settings => _settings;
    public string SettingsFile => _paths.SettingsFile;

    private string _registrySource;
    public string RegistrySource
    {
        get => _registrySource;
        set { if (Set(ref _registrySource, value)) { _settings.RegistrySource = value; _settings.Save(_paths.SettingsFile); } }
    }

    private string _baseClientPath;
    /// <summary>Folder containing a clean SWG 14.1 install; imported as the shared base.</summary>
    public string BaseClientPath { get => _baseClientPath; set => Set(ref _baseClientPath, value); }

    public bool BaseClientReady => Directory.Exists(_paths.BaseClientDir) &&
                                   Directory.EnumerateFiles(_paths.BaseClientDir).Any();

    private string _statusBar = "";
    public string StatusBar { get => _statusBar; set => Set(ref _statusBar, value); }

    private string _selectedEra = "All eras";
    public string SelectedEra
    {
        get => _selectedEra;
        set { if (Set(ref _selectedEra, value)) ApplyFilter(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (Set(ref _isLoading, value)) RefreshCommand.RaiseCanExecuteChanged(); }
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand SaveBasePathCommand { get; }
    public RelayCommand AddServerCommand { get; }
    public RelayCommand CheckForUpdatesCommand { get; }

    public string LauncherVersionLabel =>
        $"Launcher v{UpdateService.CurrentVersion()}  ·  r/swg community project";

    public async Task LoadAsync()
    {
        IsLoading = true;
        _allServers.Clear();
        Servers.Clear();
        StatusBar = "Loading server registry…";
        int failed = 0;

        // 1. Moderated registry (online), falling back to the registry bundled
        //    with the launcher, then per-entry to bundled manifests.
        var bundledRegistry = Path.Combine(AppContext.BaseDirectory, "registry", "registry.json");
        var bundledManifests = Path.Combine(AppContext.BaseDirectory, "registry", "manifests");
        try
        {
            ServerRegistry registry;
            try
            {
                registry = await _registryClient.GetRegistryAsync(RegistrySource);
            }
            catch when (File.Exists(bundledRegistry))
            {
                registry = await _registryClient.GetRegistryAsync(bundledRegistry);
                StatusBar = "Online registry unreachable — using the server list bundled with the launcher.";
            }

            foreach (var entry in registry.Servers.Where(s => s.Status == "verified"))
            {
                var vm = await TryLoadServerAsync(entry.ManifestUrl, isCustom: false, expectedId: entry.ServerId);
                if (vm is null)
                {
                    var local = Path.Combine(bundledManifests, entry.ServerId + ".json");
                    if (File.Exists(local))
                        vm = await TryLoadServerAsync(local, isCustom: false, expectedId: entry.ServerId);
                }
                if (vm is null) failed++;
            }
        }
        catch (Exception ex)
        {
            StatusBar = "Could not load registry: " + ex.Message;
        }

        // 2. User-added servers
        foreach (var source in _settings.CustomManifestSources.ToList())
        {
            var vm = await TryLoadServerAsync(source, isCustom: true);
            if (vm is null) failed++;
        }

        RebuildEraFilters();
        ApplyFilter();
        StatusBar = $"{_allServers.Count} server(s)" +
                    (failed > 0 ? $" — {failed} unreachable" : "") +
                    DiskUsageSuffix();
        IsLoading = false;
    }

    private async Task<ServerViewModel?> TryLoadServerAsync(string source, bool isCustom, string? expectedId = null)
    {
        try
        {
            var manifest = await _registryClient.GetManifestAsync(source);
            if (expectedId is not null && manifest.ServerId != expectedId) return null; // identity mismatch
            if (_allServers.Any(s => s.Manifest.ServerId == manifest.ServerId)) return null; // duplicate
            var vm = new ServerViewModel(manifest, source, isCustom, _paths, _installer,
                _launchService, _newsService, _populationService);
            vm.RemoveRequested += RemoveCustomServer;
            _allServers.Add(vm);
            return vm;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Add a server from a manifest URL the user pasted (or a local file for testing).</summary>
    private async void AddServer()
    {
        var dialog = new AddServerWindow { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ManifestSource)) return;

        var source = dialog.ManifestSource.Trim();
        if (_settings.CustomManifestSources.Contains(source, StringComparer.OrdinalIgnoreCase))
        {
            StatusBar = "That server is already in your list.";
            return;
        }

        StatusBar = "Checking manifest…";
        var vm = await TryLoadServerAsync(source, isCustom: true);
        if (vm is null)
        {
            StatusBar = "Could not load a valid server manifest from that address. " +
                        "It must be a manifest.json as described in the submission guide.";
            return;
        }

        _settings.CustomManifestSources.Add(source);
        _settings.Save(_paths.SettingsFile);
        RebuildEraFilters();
        ApplyFilter();
        StatusBar = $"Added {vm.Name}.";
    }

    private void RemoveCustomServer(ServerViewModel vm)
    {
        _settings.CustomManifestSources.RemoveAll(s => string.Equals(s, vm.Source, StringComparison.OrdinalIgnoreCase));
        _settings.Save(_paths.SettingsFile);
        _allServers.Remove(vm);
        Servers.Remove(vm);
        StatusBar = $"Removed {vm.Name}. Installed files were kept ({vm.ServerDir}).";
    }

    private void RebuildEraFilters()
    {
        var selected = SelectedEra;
        EraFilters.Clear();
        EraFilters.Add("All eras");
        foreach (var era in _allServers.Select(s => s.Era).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().OrderBy(e => e))
            EraFilters.Add(era);
        _selectedEra = EraFilters.Contains(selected) ? selected : "All eras";
        Raise(nameof(SelectedEra));
    }

    private void ApplyFilter()
    {
        Servers.Clear();
        foreach (var vm in _allServers.Where(s => SelectedEra == "All eras" || s.Era == SelectedEra)
                                      .OrderByDescending(s => s.IsInstalled).ThenBy(s => s.Name))
            Servers.Add(vm);
    }

    private async Task CheckForUpdatesAsync()
    {
        StatusBar = "Checking for updates…";
        var info = await UpdateService.CheckAsync();
        if (info is null)
        {
            StatusBar = "Could not reach GitHub to check for updates.";
            return;
        }
        if (info.UpdateAvailable)
        {
            var msg = $"A new version is available!\n\nCurrent: v{info.CurrentVersion}\nLatest:  v{info.LatestVersion}" +
                      (string.IsNullOrWhiteSpace(info.ReleaseNotes) ? "" : $"\n\n{info.ReleaseNotes}") +
                      "\n\nOpen the download page?";
            if (MessageBox.Show(msg, "Update available", MessageBoxButton.YesNo,
                    MessageBoxImage.Information) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(info.DownloadUrl) { UseShellExecute = true });
        }
        else
        {
            StatusBar = $"You are on the latest version (v{info.CurrentVersion}).";
        }
    }

    private string DiskUsageSuffix()
    {
        try
        {
            if (!Directory.Exists(_paths.InstallRoot)) return "";
            // Hard links share storage; counting servers\ alone still gives a useful upper bound.
            long bytes = Directory.EnumerateFiles(_paths.ServersDir, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
            return bytes > 0 ? $"  •  {bytes / (double)(1L << 30):F1} GB in game folders" : "";
        }
        catch { return ""; }
    }

    /// <summary>
    /// Imports the user's clean SWG client as the shared base (one-time copy
    /// into {InstallRoot}\base so future hard links live on one volume).
    /// </summary>
    private void SaveBasePath()
    {
        try
        {
            var src = Path.GetFullPath(BaseClientPath);
            var dst = Path.GetFullPath(_paths.BaseClientDir);
            if (!Directory.Exists(src)) { StatusBar = "Base client folder not found."; return; }

            // Sanity check: a real SWG install has .tre archives.
            if (!Directory.EnumerateFiles(src, "*.tre").Any())
            {
                StatusBar = "That folder doesn't look like a SWG client (no .tre files found).";
                return;
            }

            if (!string.Equals(src, dst, StringComparison.OrdinalIgnoreCase))
            {
                StatusBar = "Importing base client (one-time copy)…";
                foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(src, file);
                    var dest = Path.Combine(dst, rel);
                    if (File.Exists(dest)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest);
                }
            }
            _settings.BaseClientSource = src;
            _settings.Save(_paths.SettingsFile);
            BaseClientPath = dst;
            Raise(nameof(BaseClientReady));
            StatusBar = "Base client ready.";
        }
        catch (Exception ex)
        {
            StatusBar = "Base import failed: " + ex.Message;
        }
    }
}
