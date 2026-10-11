using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslateAI.Services.Native;

namespace QuickTranslateAI.Services;

/// <summary>
/// Pano erisimi. Pano kilitli olabildigi icin her islem yeniden denenir;
/// ayrica ikinci Ctrl+C sonrasi kaynak uygulamanin panoyu doldurmasi beklenir.
/// </summary>
public static class ClipboardService
{
    private const int RetryCount = 10;
    private const int RetryDelayMs = 40;

    /// <summary>
    /// Pano icerigi degisene kadar (en fazla <paramref name="timeoutMs"/>) bekler ve metni dondurur.
    /// Kaynak uygulama panoyu doldurmasa bile mevcut icerik dondurulur.
    /// </summary>
    public static async Task<string?> WaitForTextAsync(int timeoutMs = 400)
    {
        var startSequence = NativeMethods.GetClipboardSequenceNumber();
        var waited = 0;

        while (waited < timeoutMs)
        {
            await Task.Delay(RetryDelayMs).ConfigureAwait(true);
            waited += RetryDelayMs;

            if (NativeMethods.GetClipboardSequenceNumber() != startSequence)
            {
                break;
            }
        }

        return TryGetText();
    }

    public static string? TryGetText()
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (Exception)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        return null;
    }

    public static bool TrySetText(string text)
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                // copy: true -> uygulama kapandiktan sonra da panoda kalir.
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (Exception)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        return false;
    }
}
