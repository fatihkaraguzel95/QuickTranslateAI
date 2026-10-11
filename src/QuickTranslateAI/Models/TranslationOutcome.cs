namespace QuickTranslateAI.Models;

/// <summary>Bir çeviri isteğinin sonucu.</summary>
public sealed record TranslationOutcome(
    string SourceText,
    string TranslatedText,
    string DetectedLanguage,
    string TargetLanguage)
{
    /// <summary>Kaynak metin zaten hedef dildeydi ve çeviri atlandı.</summary>
    public bool WasSkipped { get; init; }
}
