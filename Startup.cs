using Microsoft.Win32;
using System.IO;

namespace Cally;

internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("Cally") is string;
        }
    }

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (!enable)
        {
            key.DeleteValue("Cally", false);
            return;
        }
        var path = Path.Combine(AppContext.BaseDirectory, "Cally.exe");
        if (!File.Exists(path)) throw new FileNotFoundException("Cally.exe was not found.");
        key.SetValue("Cally", $"\"{path}\"", RegistryValueKind.String);
    }
}
