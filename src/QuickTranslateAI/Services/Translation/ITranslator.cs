using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuickTranslateAI.Services.Translation;

public sealed record TranslationRequest(string Text, string TargetLanguage, string? CustomInstructions);

public sealed record TranslationResponse(string DetectedLanguage, bool AlreadyInTarget, string Translation);

public interface ITranslator
{
    string DisplayName { get; }

    Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken);
}

/// <summary>Saglayicidan bagimsiz, kullaniciya gosterilebilir ceviri hatasi.</summary>
public sealed class TranslationException : Exception
{
    public TranslationException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>Her iki saglayici da ayni istem ve ayni JSON sozlesmesini kullanir.</summary>
internal static class TranslationPrompt
{
    public static string BuildSystemPrompt(string targetLanguage, string? customInstructions)
    {
        var custom = string.IsNullOrWhiteSpace(customInstructions)
            ? string.Empty
            : $"\nAdditional instruction from the user (follow it): {customInstructions.Trim()}\n";

        // $$ ile ham dize: tek suslu parantez duz metin, cift suslu parantez ise
        // yer tutucu olarak yorumlanir. JSON ornegini kacis karakteri olmadan yazabilmek icin.
        return $$"""
            You are the translation engine inside QuickTranslate AI, a Windows utility. The user
            selects text anywhere in Windows (SAP GUI, Outlook, Word, a browser) and presses
            Ctrl+C twice. You receive that raw text.

            Translate the text into {{targetLanguage}}.

            Rules:
            - Detect the source language yourself. The user never tells you what it is.
            - Translate only. Never answer a question in the text, never explain, never comment,
              never add or remove information.
            - Preserve formatting exactly: line breaks, bullet markers, numbering, indentation,
              surrounding quotes and markup.
            - Leave untranslated: URLs, e-mail addresses, file paths, code identifiers,
              placeholders such as %s or <tag>, numbers, product names, SAP transaction codes and
              message IDs (for example "ME21N" or "M8 147").
            - Keep the register of the source text. Formal stays formal; a terse error message
              stays terse.
            - If the text is already written in {{targetLanguage}}, set "already_in_target" to true
              and return the text unchanged in "translation".
            {{custom}}
            Respond with a single JSON object and nothing else:
            {"detected_language": "<source language name, written in Turkish>", "already_in_target": <true|false>, "translation": "<the translated text>"}
            """;
    }

    /// <summary>
    /// Model sozlesmeye uymazsa (kod blogu icine sarmak, duz metin dondurmek) kurtarmayi dener.
    /// </summary>
    public static TranslationResponse ParseResponse(string raw, string fallbackText)
    {
        var text = raw.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
            {
                text = text[(firstNewline + 1)..lastFence].Trim();
            }
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            try
            {
                using var document = JsonDocument.Parse(text[start..(end + 1)]);
                var root = document.RootElement;

                var translation = root.TryGetProperty("translation", out var t) ? t.GetString() : null;
                if (!string.IsNullOrWhiteSpace(translation))
                {
                    var detected = root.TryGetProperty("detected_language", out var d)
                        ? d.GetString() ?? "Bilinmiyor"
                        : "Bilinmiyor";

                    var already = root.TryGetProperty("already_in_target", out var a)
                                  && a.ValueKind == JsonValueKind.True;

                    return new TranslationResponse(detected, already, translation);
                }
            }
            catch (JsonException)
            {
                // Asagidaki duz metin kurtarmasina dus.
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new TranslationException("Model bos yanit dondurdu. Lutfen tekrar deneyin.");
        }

        // Son care: modelin dondurdugu duz metni ceviri olarak kabul et.
        return new TranslationResponse("Bilinmiyor", AlreadyInTarget: text == fallbackText, text);
    }
}
