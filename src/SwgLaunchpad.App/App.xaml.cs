using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace SwgLaunchpad.App;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    protected override void OnStartup(StartupEventArgs e)
    {
        // One launcher at a time: two instances patching the same folders
        // would corrupt installs. (Multiple *game* clients are fine.)
        _singleInstanceMutex = new Mutex(true, @"Local\SwgUnifiedLaunchpad", out bool createdNew);
        if (!createdNew)
        {
            // Bring the existing window to the front instead of silently dying.
            var existing = Process.GetProcessesByName("SwgLaunchpad").FirstOrDefault();
            if (existing is not null)
            {
                ShowWindow(existing.MainWindowHandle, 9 /* SW_RESTORE */);
                SetForegroundWindow(existing.MainWindowHandle);
            }
            Shutdown();
            return;
        }

        base.OnStartup(e);
        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
