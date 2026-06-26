using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using SwgLaunchpad.App.ViewModels;

namespace SwgLaunchpad.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _liveRefresh;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        // Remember window size between sessions.
        Width = _vm.Settings.WindowWidth;
        Height = _vm.Settings.WindowHeight;

        // Keep online status + population fresh without a manual refresh.
        _liveRefresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _liveRefresh.Tick += (_, _) =>
        {
            foreach (var server in _vm.Servers)
                _ = server.RefreshLiveInfoAsync();
        };
        _liveRefresh.Start();

        Loaded += async (_, _) => await _vm.LoadAsync();

        Closing += (_, _) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _vm.Settings.WindowWidth = Width;
                _vm.Settings.WindowHeight = Height;
                _vm.Settings.Save(_vm.SettingsFile);
            }
        };
    }

    private void OnBrowseBaseClient(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select your Star Wars Galaxies 14.1 client folder",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
            _vm.BaseClientPath = dialog.FolderName;
    }
}
