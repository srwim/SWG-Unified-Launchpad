using System.Windows;
using SwgLaunchpad.Core.Services;

namespace SwgLaunchpad.App;

public partial class SettingsWindow : Window
{
    private readonly string _serverDir;

    public SettingsWindow(string serverDir, string serverName)
    {
        InitializeComponent();
        _serverDir = serverDir;
        HeaderText.Text = serverName;

        var cfg = OptionsCfg.Load(_serverDir);
        var (w, h) = cfg.GetResolution();
        WidthBox.Text = w.ToString();
        HeightBox.Text = h.ToString();
        WindowedBox.IsChecked = cfg.GetWindowed();
        SkipIntroBox.IsChecked = cfg.GetSkipIntro();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthBox.Text, out var w) || !int.TryParse(HeightBox.Text, out var h) ||
            w < 640 || h < 480)
        {
            MessageBox.Show(this, "Enter a valid resolution (minimum 640x480).", "Game Settings");
            return;
        }

        var cfg = OptionsCfg.Load(_serverDir);
        cfg.SetResolution(w, h);
        cfg.SetWindowed(WindowedBox.IsChecked == true);
        cfg.SetSkipIntro(SkipIntroBox.IsChecked == true);
        cfg.Save();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
