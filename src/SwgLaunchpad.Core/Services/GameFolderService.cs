using System.Runtime.InteropServices;

namespace SwgLaunchpad.Core.Services;

/// <summary>
/// Builds a server's game folder by hard-linking every file of the shared
/// pristine base client into it (zero extra disk on NTFS). The patcher then
/// overlays the server's own files. Falls back to copying when hard links
/// aren't possible (non-NTFS, different volume, non-Windows).
/// </summary>
public sealed class GameFolderService
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    /// <summary>
    /// Ensures serverDir contains every base-client file (as a hard link or
    /// copy). Existing files are left alone — server-patched files are never
    /// clobbered by a re-run.
    /// </summary>
    public void PrepareServerFolder(string baseClientDir, string serverDir, IProgress<string>? progress = null)
    {
        if (!Directory.Exists(baseClientDir) || !Directory.EnumerateFileSystemEntries(baseClientDir).Any())
            throw new DirectoryNotFoundException(
                $"Base client folder '{baseClientDir}' is missing or empty. Point the launcher at a clean SWG install first.");

        Directory.CreateDirectory(serverDir);
        var baseRoot = Path.GetFullPath(baseClientDir);

        foreach (var srcPath in Directory.EnumerateFiles(baseRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(baseRoot, srcPath);
            var destPath = Path.Combine(serverDir, relative);
            if (File.Exists(destPath)) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            progress?.Report(relative);

            if (!TryHardLink(destPath, srcPath))
                File.Copy(srcPath, destPath);
        }
    }

    private static bool TryHardLink(string newLink, string existingFile)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        return CreateHardLink(newLink, existingFile, IntPtr.Zero);
    }

    public bool IsInstalled(string serverDir, string executable) =>
        File.Exists(Path.Combine(serverDir, executable));
}
