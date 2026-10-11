using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace QuickTranslateAI.Services.Translation;

/// <summary>
/// Anthropic Claude uzerinden ceviri. Resmi Anthropic .NET SDK'si kullanilir.
/// </summary>
public sealed class ClaudeTranslator : ITranslator
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    public ClaudeTranslator(string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new TranslationException("Claude API anahtari ayarlanmamis.");
        }

        _model = string.IsNullOrWhiteSpace(model) ? "claude-opus-5" : model;
        _client = new AnthropicClient { ApiKey = apiKey.Trim() };
    }

    public string DisplayName => $"Claude ({_model})";

    public async Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 8192,
            System = TranslationPrompt.BuildSystemPrompt(request.TargetLanguage, request.CustomInstructions),
            Messages = [new() { Role = Role.User, Content = request.Text }],

            // Ceviri basit bir gorev: dusuk efor gecikmeyi ve maliyeti belirgin dusurur,
            // adaptif dusunme acik kaldigi icin kalite korunur.
            OutputConfig = new OutputConfig { Effort = Effort.Low }
        };

        Message response;
        try
        {
            response = await _client.Messages.Create(parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            throw new TranslationException("Claude API anahtari gecersiz. Ayarlardan yeniden girin.", ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new TranslationException("Claude hiz sinirina takildi. Birkac saniye sonra tekrar deneyin.", ex);
        }
        catch (AnthropicNotFoundException ex)
        {
            throw new TranslationException($"Model bulunamadi: {_model}. Ayarlardan baska bir model secin.", ex);
        }
        catch (AnthropicIOException ex)
        {
            throw new TranslationException("Claude'a baglanilamadi. Internet baglantinizi kontrol edin.", ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new TranslationException($"Claude hatasi: {ex.Message}", ex);
        }

        if (response.StopReason == StopReason.Refusal)
        {
            throw new TranslationException(
                "Model bu metni cevirmeyi reddetti. Metni gozden gecirip tekrar deneyin.");
        }

        var text = string.Concat(
            response.Content
                .Select(block => block.Value)
                .OfType<TextBlock>()
                .Select(block => block.Text));

        return TranslationPrompt.ParseResponse(text, request.Text);
    }
}
