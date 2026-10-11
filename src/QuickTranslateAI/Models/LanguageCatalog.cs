using System.Collections.Generic;
using System.Linq;

namespace QuickTranslateAI.Models;

public sealed record LanguageOption(string Code, string DisplayName, string EnglishName);

public static class LanguageCatalog
{
    /// <summary>Hedef dil listesi. Kaynak dil her zaman otomatik algılanır.</summary>
    public static IReadOnlyList<LanguageOption> All { get; } = new List<LanguageOption>
    {
        new("tr", "Türkçe", "Turkish"),
        new("de", "Almanca", "German"),
        new("en", "İngilizce", "English"),
        new("fr", "Fransızca", "French"),
        new("es", "İspanyolca", "Spanish"),
        new("it", "İtalyanca", "Italian"),
        new("nl", "Felemenkçe", "Dutch"),
        new("pt", "Portekizce", "Portuguese"),
        new("pl", "Lehçe", "Polish"),
        new("cs", "Çekçe", "Czech"),
        new("ru", "Rusça", "Russian"),
        new("uk", "Ukraynaca", "Ukrainian"),
        new("ro", "Romence", "Romanian"),
        new("hu", "Macarca", "Hungarian"),
        new("sv", "İsveççe", "Swedish"),
        new("da", "Danca", "Danish"),
        new("nb", "Norveççe", "Norwegian"),
        new("fi", "Fince", "Finnish"),
        new("el", "Yunanca", "Greek"),
        new("bg", "Bulgarca", "Bulgarian"),
        new("sr", "Sırpça", "Serbian"),
        new("hr", "Hırvatça", "Croatian"),
        new("ar", "Arapça", "Arabic"),
        new("fa", "Farsça", "Persian"),
        new("he", "İbranice", "Hebrew"),
        new("hi", "Hintçe", "Hindi"),
        new("ja", "Japonca", "Japanese"),
        new("ko", "Korece", "Korean"),
        new("zh", "Çince (Basitleştirilmiş)", "Chinese (Simplified)"),
        new("az", "Azerbaycan Türkçesi", "Azerbaijani"),
        new("ka", "Gürcüce", "Georgian"),
        new("id", "Endonezce", "Indonesian"),
        new("vi", "Vietnamca", "Vietnamese"),
        new("th", "Tayca", "Thai")
    };

    public static LanguageOption? ByCode(string? code) =>
        code is null ? null : All.FirstOrDefault(l => l.Code == code);

    public static LanguageOption Default => All[0];
}
