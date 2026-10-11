using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslateAI.Models;

namespace QuickTranslateAI.Services.Translation;

/// <summary>
/// Ayarlardan dogru saglayiciyi kurar, hedef dil mantigini uygular ve sonucu dondurur.
/// Pano ve arayuz islerini bilmez; onlari cagiran taraf yapar.
/// </summary>
public sealed class TranslationCoordinator : IDisposable
{
    private readonly object _gate = new();

    private ITranslator? _translator;
    private string _translatorKey = string.Empty;

    /// <summary>Saglayici, model veya anahtar degistiginde onbellegi dusurur.</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            (_translator as IDisposable)?.Dispose();
            _translator = null;
            _translatorKey = string.Empty;
        }
    }

    public ITranslator GetTranslator(AppSettings settings)
    {
        var apiKey = SecretStore.Read(settings.Provider)
                     ?? throw new TranslationException(
                         "API anahtari bulunamadi. Ayarlar penceresinden anahtarinizi girin.");

        var model = ModelFor(settings);
        var key = $"{settings.Provider}|{model}|{settings.CustomBaseUrl}|{apiKey.GetHashCode()}";

        lock (_gate)
        {
            if (_translator is not null && _translatorKey == key)
            {
                return _translator;
            }

            (_translator as IDisposable)?.Dispose();

            _translator = settings.Provider switch
            {
                ProviderKind.Claude => new ClaudeTranslator(apiKey, model),

                ProviderKind.OpenAi => new OpenAiTranslator(
                    apiKey,
                    model,
                    OpenAiTranslator.OpenAiBaseUrl,
                    useJsonMode: true,
                    label: "OpenAI"),

                // NVIDIA NIM ve benzeri OpenAI uyumlu uc noktalar.
                ProviderKind.Custom => new OpenAiTranslator(
                    apiKey,
                    model,
                    settings.CustomBaseUrl,
                    settings.CustomSupportsJsonMode,
                    string.IsNullOrWhiteSpace(settings.CustomDisplayName) ? "Özel sağlayıcı" : settings.CustomDisplayName),

                _ => throw new TranslationException("Bilinmeyen ceviri saglayicisi.")
            };

            _translatorKey = key;
            return _translator;
        }
    }

    public static string ModelFor(AppSettings settings) => settings.Provider switch
    {
        ProviderKind.Claude => settings.ClaudeModel,
        ProviderKind.OpenAi => settings.OpenAiModel,
        ProviderKind.Custom => settings.CustomModel,
        _ => string.Empty
    };

    /// <summary>
    /// Metni ayarlardaki hedef dile cevirir. Metin zaten hedef dildeyse ve bir yedek dil
    /// tanimliysa ceviri yedek dile yapilir. Boylece tek kisayol iki yonlu calisir.
    /// </summary>
    public async Task<TranslationOutcome> TranslateAsync(
        string text,
        AppSettings settings,
        string? overrideTargetLanguage,
        CancellationToken cancellationToken)
    {
        var translator = GetTranslator(settings);
        var target = overrideTargetLanguage ?? settings.TargetLanguage;

        var response = await translator
            .TranslateAsync(new TranslationRequest(text, target, settings.CustomInstructions), cancellationToken)
            .ConfigureAwait(false);

        var usedFallback = false;

        if (response.AlreadyInTarget
            && overrideTargetLanguage is null
            && !string.IsNullOrWhiteSpace(settings.FallbackLanguage)
            && !string.Equals(settings.FallbackLanguage, settings.TargetLanguage, StringComparison.OrdinalIgnoreCase))
        {
            target = settings.FallbackLanguage!;
            usedFallback = true;

            response = await translator
                .TranslateAsync(new TranslationRequest(text, target, settings.CustomInstructions), cancellationToken)
                .ConfigureAwait(false);
        }

        return new TranslationOutcome(text, response.Translation, response.DetectedLanguage, target)
        {
            WasSkipped = response.AlreadyInTarget && !usedFallback
        };
    }

    public void Dispose() => Invalidate();
}
