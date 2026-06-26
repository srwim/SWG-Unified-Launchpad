using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using SwgLaunchpad.Core.Models;
using SwgLaunchpad.Core.Services;

namespace SwgLaunchpad.App;

public partial class LoginWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly CredentialStore _store;
    private readonly ServerManifest _manifest;

    public bool Saved { get; private set; }

    public LoginWindow(CredentialStore store, ServerManifest manifest)
    {
        InitializeComponent();
        _store = store;
        _manifest = manifest;
        HeaderText.Text = manifest.Name;

        var existing = store.Load(manifest.ServerId);
        if (existing is not null)
        {
            UsernameBox.Text = existing.Username;
            PasswordBox.Password = existing.Password;
            StatusText.Text = "A saved account is on file for this server.";
        }
        ForgotLink.Visibility = string.IsNullOrWhiteSpace(manifest.Auth.PasswordResetUrl)
            ? Visibility.Collapsed : Visibility.Visible;
        RegisterLink.Visibility = string.IsNullOrWhiteSpace(manifest.Auth.RegisterUrl)
            ? Visibility.Collapsed : Visibility.Visible;
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var user = UsernameBox.Text.Trim();
        var pass = PasswordBox.Password;
        if (user.Length == 0 || pass.Length == 0)
        {
            StatusText.Text = "Enter a username and password.";
            return;
        }

        var creds = new ServerCredentials(user, pass);

        StatusText.Text = "Checking credentials…";
        var valid = await AccountValidator.ValidateAsync(Http, _manifest.Auth.LoginUrl, creds);
        if (valid == false)
        {
            StatusText.Text = "The server rejected those credentials. Check username/password.";
            return;
        }

        _store.Save(_manifest.ServerId, creds);
        Saved = true;
        DialogResult = true;
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _store.Clear(_manifest.ServerId);
        UsernameBox.Text = "";
        PasswordBox.Password = "";
        StatusText.Text = "Saved account removed.";
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnForgot(object sender, MouseButtonEventArgs e) => OpenUrl(_manifest.Auth.PasswordResetUrl);
    private void OnRegister(object sender, MouseButtonEventArgs e) => OpenUrl(_manifest.Auth.RegisterUrl);

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { /* ignored */ }
    }
}
