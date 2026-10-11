using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using QuickTranslateAI.Models;
using QuickTranslateAI.Services.Native;

namespace QuickTranslateAI.Theming;

/// <summary>
/// Paleti degistirir, Windows'un acik/koyu ve vurgu rengi ayarini takip eder ve
/// her pencereye DWM cam efektini uygular.
/// </summary>
public sealed class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string DwmKey = @"Software\Microsoft\Windows\DWM";

    private static readonly Uri LightPalette = new("/Theming/Palette.Light.xaml", UriKind.Relative);
    private static readonly Uri DarkPalette = new("/Theming/Palette.Dark.xaml", UriKind.Relative);

    private readonly List<RegisteredWindow> _windows = [];

    private ResourceDictionary? _paletteDictionary;
    private AppSettings _settings = new();

    public static ThemeManager Current { get; } = new();

    public bool IsDark { get; private set; }

    public event EventHandler? ThemeChanged;

    public void Initialize(AppSettings settings)
    {
        _settings = settings;

        _paletteDictionary = new ResourceDictionary();
        Application.Current.Resources.MergedDictionaries.Insert(0, _paletteDictionary);

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Apply(settings);
    }

    /// <summary>Ayar degistiginde veya Windows temasi degistiginde cagrilir.</summary>
    public void Apply(AppSettings settings)
    {
        _settings = settings;

        var dark = settings.Theme switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            _ => IsSystemDark()
        };

        IsDark = dark;

        if (_paletteDictionary is not null)
        {
            try
            {
                _paletteDictionary.Source = dark ? DarkPalette : LightPalette;
            }
            catch (Exception ex)
            {
                Services.Log.Error("Palet sözlüğü yüklenemedi.", ex);
            }
        }

        Services.Log.Info(
            $"Tema uygulandı: dark={dark}, backdrop={settings.Backdrop}, " +
            $"palet anahtarları={_paletteDictionary?.Count ?? -1}, " +
            $"App.Surface={(Application.Current.Resources["App.Surface"] is null ? "YOK" : "var")}");

        if (settings.UseSystemAccent)
        {
            ApplySystemAccent(dark);
        }
        else
        {
            RemoveAccentOverrides();
        }

        foreach (var entry in LiveWindows())
        {
            ApplyToWindow(entry.Window, entry.SmallCorners);
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pencereyi tema degisimlerine abone eder ve cam efektini hemen uygular.</summary>
    public void Register(Window window, bool smallCorners = false)
    {
        _windows.Add(new RegisteredWindow(new WeakReference<Window>(window), smallCorners));

        if (window.IsInitialized)
        {
            ApplyToWindow(window, smallCorners);
        }

        window.SourceInitialized += (_, _) => ApplyToWindow(window, smallCorners);
    }

    private void ApplyToWindow(Window window, bool smallCorners)
    {
        Dwm.ApplyBackdrop(window, _settings.Backdrop, IsDark, smallCorners);

        // Solid modda DWM arka plani kapanir; pencerenin kendi opak zemini devreye girer.
        window.Background = _settings.Backdrop == BackdropMode.Solid || !Dwm.SupportsBackdrop
            ? (Brush)Application.Current.Resources["App.Surface"]
            : Brushes.Transparent;
    }

    private List<(Window Window, bool SmallCorners)> LiveWindows()
    {
        var live = new List<(Window, bool)>();

        for (var i = _windows.Count - 1; i >= 0; i--)
        {
            if (_windows[i].Reference.TryGetTarget(out var window))
            {
                live.Add((window, _windows[i].SmallCorners));
            }
            else
            {
                _windows.RemoveAt(i);
            }
        }

        return live;
    }

    private sealed record RegisteredWindow(WeakReference<Window> Reference, bool SmallCorners);

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle
            or UserPreferenceCategory.Color))
        {
            return;
        }

        // Kayit defteri degeri olay aninda henuz guncellenmemis olabilir.
        Application.Current?.Dispatcher.BeginInvoke(() => Apply(_settings));
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Windows kisisellestirme vurgu rengini okur ve tema firca degerlerinin uzerine yazar.
    /// Okunamazsa paletin kendi vurgu rengi kalir.
    /// </summary>
    private static void ApplySystemAccent(bool dark)
    {
        var accent = ReadSystemAccent();
        if (accent is null)
        {
            return;
        }

        var color = accent.Value;

        // Koyu temada sistem vurgusu genelde cok koyu kalir; okunabilirlik icin acilir.
        var baseColor = dark ? Lighten(color, 0.28) : color;

        var resources = Application.Current.Resources;
        resources["App.Accent"] = new SolidColorBrush(baseColor);
        resources["App.AccentHover"] = new SolidColorBrush(Lighten(baseColor, 0.12));
        resources["App.AccentPressed"] = new SolidColorBrush(Darken(baseColor, 0.14));
        resources["App.AccentSoft"] = new SolidColorBrush(WithAlpha(baseColor, dark ? (byte)0x33 : (byte)0x1A));
        resources["App.AccentColor"] = baseColor;

        // Vurgu uzerindeki yazi rengi parlaklik ile secilir, boylece her accent'te okunur kalir.
        resources["App.OnAccent"] = new SolidColorBrush(
            RelativeLuminance(baseColor) > 0.5 ? Color.FromRgb(0x06, 0x12, 0x1F) : Colors.White);
    }

    /// <summary>Sistem vurgusu kapatildiginda paletin kendi degerlerine geri donulur.</summary>
    private static void RemoveAccentOverrides()
    {
        var resources = Application.Current.Resources;

        foreach (var key in new[] { "App.Accent", "App.AccentHover", "App.AccentPressed", "App.AccentSoft", "App.AccentColor", "App.OnAccent" })
        {
            if (resources.Contains(key))
            {
                resources.Remove(key);
            }
        }
    }

    private static Color? ReadSystemAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
            if (key?.GetValue("AccentColor") is int raw)
            {
                // DWM degeri 0xAABBGGRR duzeninde saklanir.
                var bytes = BitConverter.GetBytes(raw);
                return Color.FromRgb(bytes[0], bytes[1], bytes[2]);
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    private static Color Lighten(Color color, double amount) => Color.FromRgb(
        (byte)Math.Clamp(color.R + (255 - color.R) * amount, 0, 255),
        (byte)Math.Clamp(color.G + (255 - color.G) * amount, 0, 255),
        (byte)Math.Clamp(color.B + (255 - color.B) * amount, 0, 255));

    private static Color Darken(Color color, double amount) => Color.FromRgb(
        (byte)Math.Clamp(color.R * (1 - amount), 0, 255),
        (byte)Math.Clamp(color.G * (1 - amount), 0, 255),
        (byte)Math.Clamp(color.B * (1 - amount), 0, 255));

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static double RelativeLuminance(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
}
