using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using QuickTranslateAI.Models;
using QuickTranslateAI.Services;
using QuickTranslateAI.Services.Native;
using QuickTranslateAI.Services.Translation;
using QuickTranslateAI.Theming;
using QuickTranslateAI.Views;
using Forms = System.Windows.Forms;

namespace QuickTranslateAI;

/// <summary>
/// Uygulamanin calisma zamani: tepsi simgesi, kisayol dinleyicisi ve ceviri akisi.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly SettingsStore _store = new();
    private readonly TranslationCoordinator _coordinator = new();
    private readonly DoubleCopyWatcher _watcher = new();

    private Forms.NotifyIcon? _tray;
    private TranslationPopup? _popup;
    private SetupWindow? _settingsWindow;
    private CancellationTokenSource? _inflight;
    private AppSettings _settings = new();

    /// <summary>Onay bekleyen metin; kullanici "Gonder ve cevir" derse kullanilir.</summary>
    private string? _pendingText;

    /// <summary>Son cevrilen kaynak metin; "Tekrar dene" ve dil degisimi icin saklanir.</summary>
    private string? _lastSourceText;

    /// <summary>
    /// Pano okumasi asenkron oldugu icin, ikinci bir tetikleme ilk istek baslamadan
    /// once gelebilir. Bu bayrak kanca olayinin hemen ardindan kurulur.
    /// </summary>
    private bool _handling;

    private bool _busy;

    public void Start(bool silent)
    {
        _settings = _store.Load();
        Log.Info($"Başlatılıyor. Kurulum={_settings.SetupCompleted}, sağlayıcı={_settings.Provider}, hedef={_settings.TargetLanguage}, sessiz={silent}");
        ThemeManager.Current.Initialize(_settings);

        CreateTrayIcon();

        var hasKey = SecretStore.Has(_settings.Provider);

        if (!_settings.SetupCompleted || !hasKey)
        {
            // Kurulum tamamlanmadan dinlemeye baslamanin anlami yok.
            if (!RunSetup(settingsMode: false))
            {
                Application.Current.Shutdown();
                return;
            }
        }
        else if (!silent)
        {
            ShowBalloon("QuickTranslate AI çalışıyor", "Metni seç ve Ctrl+C tuşuna iki kez bas.");
        }

        StartWatcher();
    }

    // ===================== Tepsi =====================

    private void CreateTrayIcon()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "QuickTranslate AI"
        };

        _tray.MouseUp += OnTrayMouseUp;
        _tray.BalloonTipClicked += (_, _) => ShowSettings();
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        // Simge .exe'nin kendi kaynagindan okunur; yaninda ayri bir .ico dosyasi gerekmez.
        var path = Environment.ProcessPath;

        if (path is not null && File.Exists(path))
        {
            try
            {
                var extracted = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
            catch (Exception)
            {
                // Asagidaki varsayilan simgeye dus.
            }
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void OnTrayMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            ShowSettings();
            return;
        }

        if (e.Button == Forms.MouseButtons.Right)
        {
            ShowTrayMenu();
        }
    }

    private void ShowTrayMenu()
    {
        var menu = new ContextMenu
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint,
            StaysOpen = false
        };

        var toggle = new MenuItem
        {
            Header = _settings.Enabled ? "Dinleme etkin" : "Duraklatıldı",
            IsChecked = _settings.Enabled,
            IsCheckable = false
        };
        toggle.Click += (_, _) => SetEnabled(!_settings.Enabled);

        var settings = new MenuItem { Header = "Ayarlar…" };
        settings.Click += (_, _) => ShowSettings();

        var about = new MenuItem
        {
            Header = $"Hedef dil: {_settings.TargetLanguage}",
            IsEnabled = false
        };

        var quit = new MenuItem { Header = "Çıkış" };
        quit.Click += (_, _) => Application.Current.Shutdown();

        menu.Items.Add(toggle);
        menu.Items.Add(about);
        menu.Items.Add(new Separator());
        menu.Items.Add(settings);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);

        if (NativeMethods.GetCursorPos(out var cursor))
        {
            // AbsolutePoint cihazdan bagimsiz birim bekler; imlecin DPI olcegini uygula.
            var scale = NativeMethods.GetDpiScaleAt(cursor);
            menu.HorizontalOffset = cursor.X / scale;
            menu.VerticalOffset = cursor.Y / scale;
        }

        menu.IsOpen = true;

        // Menu bir pencereye ait olmadigi icin odagi kendimiz vermeliyiz,
        // aksi halde disari tiklandiginda kapanmaz.
        if (PresentationSource.FromVisual(menu) is HwndSource source)
        {
            NativeMethods.SetForegroundWindow(source.Handle);
        }
    }

    private void ShowBalloon(string title, string message)
    {
        _tray?.ShowBalloonTip(4000, title, message, Forms.ToolTipIcon.None);
    }

    // ===================== Ayarlar =====================

    public void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        RunSetup(settingsMode: true);
    }

    private bool RunSetup(bool settingsMode)
    {
        var window = new SetupWindow(_settings, settingsMode);
        _settingsWindow = window;

        bool? accepted;
        try
        {
            accepted = window.ShowDialog();
        }
        finally
        {
            _settingsWindow = null;
        }

        if (accepted != true || window.Result is null)
        {
            // Iptal edildiyse gorsel onizleme degisiklikleri geri alinir.
            ThemeManager.Current.Apply(_settings);
            return false;
        }

        ApplySettings(window.Result);
        return true;
    }

    private void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _store.Save(settings);

        ThemeManager.Current.Apply(settings);
        _coordinator.Invalidate();

        _watcher.WindowMs = settings.DoubleCopyWindowMs;
        _watcher.IsEnabled = settings.Enabled;

        try
        {
            StartupRegistration.Apply(settings.StartWithWindows);
        }
        catch (Exception ex)
        {
            ShowBalloon("Otomatik başlatma ayarlanamadı", ex.Message);
        }

        if (_tray is not null)
        {
            _tray.Text = settings.Enabled
                ? $"QuickTranslate AI — {settings.TargetLanguage}"
                : "QuickTranslate AI — duraklatıldı";
        }
    }

    private void SetEnabled(bool enabled)
    {
        var updated = _settings.Clone();
        updated.Enabled = enabled;
        ApplySettings(updated);

        ShowBalloon(
            enabled ? "Dinleme etkin" : "Duraklatıldı",
            enabled ? "Ctrl+C ×2 yeniden çalışıyor." : "Kısayol geçici olarak kapatıldı.");
    }

    // ===================== Kısayol =====================

    private void StartWatcher()
    {
        _watcher.WindowMs = _settings.DoubleCopyWindowMs;
        _watcher.IsEnabled = _settings.Enabled;
        _watcher.DoubleCopyDetected += OnDoubleCopy;

        try
        {
            _watcher.Start();
        }
        catch (InvalidOperationException ex)
        {
            Log.Error("Klavye kancası kurulamadı.", ex);
            MessageBox.Show(ex.Message, "QuickTranslate AI", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnDoubleCopy(object? sender, EventArgs e)
    {
        // Kanca is parcacigindan UI is parcacigina gecilir.
        Application.Current.Dispatcher.BeginInvoke(new Action(async () => await HandleDoubleCopyAsync()));
    }

    private async Task HandleDoubleCopyAsync()
    {
        if (_busy || _handling)
        {
            return;
        }

        _handling = true;

        try
        {
            var text = await ClipboardService.WaitForTextAsync().ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(text))
            {
                Log.Warn("Çift Ctrl+C algılandı ama panoda metin yok.");
                GetPopup().ShowError("Panoda çevrilecek metin yok.", canRetry: false);
                return;
            }

            Log.Info($"Çift Ctrl+C: {text.Length} karakter.");

            var needsConfirmation = _settings.ConfirmBeforeSend || text.Length > _settings.MaxCharacters;

            if (needsConfirmation)
            {
                _pendingText = text;
                GetPopup().ShowConfirm(text, text.Length);
                return;
            }

            await TranslateAsync(text, overrideLanguage: null).ConfigureAwait(true);
        }
        finally
        {
            _handling = false;
        }
    }

    private async Task TranslateAsync(string text, LanguageOption? overrideLanguage)
    {
        _busy = true;
        _lastSourceText = text;

        _inflight?.Cancel();
        _inflight = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        var popup = GetPopup();
        popup.ShowLoading();

        try
        {
            var outcome = await _coordinator
                .TranslateAsync(text, _settings, overrideLanguage?.DisplayName, _inflight.Token)
                .ConfigureAwait(true);

            PresentOutcome(outcome, overrideLanguage);
        }
        catch (TranslationException ex)
        {
            Log.Error("Çeviri başarısız.", ex);
            popup.ShowError(ex.Message, canRetry: true);
        }
        catch (OperationCanceledException)
        {
            popup.ShowError("Çeviri zaman aşımına uğradı.", canRetry: true);
        }
        catch (Exception ex)
        {
            Log.Error("Çeviri sırasında beklenmeyen hata.", ex);
            popup.ShowError($"Beklenmeyen hata: {ex.Message}", canRetry: true);
        }
        finally
        {
            _busy = false;
        }
    }

    private void PresentOutcome(TranslationOutcome outcome, LanguageOption? overrideLanguage)
    {
        var popup = GetPopup();

        var currentCode = overrideLanguage?.Code
                          ?? (outcome.TargetLanguage == _settings.FallbackLanguage
                              ? _settings.FallbackLanguageCode
                              : _settings.TargetLanguageCode)
                          ?? _settings.TargetLanguageCode;

        switch (_settings.Behavior)
        {
            case ResultBehavior.ClipboardAndToast:
                ClipboardService.TrySetText(outcome.TranslatedText);
                popup.ShowResult(outcome, LanguageCatalog.All, currentCode,
                    allowReplace: true, _settings.PopupAutoCloseSeconds, asToast: true);
                break;

            case ResultBehavior.ReplaceSelection:
                popup.ShowResult(outcome, LanguageCatalog.All, currentCode,
                    allowReplace: false, _settings.PopupAutoCloseSeconds, asToast: true);
                ReplaceSelection(outcome.TranslatedText);
                break;

            default:
                popup.ShowResult(outcome, LanguageCatalog.All, currentCode,
                    allowReplace: true, _settings.PopupAutoCloseSeconds, asToast: false);
                break;
        }
    }

    /// <summary>
    /// Ceviriyi panoya alir ve on plandaki uygulamaya Ctrl+V gonderir.
    /// Istege bagli olarak ozgun pano icerigi geri yuklenir.
    /// </summary>
    private void ReplaceSelection(string translation)
    {
        var original = ClipboardService.TryGetText();

        if (!ClipboardService.TrySetText(translation))
        {
            GetPopup().ShowError("Pano kilitli olduğu için çeviri yazılamadı.", canRetry: true);
            return;
        }

        // Kullanici Ctrl'yi birakmadiysa yapistirma baska bir kisayola donusur.
        NativeMethods.ReleaseCtrl();
        NativeMethods.SendCtrlV();

        if (!_settings.RestoreClipboardAfterReplace || original is null)
        {
            return;
        }

        // Yapistirmanin tamamlanmasi icin kisa bir sure beklenir.
        _ = Task.Delay(400).ContinueWith(
            _ => ClipboardService.TrySetText(original),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ===================== Popup =====================

    private TranslationPopup GetPopup()
    {
        if (_popup is not null)
        {
            return _popup;
        }

        _popup = new TranslationPopup();

        _popup.SettingsRequested += (_, _) => ShowSettings();

        _popup.RetryRequested += async (_, _) =>
        {
            if (_lastSourceText is not null)
            {
                await TranslateAsync(_lastSourceText, overrideLanguage: null).ConfigureAwait(true);
            }
        };

        _popup.RetranslateRequested += async (_, language) =>
        {
            if (_lastSourceText is not null)
            {
                await TranslateAsync(_lastSourceText, language).ConfigureAwait(true);
            }
        };

        _popup.ConfirmAccepted += async (_, _) =>
        {
            var text = _pendingText;
            _pendingText = null;

            if (text is not null)
            {
                await TranslateAsync(text, overrideLanguage: null).ConfigureAwait(true);
            }
        };

        _popup.ConfirmRejected += (_, _) => _pendingText = null;

        _popup.ReplaceRequested += (_, translation) => ReplaceSelection(translation);

        return _popup;
    }

    public void Dispose()
    {
        _watcher.DoubleCopyDetected -= OnDoubleCopy;
        _watcher.Dispose();
        _inflight?.Cancel();
        _inflight?.Dispose();
        _coordinator.Dispose();

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
    }
}
