using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Logging; // Using Plugin.Log for consistency if available statically
using Newtonsoft.Json; // Requires Newtonsoft.Json package

namespace ChatTranslatorAI; // Updated namespace

public class OpenRouterTranslator
{
    private readonly HttpClient _httpClient;
    private const string OpenRouterApiUrl = "https://openrouter.ai/api/v1/chat/completions";

    public OpenRouterTranslator()
    {
        _httpClient = new HttpClient();
        // Set a default User-Agent if desired, or other default headers
        // _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FFXIVChatTranslator/1.0");
    }

    public async Task<string?> TranslateTextAsync(string textToTranslate, string apiKey, string modelName, string sourceLanguage = "Japanese", string targetLanguage = "English", bool useFormalLanguage = false)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Plugin.Log.Warning("OpenRouter API Key is missing.");
            return "Error: API Key is missing.";
        }
        if (string.IsNullOrWhiteSpace(modelName))
        {
            Plugin.Log.Warning("OpenRouter Model Name is missing.");
            return "Error: Model Name is missing.";
        }
        if (string.IsNullOrWhiteSpace(textToTranslate))
        {
            Plugin.Log.Debug("Text to translate is empty, skipping translation.");
            return null; 
        }

        string systemPrompt;
        // Adjust politeness instructions based on useFormalLanguage
        // For automatic translations (where useFormalLanguage will be false), this aims for a standard/neutral translation.
        string politenessInstructionJP = useFormalLanguage ? "Use formal Japanese." : "Use casual Japanese."; // Empty string means no specific politeness request for standard translation
        string politenessInstructionEN = useFormalLanguage ? "Translate into formal and polite English." : ""; // Empty string for standard English

        string formalityCn = useFormalLanguage ? "formal" : ""; // Request "standard Chinese"
        string formalityCnt = useFormalLanguage ? "formal" : ""; // Request "standard Traditional Chinese"
        string formalityId = useFormalLanguage ? "formal (baku)" : ""; // Request "standard Indonesian"

        if (sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) && targetLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
        {
            // This is for /jp command, target is Japanese
            systemPrompt = $"You are a direct translator to Japanese. Detect the language of the input text and translate it to Japanese. {politenessInstructionJP} and use style speaking like context above. Your response must be in exactly this format: 'TRANSLATION || ROMAJI' where TRANSLATION is the Japanese text and ROMAJI is the transliteration to Latin alphabet. The Japanese text must come first, followed by exactly ' || ' as the separator, then the romaji. DO NOT include any quotes, equals signs, or other special characters in your output. DO NOT include any explanations or additional text.";
        }
        else if (sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) && targetLanguage.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            // This is for /en command, target is English
            systemPrompt = $"You are a direct translator to English. Detect the language of the input text and translate it to grammatically correct, natural-sounding English. {politenessInstructionEN} If the source appears to be Japanese, include romaji in this exact format: 'TRANSLATION || ROMAJI' where TRANSLATION is the English text and ROMAJI is the Japanese in Latin alphabet. For other languages, just return the English translation. Do not include quotes, equals signs, or other special characters that are not part of the actual translation. Return ONLY the translated text with proper grammar.";
        }
        else if (sourceLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase) && targetLanguage.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            systemPrompt = $"You are a direct translator from Japanese to English. {politenessInstructionEN} Translate the content with proper grammar and natural phrasing. Include romaji in this exact format: 'TRANSLATION || ROMAJI' where TRANSLATION is the English text and ROMAJI is the Japanese in Latin alphabet. Do not include quotes or other special characters that aren't part of the actual translation.";
        }
        else if (sourceLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) && targetLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
        {
            // This case is not directly used by /jp if "auto" is effective, but good to have.
            systemPrompt = $"You are a direct translator from English to Japanese. {politenessInstructionJP} Ensure correct Japanese grammar and natural phrasing. Your response must be in exactly this format: 'TRANSLATION || ROMAJI' where TRANSLATION is the Japanese text and ROMAJI is the transliteration to Latin alphabet. The Japanese text must come first, followed by exactly ' || ' as the separator, then the romaji. DO NOT include any quotes or special characters.";
        }
        else if ((sourceLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)) && 
                 targetLanguage.Equals("Chinese", StringComparison.OrdinalIgnoreCase))
        {
            // Translation to Chinese Simplified with pinyin
            systemPrompt = $"You are a direct translator to Chinese (Simplified). Detect the input language and translate it to grammatically correct, natural-sounding {formalityCn} Chinese. Include pinyin in this exact format: 'TRANSLATION || PINYIN' where TRANSLATION is the Chinese text and PINYIN is the pronunciation. The Chinese text must come first, followed by exactly ' || ' as the separator, then the pinyin. Do not include quotes, equals signs, or other special characters.";
        }
        else if ((sourceLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)) && 
                 targetLanguage.Equals("ChineseTraditional", StringComparison.OrdinalIgnoreCase))
        {
            // Translation to Chinese Traditional with pinyin
            systemPrompt = $"You are a direct translator to Chinese (Traditional). Detect the input language and translate it to grammatically correct, natural-sounding {formalityCnt} Traditional Chinese. Include pinyin in this exact format: 'TRANSLATION || PINYIN' where TRANSLATION is the Traditional Chinese text and PINYIN is the pronunciation. The Chinese text must come first, followed by exactly ' || ' as the separator, then the pinyin. Do not include quotes, equals signs, or other special characters.";
        }
        else if ((sourceLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)) && 
                 targetLanguage.Equals("Indonesian", StringComparison.OrdinalIgnoreCase))
        {
            // Translation to Indonesian
            systemPrompt = $"You are a direct translator to Indonesian. Detect the input language and translate it to grammatically correct, natural-sounding {formalityId} Indonesian. Provide only the translated text with proper grammar. Do not include quotes, equals signs, or other special characters that aren't part of the actual translation. No explanations or additional context.";
        }
        else
        {
            // Default or unsupported language pair
            systemPrompt = $"Translate the following text from {sourceLanguage} to grammatically correct, natural-sounding {targetLanguage}. Do not include quotes, equals signs, or other special characters that aren't part of the actual translation. Provide only the translated text with proper grammar and natural phrasing.";
            Plugin.Log.Warning($"Unsupported language pair or using default prompt for {sourceLanguage} to {targetLanguage}. Politeness instruction not applied.");
        }

        try
        {
            var messages = new System.Collections.Generic.List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = textToTranslate }
            };
            
            var requestPayload = new
            {
                model = modelName,
                messages = messages.ToArray()
            };

            var jsonPayload = JsonConvert.SerializeObject(requestPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, OpenRouterApiUrl)
            {
                Content = content
            };
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(httpRequestMessage);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                dynamic? responseObject = JsonConvert.DeserializeObject<dynamic>(responseString);
                
                string? translatedText = responseObject?.choices[0]?.message?.content;
                if (translatedText != null)
                {
                    Plugin.Log.Debug($"Successfully translated to: {translatedText}");
                    return translatedText.Trim();
                }
                else
                {
                    Plugin.Log.Warning("OpenRouter response was successful but content was not in expected format.");
                    return "Error: Could not parse translation from response.";
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Plugin.Log.Error($"Error translating with OpenRouter: {response.StatusCode} - {errorContent}");
                return $"Error: {response.StatusCode} - See plugin log for details.";
            }
        }
        catch (HttpRequestException httpEx)
        {
            Plugin.Log.Error(httpEx, "HTTP Exception during OpenRouter translation request.");
            return "Error: Network error during translation.";
        }
        catch (JsonException jsonEx)
        {
             Plugin.Log.Error(jsonEx, "JSON Exception during OpenRouter translation processing.");
            return "Error: Problem processing translation data.";
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Generic Exception during OpenRouter translation request.");
            return "Error: An unexpected error occurred during translation.";
        }
    }
} 