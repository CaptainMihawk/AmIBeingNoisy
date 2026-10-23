using Microsoft.Win32;

namespace LoudEnv.Tray;

public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LoudEnv";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        if (key?.GetValue(ValueName) is not string value)
            return false;

        return string.Equals(value.Trim('"'), CurrentExecutable(), StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath)
                        ?? throw new InvalidOperationException(
                            "Não foi possível abrir a chave de inicialização do Windows.");

        if (enabled)
            key.SetValue(ValueName, $"\"{CurrentExecutable()}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string CurrentExecutable() =>
        Environment.ProcessPath ?? Application.ExecutablePath;
}
