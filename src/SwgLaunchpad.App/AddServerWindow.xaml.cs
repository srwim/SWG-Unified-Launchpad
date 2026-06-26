using System.Windows;
using System.Windows.Input;

namespace SwgLaunchpad.App;

public partial class AddServerWindow : Window
{
    public string? ManifestSource { get; private set; }

    public AddServerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => SourceBox.Focus();
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        ManifestSource = SourceBox.Text;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnSourceKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnAdd(sender, e);
    }
}
