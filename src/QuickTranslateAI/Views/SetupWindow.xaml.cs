using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using QuickTranslateAI.Models;
using QuickTranslateAI.Services;
using QuickTranslateAI.Services.Translation;
using QuickTranslateAI.Theming;

namespace QuickTranslateAI.Views;

public sealed record ChoiceOption(int Value, string Label);

/// <summary>OpenAI uyumlu bir saglayici icin hazir ayar.</summary>
public sealed record ProviderPreset(string Key, string Name, string BaseUrl, bool JsonMode, string[] Models);

/// <summary>
/// Ilk acilista gosterilen kurulum sihirbazi. Uygulamanin calismasi icin gereken
/// her sey burada sorulur: saglayici, API anahtari, diller, davranis ve gorunum.
/// </summary>
public partial class SetupWindow : Window
{
    // Model alani serbest metindir; bu listeler yalnizca oneri olarak acilir.
    private static readonly string[] ClaudeModels =
    [
        "claude-opus-5",
        "claude-sonnet-5",
        "claude-haiku-4-5"
    ];

    private static readonly string[] OpenAiModels =
    [
        "gpt-4o-mini",
        "gpt-4o",
        "gpt-4.1-mini"
    ];

    private static readonly ProviderPreset[] Presets =
    [
        new("nvidia", "NVIDIA NIM", "https://integrate.api.nvidia.com/v1", JsonMode: false,
        [
            "meta/llama-3.3-70b-instruct",
            "nvidia/llama-3.3-nemotron-super-49b-v1",
            "qwen/qwen2.5-72b-instruct",
            "deepseek-ai/deepseek-v3",
            "mistralai/mistral-large-2-instruct"
        ]),
        new("groq", "Groq", "https://api.groq.com/openai/v1", JsonMode: true,
        [
            "llama-3.3-70b-versatile",
            "qwen-2.5-32b"
        ]),
        new("openrouter", "OpenRouter", "https://openrouter.ai/api/v1", JsonMode: true,
        [
            "meta-llama/llama-3.3-70b-instruct",
            "qwen/qwen-2.5-72b-instruct"
        ]),
        new("ollama", "Yerel (Ollama)", "http://localhost:11434/v1", JsonMode: false,
        [
            "llama3.3",
            "qwen2.5:14b"
        ])
    ];

    private static readonly ChoiceOption[] DoubleCopyWindows =
    [
        new(400, "400 ms — hızlı"),
        new(500, "500 ms — varsayılan"),
        new(700, "700 ms — rahat"),
        new(1000, "1000 ms — yavaş")
    ];

    private static readonly ChoiceOption[] AutoCloseChoices =
    [
        new(0, "Kapanmasın"),
        new(8, "8 saniye sonra"),
        new(14, "14 saniye sonra"),
        new(25, "25 saniye sonra")
    ];

    private static readonly ChoiceOption[] MaxCharChoices =
    [
        new(1000, "1.000 karakter"),
        new(2500, "2.500 karakter"),
        new(5000, "5.000 karakter"),
        new(12000, "12.000 karakter"),
        new(int.MaxValue, "Sınır yok")
    ];

    private readonly AppSettings _settings;
    private readonly bool _settingsMode;
    private readonly bool _initialized;

    private string _apiKey = string.Empty;
    private int _step = 1;
    private bool _keyVisible;
    private bool _testPassed;

    /// <param name="settingsMode">
    /// true ise pencere ayar ekrani gibi davranir: adimlar soldaki listeden dogrudan
    /// secilebilir, gelismis alanlar gorunur ve tek bir "Kaydet" dugmesi vardir.
    /// </param>
    public SetupWindow(AppSettings current, bool settingsMode = false)
    {
        _settings = current.Clone();
        _settingsMode = settingsMode;

        InitializeComponent();
        ThemeManager.Current.Register(this);

        TargetCombo.ItemsSource = LanguageCatalog.All;
        FallbackCombo.ItemsSource = LanguageCatalog.All;
        DoubleCopyCombo.ItemsSource = DoubleCopyWindows;
        AutoCloseCombo.ItemsSource = AutoCloseChoices;
        MaxCharsCombo.ItemsSource = MaxCharChoices;

        if (_settingsMode)
        {
            Title = "QuickTranslate AI ayarları";
            WindowTitleText.Text = "QUICKTRANSLATE AI · AYARLAR";
            RailHint.Text = "Bir başlığa tıklayarak doğrudan o bölüme geçebilirsin.";
            ShortcutCard.Visibility = Visibility.Visible;
            AdvancedStep2.Visibility = Visibility.Visible;
            AdvancedStep3.Visibility = Visibility.Visible;
            StepRow1.Cursor = Cursors.Hand;
            StepRow2.Cursor = Cursors.Hand;
            StepRow3.Cursor = Cursors.Hand;
            PathsText.Text = $"Ayarlar: {SettingsStore.FilePath}\nAPI anahtarı: Windows Kimlik Bilgisi Yöneticisi → {SecretStore.TargetFor(current.Provider)}";
        }

        LoadFrom(_settings);

        _initialized = true;
        UpdateStepVisuals();

    }

