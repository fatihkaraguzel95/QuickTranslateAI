using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using QuickTranslateAI.Models;
using QuickTranslateAI.Services.Native;
using QuickTranslateAI.Theming;
using Forms = System.Windows.Forms;

namespace QuickTranslateAI.Views;

/// <summary>
/// Imlecin yaninda acilan cam sonuc penceresi. Tek ornek olarak yeniden kullanilir:
/// her ceviride yeni pencere acmak yerine icerik degistirilir.
/// </summary>
public partial class TranslationPopup : Window
{
    private const int EdgeMargin = 12;
    private const int CursorOffsetY = 22;

    private readonly DispatcherTimer _countdown = new() { Interval = TimeSpan.FromMilliseconds(100) };

    private Storyboard? _spinner;
    private TranslationOutcome? _outcome;
    private double _secondsLeft;
    private double _secondsTotal;
    private bool _suppressLanguageEvent;
    private bool _wasActivated;

    public TranslationPopup()
    {
        InitializeComponent();

        ThemeManager.Current.Register(this, smallCorners: false);

        _countdown.Tick += OnCountdownTick;
        MouseEnter += (_, _) => PauseCountdown();
        MouseLeave += (_, _) => ResumeCountdown();
        PreviewKeyDown += OnPreviewKeyDown;
        Activated += (_, _) => _wasActivated = true;
        Deactivated += OnDeactivated;
    }

    /// <summary>Kullanici "Ayarlar" dedi.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Kullanici "Tekrar dene" dedi.</summary>
    public event EventHandler? RetryRequested;

    /// <summary>Kullanici acilir listeden baska bir hedef dil secti.</summary>
    public event EventHandler<LanguageOption>? RetranslateRequested;

    /// <summary>Gonderim onayi istendi ve kullanici kabul etti.</summary>
    public event EventHandler? ConfirmAccepted;

    /// <summary>Gonderim onayi reddedildi.</summary>
    public event EventHandler? ConfirmRejected;

    /// <summary>Ceviriyi secili metnin yerine yaz.</summary>
    public event EventHandler<string>? ReplaceRequested;

    // ===================== Durumlar =====================

    public void ShowLoading(string message = "Çevriliyor…")
    {
        StopCountdown();
        SetPanels(loading: true, confirm: false, error: false, result: false);
        LoadingText.Text = message;
        LanguageChip.Visibility = Visibility.Collapsed;
        StartSpinner();
        PresentNearCursor();
    }

    public void ShowConfirm(string sourceText, int characterCount)
    {
        StopCountdown();
        SetPanels(loading: false, confirm: true, error: false, result: false);

        ConfirmPreview.Text = Shorten(sourceText, 420);
        LanguageChip.Visibility = Visibility.Visible;
        LanguageChipText.Text = $"{characterCount:N0} KARAKTER";

        PresentNearCursor();
    }

    public void ShowError(string message, bool canRetry)
    {
        StopCountdown();
        StopSpinner();
        SetPanels(loading: false, confirm: false, error: true, result: false);

        ErrorText.Text = message;
        RetryButton.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
        LanguageChip.Visibility = Visibility.Collapsed;

        PresentNearCursor();
    }

    public void ShowResult(
        TranslationOutcome outcome,
        IReadOnlyList<LanguageOption> languages,
        string currentTargetCode,
        bool allowReplace,
        int autoCloseSeconds,
        bool asToast)
    {
        _outcome = outcome;

        StopSpinner();
        SetPanels(loading: false, confirm: false, error: false, result: true);
        CopyButton.Content = "Kopyala";

        ResultText.Text = outcome.TranslatedText;
        SourceText.Text = outcome.SourceText;
        SourceBox.Visibility = Visibility.Collapsed;
        ToggleSourceButton.Content = "Özgün metni göster";

        LanguageChip.Visibility = Visibility.Visible;
        LanguageChipText.Text = outcome.WasSkipped
            ? "ZATEN HEDEF DİLDE"
            : $"{outcome.DetectedLanguage.ToUpperInvariant()} → {outcome.TargetLanguage.ToUpperInvariant()}";

        ReplaceButton.Visibility = allowReplace ? Visibility.Visible : Visibility.Collapsed;

        _suppressLanguageEvent = true;
        TargetLanguageCombo.ItemsSource = languages;
        TargetLanguageCombo.SelectedItem = LanguageCatalog.ByCode(currentTargetCode);
        _suppressLanguageEvent = false;

        if (asToast)
        {
            PresentAsToast();
        }
        else
        {
            PresentNearCursor();
        }

        StartCountdown(autoCloseSeconds);
    }

    public void HidePopup()
    {
        StopCountdown();
        StopSpinner();
        Hide();
    }

