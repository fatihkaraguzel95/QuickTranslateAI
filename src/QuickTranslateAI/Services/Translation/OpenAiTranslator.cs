using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuickTranslateAI.Services.Translation;

/// <summary>
/// OpenAI uyumlu Chat Completions uc noktalari uzerinden ceviri.
/// Hem api.openai.com hem de ayni sozlesmeyi konusan saglayicilar icin kullanilir:
/// NVIDIA NIM (integrate.api.nvidia.com/v1), Groq, Together, OpenRouter, yerel sunucular.
/// Tek bir uc nokta kullanildigi icin saglayiciya ozel SDK yerine dogrudan HTTP tercih edildi.
/// </summary>
public sealed class OpenAiTranslator : ITranslator, IDisposable
{
    public const string OpenAiBaseUrl = "https://api.openai.com/v1";
    public const string NvidiaBaseUrl = "https://integrate.api.nvidia.com/v1";

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly string _label;
    private readonly bool _useJsonMode;

    /// <param name="baseUrl">Taban adres, orn. https://integrate.api.nvidia.com/v1</param>
    /// <param name="useJsonMode">
    /// response_format alani gonderilsin mi. Bazi uyumlu sunucular bu alani reddeder.
    /// </param>
    public OpenAiTranslator(
        string apiKey,
        string model,
        string? baseUrl = null,
        bool useJsonMode = true,
        string? label = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new TranslationException("API anahtari ayarlanmamis.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new TranslationException("Model adi bos olamaz. Ayarlardan bir model girin.");
        }

        _model = model.Trim();
        _endpoint = BuildEndpoint(baseUrl);
        _label = string.IsNullOrWhiteSpace(label) ? "OpenAI uyumlu" : label!.Trim();
        _useJsonMode = useJsonMode;

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
    }

    public string DisplayName => $"{_label} ({_model})";

    /// <summary>Kullanici /v1 ile de versin vermesin de dogru yola ulasalim.</summary>
    private static string BuildEndpoint(string? baseUrl)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl) ? OpenAiBaseUrl : baseUrl.Trim().TrimEnd('/');

        if (root.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        if (!Uri.TryCreate(root, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new TranslationException(
                $"Gecersiz uc nokta adresi: {baseUrl}. Ornek: https://integrate.api.nvidia.com/v1");
        }

        return root + "/chat/completions";
    }

    public async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        var systemPrompt = TranslationPrompt.BuildSystemPrompt(request.TargetLanguage, request.CustomInstructions);

        var messages = new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = request.Text }
        };

        // Bazi OpenAI uyumlu sunucular response_format alanini bilmiyor ve 400 donuyor;
        // bu durumda JSON sozlesmesini yalnizca istem metni saglar.
        object payload = _useJsonMode
            ? new
            {
                model = _model,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages
            }
            : new
            {
                model = _model,
                temperature = 0,
                messages
            };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(_endpoint, content, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new TranslationException(
                $"{_label} uc noktasina baglanilamadi ({_endpoint}). Adresi ve internet baglantinizi kontrol edin.",
                ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranslationException($"{_label} istegi zaman asimina ugradi.", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new TranslationException(DescribeError(response.StatusCode, body));
            }

            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("choices", out var choices)
                || choices.GetArrayLength() == 0)
            {
                throw new TranslationException($"{_label} beklenmeyen bir yanit dondurdu.");
            }

            var text = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return TranslationPrompt.ParseResponse(text, request.Text);
        }
    }

    private string DescribeError(HttpStatusCode status, string body)
    {
        var detail = ExtractMessage(body);

        return status switch
        {
            HttpStatusCode.Unauthorized => $"{_label} API anahtari gecersiz. Ayarlardan yeniden girin.",
            HttpStatusCode.Forbidden => $"{_label} bu anahtarla erisime izin vermiyor: {detail}",
            HttpStatusCode.PaymentRequired => $"{_label} hesabinizda yeterli kredi yok.",
            HttpStatusCode.TooManyRequests => $"{_label} hiz sinirina takildi. Birkac saniye sonra tekrar deneyin.",
            HttpStatusCode.NotFound => $"Model veya uc nokta bulunamadi ({_model}): {detail}",
            HttpStatusCode.BadRequest => $"Istek reddedildi ({_model}): {detail}",
            _ => $"{_label} hatasi ({(int)status}): {detail}"
        };
    }

    private static string ExtractMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
        }

        return body.Length > 300 ? body[..300] : body;
    }

    public void Dispose() => _http.Dispose();
}