    /// <summary>Kurulum tamamlandiginda doldurulur.</summary>
    public AppSettings? Result { get; private set; }

    private void LoadFrom(AppSettings settings)
    {
        ProviderClaude.IsChecked = settings.Provider == ProviderKind.Claude;
        ProviderOpenAi.IsChecked = settings.Provider == ProviderKind.OpenAi;
        ProviderCustom.IsChecked = settings.Provider == ProviderKind.Custom;

        CustomNameBox.Text = settings.CustomDisplayName;
        CustomUrlBox.Text = settings.CustomBaseUrl;
        CustomJsonCheck.IsChecked = settings.CustomSupportsJsonMode;

        ApplyProviderUi(settings.Provider, TranslationCoordinator.ModelFor(settings));

        var existingKey = SecretStore.Read(settings.Provider);
        if (!string.IsNullOrWhiteSpace(existingKey))
        {
            _apiKey = existingKey;
            ApiKeyBox.Password = existingKey;
        }

        TargetCombo.SelectedItem = LanguageCatalog.ByCode(settings.TargetLanguageCode) ?? LanguageCatalog.Default;
        FallbackCombo.SelectedItem = LanguageCatalog.ByCode(settings.FallbackLanguageCode);

        DoubleCopyCombo.SelectedItem = DoubleCopyWindows.FirstOrDefault(c => c.Value == settings.DoubleCopyWindowMs)
                                       ?? DoubleCopyWindows[1];
        AutoCloseCombo.SelectedItem = AutoCloseChoices.FirstOrDefault(c => c.Value == settings.PopupAutoCloseSeconds)
                                      ?? AutoCloseChoices[2];

        BehaviorPopup.IsChecked = settings.Behavior == ResultBehavior.Popup;
        BehaviorClipboard.IsChecked = settings.Behavior == ResultBehavior.ClipboardAndToast;
        BehaviorReplace.IsChecked = settings.Behavior == ResultBehavior.ReplaceSelection;

        ThemeSystem.IsChecked = settings.Theme == AppThemeMode.System;
        ThemeLight.IsChecked = settings.Theme == AppThemeMode.Light;
        ThemeDark.IsChecked = settings.Theme == AppThemeMode.Dark;

        BackdropAcrylic.IsChecked = settings.Backdrop == BackdropMode.Acrylic;
        BackdropMica.IsChecked = settings.Backdrop == BackdropMode.Mica;
        BackdropSolid.IsChecked = settings.Backdrop == BackdropMode.Solid;

        SystemAccentCheck.IsChecked = settings.UseSystemAccent;
        AutoStartCheck.IsChecked = settings.StartWithWindows;
        ConfirmCheck.IsChecked = settings.ConfirmBeforeSend;

        CustomInstructionsBox.Text = settings.CustomInstructions ?? string.Empty;
        EnabledCheck.IsChecked = settings.Enabled;
        RestoreClipboardCheck.IsChecked = settings.RestoreClipboardAfterReplace;
        MaxCharsCombo.SelectedItem = MaxCharChoices.FirstOrDefault(c => c.Value == settings.MaxCharacters)
                                     ?? MaxCharChoices[2];

        if (!Services.Native.Dwm.SupportsBackdrop)
        {
            BackdropAcrylic.IsEnabled = false;
            BackdropMica.IsEnabled = false;
            BackdropSolid.IsChecked = true;
        }
    }

