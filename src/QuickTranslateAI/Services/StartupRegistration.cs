using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace QuickTranslateAI.Services;

/// <summary>Windows oturum açılışında otomatik başlatma (HKCU \Run anahtarı).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickTranslateAI";

    private static string ExecutablePath =>
        Process.GetCurrentProcess().MainModule?.FileName
        ?? Environment.ProcessPath
        ?? throw new InvalidOperationException("Uygulama yolu belirlenemedi.");

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value.Contains("QuickTranslateAI", StringComparison.OrdinalIgnoreCase);
    }

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
                        ?? throw new InvalidOperationException("Kayıt defteri Run anahtarı açılamadı.");

        if (enabled)
        {
            // --silent: açılışta ayar penceresi gösterilmez, yalnızca tepsiye yerleşir.
            key.SetValue(ValueName, $"\"{ExecutablePath}\" --silent");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
