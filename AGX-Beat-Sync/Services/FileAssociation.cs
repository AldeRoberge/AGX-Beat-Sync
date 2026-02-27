using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AGX_Beat_Sync.Services;

/// <summary>Registers .agxbs so double-clicking opens this app. Uses HKCU so no admin rights are required.</summary>
public static class FileAssociation
{
    private const string Extension = ".agxbs";
    private const string ProgId = "AGXBeatSync.Project";
    private const string AppName = "AGX Beat Sync";

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("Shell32.dll", SetLastError = false)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>Register .agxbs to open with the current executable. Returns true if registration succeeded.</summary>
    public static bool Register()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? AppContext.BaseDirectory + "AGX-Beat-Sync.exe";
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(exePath) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            exePath = Path.GetFullPath(exePath);
            string exeName = Path.GetFileName(exePath);
            string command = $"\"{exePath}\" \"%1\"";

            using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes", writable: true);
            if (classes == null) return false;

            // 1) Extension -> ProgId (default handler for double-click)
            using (var extKey = classes.CreateSubKey(Extension, writable: true))
            {
                if (extKey == null) return false;
                extKey.SetValue("", ProgId);
                using var openWithKey = extKey.CreateSubKey("OpenWithProgids", writable: true);
                openWithKey?.SetValue(ProgId, "", RegistryValueKind.String);
            }

            // 2) ProgId: friendly name, icon, shell open command
            using (var progKey = classes.CreateSubKey(ProgId, writable: true))
            {
                if (progKey == null) return false;
                progKey.SetValue("", AppName + " project");
            }
            using (var iconKey = classes.CreateSubKey(ProgId + @"\DefaultIcon", writable: true))
                iconKey?.SetValue("", $"\"{exePath}\",0");
            using (var openKey = classes.CreateSubKey(ProgId + @"\shell\open\command", writable: true))
            {
                if (openKey == null) return false;
                openKey.SetValue("", command);
            }

            // 3) Applications\<exe> so Windows can find this exe for "Open with" and suggest it
            using (var appCmdKey = classes.CreateSubKey(@"Applications\" + exeName + @"\shell\open\command", writable: true))
            {
                if (appCmdKey != null)
                    appCmdKey.SetValue("", command);
            }

            // Clear Explorer's per-extension override so our default is used (best-effort; may need admin)
            try
            {
                using var fileExts = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + Extension, writable: true);
                fileExts?.DeleteSubKey("UserChoice", throwOnMissingSubKey: false);
            }
            catch
            {
                // Ignore; UserChoice may be protected or missing
            }

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Remove .agxbs association (optional, for uninstall or "Don't use this app").</summary>
    public static void Unregister()
    {
        try
        {
            string exeName = "AGX-Beat-Sync.exe";
            try
            {
                string? exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exePath))
                    exeName = Path.GetFileName(exePath);
            }
            catch { /* ignore */ }

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes" + Extension, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ProgId, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\" + exeName, throwOnMissingSubKey: false);
        }
        catch
        {
            // Ignore
        }
    }
}