    private void SetPanels(bool loading, bool confirm, bool error, bool result)
    {
        LoadingPanel.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        ConfirmPanel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = error ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = result ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===================== Konumlandırma =====================

    /// <summary>
    /// Pencereyi imlecin altina yerlestirir. Tum hesap fiziksel piksel uzerinden yapilir;
    /// boylece karisik DPI'li coklu ekran kurulumlarinda da dogru monitorde acilir.
    /// </summary>
    private void PresentNearCursor()
    {
        EnsureVisible();

        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        var area = screen.WorkingArea;
        var size = GetPhysicalSize();

        var x = cursor.X - (size.Width / 3);
        var y = cursor.Y + CursorOffsetY;

        // Alta sigmiyorsa imlecin ustune al.
        if (y + size.Height > area.Bottom - EdgeMargin)
        {
            y = cursor.Y - size.Height - CursorOffsetY / 2;
        }

        MoveTo(Clamp(x, area.Left + EdgeMargin, area.Right - size.Width - EdgeMargin),
               Clamp(y, area.Top + EdgeMargin, area.Bottom - size.Height - EdgeMargin));
    }

    /// <summary>Pano/yerine yaz modlarinda sag altta bildirim gibi gosterilir.</summary>
    private void PresentAsToast()
    {
        EnsureVisible();

        var screen = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];

        if (NativeMethods.GetCursorPos(out var cursor))
        {
            screen = Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
        }

        var area = screen.WorkingArea;
        var size = GetPhysicalSize();

        MoveTo(area.Right - size.Width - EdgeMargin, area.Bottom - size.Height - EdgeMargin);
    }

    private void EnsureVisible()
    {
        if (IsVisible)
        {
            UpdateLayout();
            return;
        }

        Show();
        UpdateLayout();
        AnimateIn();
    }

    private NativeMethods.RECT GetPhysicalSize()
    {
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.GetWindowRect(handle, out var rect);
        return rect;
    }

    private void MoveTo(int x, int y)
    {
        var handle = new WindowInteropHelper(this).Handle;

        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOPMOST,
            x,
            y,
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private static int Clamp(int value, int min, int max) => max < min ? min : Math.Clamp(value, min, max);

    /// <summary>
    /// Giris animasyonu: icerik katmani hafifce yukari kayarak belirir.
    /// Pencere saydamligi kapali oldugu icin animasyon Window'a degil katmana uygulanir.
    /// </summary>
    private void AnimateIn()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        RootLayer.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease });

        RootOffset.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
    }

    // ===================== Geri sayım =====================

    private void StartCountdown(int seconds)
    {
        if (seconds <= 0)
        {
            StopCountdown();
            CountdownBar.Width = 0;
            return;
        }

        _secondsTotal = seconds;
        _secondsLeft = seconds;
        _countdown.Start();
        UpdateCountdownBar();
    }

    private void StopCountdown()
    {
        _countdown.Stop();
        CountdownBar.Width = 0;
    }

    private void PauseCountdown() => _countdown.Stop();

    private void ResumeCountdown()
    {
        if (_secondsLeft > 0 && ResultPanel.Visibility == Visibility.Visible)
        {
            _countdown.Start();
        }
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        _secondsLeft -= 0.1;

        if (_secondsLeft <= 0)
        {
            HidePopup();
            return;
        }

        UpdateCountdownBar();
    }

    private void UpdateCountdownBar()
    {
        if (_secondsTotal <= 0)
        {
            return;
        }

        CountdownBar.Width = ActualWidth * (_secondsLeft / _secondsTotal);
    }

    // ===================== Olaylar =====================

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HidePopup();
            e.Handled = true;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Pencere ShowActivated=False ile aciliyor; yalnizca kullanici bir kez
        // ustune tikladiysa "disari tiklayinca kapan" davranisi devreye girer.
        // Onay ekrani bilincli bir karar bekledigi icin kapanmaz.
        if (_wasActivated && ConfirmPanel.Visibility != Visibility.Visible)
        {
            HidePopup();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => HidePopup();

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        HidePopup();
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRetryClick(object sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, EventArgs.Empty);

    private void OnAcceptConfirmClick(object sender, RoutedEventArgs e) =>
        ConfirmAccepted?.Invoke(this, EventArgs.Empty);

    private void OnCancelConfirmClick(object sender, RoutedEventArgs e)
    {
        HidePopup();
        ConfirmRejected?.Invoke(this, EventArgs.Empty);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_outcome is null)
        {
            return;
        }

        if (Services.ClipboardService.TrySetText(_outcome.TranslatedText))
        {
            CopyButton.Content = "Kopyalandı ✓";
            StopCountdown();
        }
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e)
    {
        if (_outcome is null)
        {
            return;
        }

        HidePopup();
        ReplaceRequested?.Invoke(this, _outcome.TranslatedText);
    }

    private void OnToggleSourceClick(object sender, RoutedEventArgs e)
    {
        var showing = SourceBox.Visibility == Visibility.Visible;
        SourceBox.Visibility = showing ? Visibility.Collapsed : Visibility.Visible;
        ToggleSourceButton.Content = showing ? "Özgün metni göster" : "Özgün metni gizle";
    }

    private void OnTargetLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageEvent || TargetLanguageCombo.SelectedItem is not LanguageOption language)
        {
            return;
        }

        RetranslateRequested?.Invoke(this, language);
    }

    private void StartSpinner()
    {
        _spinner ??= (Storyboard)Resources["SpinnerSpin"];
        _spinner.Begin(this, isControllable: true);
    }

    private void StopSpinner()
    {
        if (_spinner is null)
        {
            return;
        }

        try
        {
            _spinner.Stop(this);
        }
        catch (InvalidOperationException)
        {
            // Hic baslatilmadiysa yok say.
        }
    }

    private static string Shorten(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