    // ===================== Adım gezinmesi =====================

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _step = 1;
            UpdateStepVisuals();
            TestStatus.Text = "Önce API anahtarını gir.";
            TestStatus.Foreground = (System.Windows.Media.Brush)FindResource("App.Danger");
            return;
        }

        if (!_settingsMode && _step < 3)
        {
            _step++;
            UpdateStepVisuals();
            return;
        }

        Finish();
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_settingsMode)
        {
            DialogResult = false;
            Close();
            return;
        }

        if (_step <= 1)
        {
            return;
        }

        _step--;
        UpdateStepVisuals();
    }

    private void OnStepRowClick(object sender, MouseButtonEventArgs e)
    {
        if (!_settingsMode || sender is not FrameworkElement element || element.Tag is not string tag)
        {
            return;
        }

        _step = int.Parse(tag);
        UpdateStepVisuals();
    }

    private void UpdateStepVisuals()
    {
        Step1.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;

        SetStepActive(StepPill1, StepDot1, _step == 1, _step > 1);
        SetStepActive(StepPill2, StepDot2, _step == 2, _step > 2);
        SetStepActive(StepPill3, StepDot3, _step == 3, done: false);

        if (_settingsMode)
        {
            BackButton.IsEnabled = true;
            BackButton.Content = "Vazgeç";
            NextButton.Content = "Kaydet";
            FooterHint.Text = "Değişiklikler kaydedildiği anda geçerli olur.";
            return;
        }

        BackButton.IsEnabled = _step > 1;
        NextButton.Content = _step == 3 ? "Kurulumu tamamla" : "İleri";

        FooterHint.Text = _step switch
        {
            1 => _testPassed
                ? "Bağlantı doğrulandı. Devam edebilirsin."
                : "Devam etmek için geçerli bir API anahtarı gerekir.",
            2 => "Kaynak dil her zaman otomatik algılanır.",
            _ => "Bitirdiğinde uygulama sistem tepsisine yerleşir ve dinlemeye başlar."
        };
    }

    private void SetStepActive(System.Windows.Controls.Border pill, System.Windows.Controls.Border dot, bool active, bool done)
    {
        pill.Background = active
            ? (System.Windows.Media.Brush)FindResource("App.AccentSoft")
            : System.Windows.Media.Brushes.Transparent;

        dot.Background = active || done
            ? (System.Windows.Media.Brush)FindResource("App.Accent")
            : (System.Windows.Media.Brush)FindResource("App.ControlFill");

        if (dot.Child is System.Windows.Controls.TextBlock label)
        {
            label.Foreground = active || done
                ? (System.Windows.Media.Brush)FindResource("App.OnAccent")
                : (System.Windows.Media.Brush)FindResource("App.TextSecondary");
        }
    }

    // ===================== Adım 1 =====================

    private ProviderKind ReadProvider()
    {
        if (ProviderOpenAi.IsChecked == true)
        {
            return ProviderKind.OpenAi;
        }

        return ProviderCustom.IsChecked == true ? ProviderKind.Custom : ProviderKind.Claude;
    }

    private void OnProviderChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        var provider = ReadProvider();

        var preferredModel = provider switch
        {
            ProviderKind.Claude => _settings.ClaudeModel,
            ProviderKind.OpenAi => _settings.OpenAiModel,
            _ => _settings.CustomModel
        };

        ApplyProviderUi(provider, preferredModel);

        // Saglayici degisince o saglayiciya ait kayitli anahtari getir.
        var stored = SecretStore.Read(provider) ?? string.Empty;

        _apiKey = stored;
        ApiKeyBox.Password = stored;
        ApiKeyPlain.Text = stored;

        _testPassed = false;
        TestStatus.Text = string.Empty;
    }

    /// <summary>Saglayiciya gore model onerilerini, ipuclarini ve ozel alanlari gunceller.</summary>
    private void ApplyProviderUi(ProviderKind provider, string? selectedModel)
    {
        CustomPanel.Visibility = provider == ProviderKind.Custom ? Visibility.Visible : Visibility.Collapsed;

        string[] suggestions;

        switch (provider)
        {
            case ProviderKind.Claude:
                suggestions = ClaudeModels;
                KeyHint.Text = "Anahtarı console.anthropic.com adresinden oluşturabilirsin.";
                ModelHint.Text = "Önerilen: claude-opus-5. Başka bir model kimliğini elle de yazabilirsin.";
                break;

            case ProviderKind.OpenAi:
                suggestions = OpenAiModels;
                KeyHint.Text = "Anahtarı platform.openai.com/api-keys adresinden oluşturabilirsin.";
                ModelHint.Text = "Listede olmayan bir modeli doğrudan yazabilirsin.";
                break;

            default:
                var preset = PresetForUrl(CustomUrlBox.Text);
                suggestions = preset?.Models ?? [];
                KeyHint.Text = preset?.Key == "nvidia"
                    ? "Anahtarı build.nvidia.com üzerinde model sayfasındaki \u201cGet API Key\u201d ile alırsın (nvapi- ile başlar)."
                    : "Seçtiğin servisin kendi panelinden bir API anahtarı oluştur.";
                ModelHint.Text = "Model kimliğini servisin sayfasında yazdığı gibi aynen gir, örn. meta/llama-3.3-70b-instruct.";
                break;
        }

        ModelCombo.ItemsSource = suggestions;
        ModelCombo.Text = string.IsNullOrWhiteSpace(selectedModel)
            ? suggestions.FirstOrDefault() ?? string.Empty
            : selectedModel;
    }

    private static ProviderPreset? PresetForUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var normalized = url.Trim().TrimEnd('/');
        return Presets.FirstOrDefault(
            preset => string.Equals(preset.BaseUrl, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string key })
        {
            return;
        }

        var preset = Presets.FirstOrDefault(p => p.Key == key);
        if (preset is null)
        {
            return;
        }

        CustomNameBox.Text = preset.Name;
        CustomUrlBox.Text = preset.BaseUrl;
        CustomJsonCheck.IsChecked = preset.JsonMode;

        ApplyProviderUi(ProviderKind.Custom, preset.Models.FirstOrDefault());

        _testPassed = false;
        TestStatus.Text = string.Empty;
    }

    private void OnCustomUrlChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        _testPassed = false;
        TestStatus.Text = string.Empty;
    }

    private void OnApiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (_keyVisible)
        {
            return;
        }

        _apiKey = ApiKeyBox.Password;
        _testPassed = false;
        TestStatus.Text = string.Empty;
    }

    private void OnApiKeyPlainChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_keyVisible)
        {
            return;
        }

        _apiKey = ApiKeyPlain.Text;
        _testPassed = false;
        TestStatus.Text = string.Empty;
    }

    private void OnToggleKeyVisibility(object sender, RoutedEventArgs e)
    {
        _keyVisible = !_keyVisible;

        if (_keyVisible)
        {
            ApiKeyPlain.Text = _apiKey;
            ApiKeyPlain.Visibility = Visibility.Visible;
            ApiKeyBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            ApiKeyBox.Password = _apiKey;
            ApiKeyBox.Visibility = Visibility.Visible;
            ApiKeyPlain.Visibility = Visibility.Collapsed;
        }

        if (sender is System.Windows.Controls.Button button)
        {
            button.Content = _keyVisible ? "Gizle" : "Göster";
        }
    }

    private async void OnTestConnectionClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            TestStatus.Text = "Önce API anahtarını gir.";
            TestStatus.Foreground = (System.Windows.Media.Brush)FindResource("App.Danger");
            return;
        }

        TestButton.IsEnabled = false;
        TestStatus.Foreground = (System.Windows.Media.Brush)FindResource("App.TextSecondary");
        TestStatus.Text = "Deneniyor…";

        try
        {
            var provider = ReadProvider();
            var model = ModelCombo.Text.Trim();

            ITranslator translator = provider switch
            {
                ProviderKind.Claude => new ClaudeTranslator(_apiKey, model),
                ProviderKind.OpenAi => new OpenAiTranslator(
                    _apiKey, model, OpenAiTranslator.OpenAiBaseUrl, useJsonMode: true, label: "OpenAI"),
                _ => new OpenAiTranslator(
                    _apiKey,
                    model,
                    CustomUrlBox.Text,
                    CustomJsonCheck.IsChecked == true,
                    string.IsNullOrWhiteSpace(CustomNameBox.Text) ? "Özel sağlayıcı" : CustomNameBox.Text.Trim())
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var target = (TargetCombo.SelectedItem as LanguageOption)?.DisplayName ?? "Türkçe";
            var response = await translator
                .TranslateAsync(new TranslationRequest("Guten Morgen", target, null), cts.Token)
                .ConfigureAwait(true);

            (translator as IDisposable)?.Dispose();

            _testPassed = true;
            TestStatus.Foreground = (System.Windows.Media.Brush)FindResource("App.Success");
            TestStatus.Text = $"Bağlantı tamam · “Guten Morgen” → “{response.Translation}”";
        }
        catch (TranslationException ex)
        {
            _testPassed = false;
            TestStatus.Foreground = (System.Windows.Media.Brush)FindResource("App.Danger");
            TestStatus.Text = ex.Message;
        }
        catch (OperationCanceledException)
        {
            _testPassed = false;
            TestStatus.Foreground = (System.Windows.Media.Brush)FindResource("App.Danger");
            TestStatus.Text = "İstek zaman aşımına uğradı.";
        }
        finally
        {
            TestButton.IsEnabled = true;
            UpdateStepVisuals();
        }
    }

    // ===================== Adım 3 =====================

    /// <summary>Tema secimleri aninda uygulanir; kullanici sonucu canli gorur.</summary>
    private void OnThemePreviewChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        var preview = _settings.Clone();
        preview.Theme = ReadTheme();
        preview.Backdrop = ReadBackdrop();
        preview.UseSystemAccent = SystemAccentCheck.IsChecked == true;

        ThemeManager.Current.Apply(preview);
    }

    private AppThemeMode ReadTheme()
    {
        if (ThemeLight.IsChecked == true)
        {
            return AppThemeMode.Light;
        }

        return ThemeDark.IsChecked == true ? AppThemeMode.Dark : AppThemeMode.System;
    }

    private BackdropMode ReadBackdrop()
    {
        if (BackdropMica.IsChecked == true)
        {
            return BackdropMode.Mica;
        }

        return BackdropSolid.IsChecked == true ? BackdropMode.Solid : BackdropMode.Acrylic;
    }

    // ===================== Bitir =====================

    private void Finish()
    {
        var provider = ReadProvider();
        var model = ModelCombo.Text.Trim();
        var target = (TargetCombo.SelectedItem as LanguageOption) ?? LanguageCatalog.Default;
        var fallback = FallbackCombo.SelectedItem as LanguageOption;

        _settings.Provider = provider;

        if (!string.IsNullOrWhiteSpace(model))
        {
            switch (provider)
            {
                case ProviderKind.Claude:
                    _settings.ClaudeModel = model;
                    break;
                case ProviderKind.OpenAi:
                    _settings.OpenAiModel = model;
                    break;
                default:
                    _settings.CustomModel = model;
                    break;
            }
        }

        if (provider == ProviderKind.Custom)
        {
            var name = CustomNameBox.Text.Trim();
            _settings.CustomDisplayName = string.IsNullOrEmpty(name) ? "Özel sağlayıcı" : name;
            _settings.CustomBaseUrl = CustomUrlBox.Text.Trim();
            _settings.CustomSupportsJsonMode = CustomJsonCheck.IsChecked == true;
        }

        _settings.TargetLanguage = target.DisplayName;
        _settings.TargetLanguageCode = target.Code;

        // Yedek dil hedefle ayniysa iki yonlu ceviri anlamsiz olur; bos birakilir.
        if (fallback is null || fallback.Code == target.Code)
        {
            _settings.FallbackLanguage = null;
            _settings.FallbackLanguageCode = null;
        }
        else
        {
            _settings.FallbackLanguage = fallback.DisplayName;
            _settings.FallbackLanguageCode = fallback.Code;
        }

        _settings.Behavior = BehaviorClipboard.IsChecked == true
            ? ResultBehavior.ClipboardAndToast
            : BehaviorReplace.IsChecked == true
                ? ResultBehavior.ReplaceSelection
                : ResultBehavior.Popup;

        _settings.DoubleCopyWindowMs = (DoubleCopyCombo.SelectedItem as ChoiceOption)?.Value ?? 500;
        _settings.PopupAutoCloseSeconds = (AutoCloseCombo.SelectedItem as ChoiceOption)?.Value ?? 14;

        _settings.Theme = ReadTheme();
        _settings.Backdrop = ReadBackdrop();
        _settings.UseSystemAccent = SystemAccentCheck.IsChecked == true;
        _settings.StartWithWindows = AutoStartCheck.IsChecked == true;
        _settings.ConfirmBeforeSend = ConfirmCheck.IsChecked == true;
        _settings.SetupCompleted = true;

        if (_settingsMode)
        {
            _settings.Enabled = EnabledCheck.IsChecked == true;
            _settings.RestoreClipboardAfterReplace = RestoreClipboardCheck.IsChecked == true;
            _settings.MaxCharacters = (MaxCharsCombo.SelectedItem as ChoiceOption)?.Value ?? 5000;

            var instructions = CustomInstructionsBox.Text.Trim();
            _settings.CustomInstructions = string.IsNullOrEmpty(instructions) ? null : instructions;
        }
        else
        {
            _settings.Enabled = true;
        }

        SecretStore.Save(provider, _apiKey);

        Result = _settings;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
