using System;
using System.IO;
using System.Text;

namespace QuickTranslateAI.Services;

/// <summary>
/// Basit dosya gunlugu. Arka planda calisan bir uygulamada konsol olmadigi icin
/// sorunlari ancak boyle izleyebiliyoruz. Dosya belli bir boyutu asinca sifirlanir.
/// </summary>
public static class Log
{
    private const long MaxBytes = 512 * 1024;

    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(SettingsStore.Directory, "quicktranslate.log");

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}\n{exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsStore.Directory);

                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Delete(FilePath);
                }

                File.AppendAllText(
                    FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Gunluk yazilamiyorsa uygulama yine de calismaya devam etmeli.
        }
    }
}
