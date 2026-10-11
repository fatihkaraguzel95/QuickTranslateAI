using System;
using System.Diagnostics;
using QuickTranslateAI.Services.Native;

namespace QuickTranslateAI.Services;

/// <summary>
/// Sistem genelinde iki hizli Ctrl+C hareketini algilar.
/// WH_KEYBOARD_LL kancasi kullanir; kanca cagrisi hizli olmali, bu yuzden olay
/// yalnizca tetiklenir ve isi cagiran taraf asenkron yapar.
/// </summary>
public sealed class DoubleCopyWatcher : IDisposable
{
    /// <summary>Henuz bir kopyalama gorulmedi. Cikarma islemi tasmasin diye -1 kullaniyoruz.</summary>
    private const long NoPreviousCopy = -1;

    private readonly NativeMethods.LowLevelKeyboardProc _callback;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private IntPtr _hook = IntPtr.Zero;
    private long _lastCopyMs = NoPreviousCopy;
    private bool _cKeyReleasedSinceLastCopy = true;
    private bool _disposed;

    /// <summary>Iki Ctrl+C arasindaki en uzun sure (ms).</summary>
    public int WindowMs { get; set; } = 500;

    /// <summary>Kanca kurulu kalir ama kapali durumda tetiklenmez.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Cift kopyalama algilandi.</summary>
    public event EventHandler? DoubleCopyDetected;

    public DoubleCopyWatcher()
    {
        // Delege alan referansi GC tarafindan toplanirsa kanca cokecektir.
        _callback = HookCallback;
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        var module = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _callback, module, 0);

        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Klavye kancasi kurulamadi. Baska bir uygulama engelliyor olabilir.");
        }
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    /// <summary>Tetikleme sonrasi sayaci sifirlar; ucuncu bir Ctrl+C yeni bir cift saymaz.</summary>
    public void ResetSequence()
    {
        _lastCopyMs = NoPreviousCopy;
        _cKeyReleasedSinceLastCopy = true;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        try
        {
            var message = (int)wParam;
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            if (data.vkCode == NativeMethods.VK_C)
            {
                if (message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP)
                {
                    _cKeyReleasedSinceLastCopy = true;
                }
                else if (message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN)
                {
                    OnCopyKeyDown();
                }
            }
        }
        catch (Exception)
        {
            // Kanca icinde atilan istisna tum sistemin klavyesini bozabilir; yut.
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void OnCopyKeyDown()
    {
        if (!NativeMethods.IsCtrlDown())
        {
            return;
        }

        // Tusu basili tutmak (otomatik tekrar) cift kopyalama sayilmamali:
        // iki basis arasinda bir KEYUP gormemiz gerekir.
        if (!_cKeyReleasedSinceLastCopy)
        {
            return;
        }

        _cKeyReleasedSinceLastCopy = false;

        var now = _clock.ElapsedMilliseconds;
        var elapsed = now - _lastCopyMs;
        _lastCopyMs = now;

        if (elapsed > WindowMs)
        {
            return;
        }

        ResetSequence();

        if (!IsEnabled)
        {
            return;
        }

        DoubleCopyDetected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
