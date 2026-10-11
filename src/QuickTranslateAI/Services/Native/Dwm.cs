using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using QuickTranslateAI.Models;

namespace QuickTranslateAI.Services.Native;

/// <summary>
/// Windows 11'in yerel cam efektlerini (Acrylic / Mica), yuvarlak koseleri ve koyu baslik
/// cubugunu uygular. Sahte blur degil; kompozisyonu DWM yapar.
/// </summary>
internal static class Dwm
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_ROUNDSMALL = 3;

    private const int DWMSBT_AUTO = 0;
    private const int DWMSBT_NONE = 1;
    private const int DWMSBT_MAINWINDOW = 2;      // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic - liquid glass
    private const int DWMSBT_TABBEDWINDOW = 4;    // Mica Alt

    /// <summary>Cam efektleri Windows 11 (build 22000+) gerektirir.</summary>
    internal static bool SupportsBackdrop =>
        Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;

    internal static bool SupportsRoundedCorners => SupportsBackdrop;

    /// <summary>
    /// Pencereye cam efektini uygular. Efektin gorunmesi icin pencere arka plani saydam
    /// olmali ve AllowsTransparency=false kalmali; WPF'in yazilim saydamligi DWM
    /// kompozisyonunu devre disi birakir.
    /// </summary>
    internal static void ApplyBackdrop(Window window, BackdropMode mode, bool dark, bool smallCorners = false)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ApplyDarkMode(handle, dark);
        ApplyCorners(handle, smallCorners);

        if (!SupportsBackdrop || mode == BackdropMode.Solid)
        {
            TrySetAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_NONE);
            return;
        }

        var backdrop = mode switch
        {
            BackdropMode.Acrylic => DWMSBT_TRANSIENTWINDOW,
            BackdropMode.Mica => DWMSBT_MAINWINDOW,
            _ => DWMSBT_AUTO
        };

        TrySetAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, backdrop);
    }

    internal static void ApplyDarkMode(IntPtr handle, bool dark)
    {
        TrySetAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, dark ? 1 : 0);
    }

    internal static void ApplyCorners(IntPtr handle, bool small)
    {
        if (!SupportsRoundedCorners)
        {
            return;
        }

        TrySetAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, small ? DWMWCP_ROUNDSMALL : DWMWCP_ROUND);
    }

    internal static void ApplyBorderColor(Window window, Color color)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !SupportsBackdrop)
        {
            return;
        }

        // DWM COLORREF bekler: 0x00BBGGRR
        var colorRef = color.R | (color.G << 8) | (color.B << 16);
        TrySetAttribute(handle, DWMWA_BORDER_COLOR, colorRef);
        TrySetAttribute(handle, DWMWA_CAPTION_COLOR, colorRef);
    }

    private static void TrySetAttribute(IntPtr handle, int attribute, int value)
    {
        try
        {
            _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // dwmapi.dll yoksa sessizce duz gorunumde kal.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
