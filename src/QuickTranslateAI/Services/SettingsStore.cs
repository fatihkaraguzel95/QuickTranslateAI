using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuickTranslateAI.Models;

namespace QuickTranslateAI.Services;

/// <summary>
/// Ayarları %APPDATA%\QuickTranslateAI\settings.json içinde tutar.
/// API anahtarı burada DEĞİL, Windows Credential Manager'da saklanır (bkz. <see cref="SecretStore"/>).
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickTranslateAI");

    public static string FilePath { get; } = Path.Combine(Directory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Bozuk ayar dosyası uygulamayı açılışta kilitlemesin.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);

        // Yarı yazılmış dosya bırakmamak için geçici dosyaya yazıp yer değiştiriyoruz.
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }
}
