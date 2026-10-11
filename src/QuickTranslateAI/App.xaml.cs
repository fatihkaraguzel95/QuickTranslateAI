using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace QuickTranslateAI;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\QuickTranslateAI.SingleInstance";
    private const string ShowSettingsEventName = @"Local\QuickTranslateAI.ShowSettings";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSettingsSignal;
    private RegisteredWaitHandle? _showSettingsWait;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);

        if (!isFirstInstance)
        {
            // Zaten calisan ornegin ayar penceresini one getir ve bu ornekten cik.
            if (EventWaitHandle.TryOpenExisting(ShowSettingsEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }

            Shutdown();
            return;
        }

        _showSettingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName);
        _showSettingsWait = ThreadPool.RegisterWaitForSingleObject(
            _showSettingsSignal,
            (_, _) => Dispatcher.BeginInvoke(() => _controller?.ShowSettings()),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);

        DispatcherUnhandledException += OnUnhandledException;

        var silent = Array.Exists(e.Args, arg => arg.Equals("--silent", StringComparison.OrdinalIgnoreCase));

        _controller = new AppController();
        _controller.Start(silent);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Beklenmeyen bir hata arka plan uygulamasini sessizce oldurmesin.
        MessageBox.Show(
            $"QuickTranslate AI beklenmeyen bir hatayla karsilasti:\n\n{e.Exception.Message}",
            "QuickTranslate AI",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showSettingsWait?.Unregister(null);
        _showSettingsSignal?.Dispose();
        _controller?.Dispose();

        if (_instanceMutex is not null)
        {
            try
            {
                _instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex bu ornege ait degilse yok sayilir.
            }

            _instanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
