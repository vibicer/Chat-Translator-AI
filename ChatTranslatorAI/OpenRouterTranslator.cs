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
        string politenessInstructionJP = useFormalLanguage ? "Use formal Japanese (丁寧語 - teineigo or 敬語 - keigo where appropriate)." : "Use casual, informal Japanese (タメ口 - tameguchi).";
        string politenessInstructionEN = useFormalLanguage ? "Translate into formal and polite English." : "Translate into casual and informal English.";

        if (sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) && targetLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
        {
            // This is for /jp command, target is Japanese
            systemPrompt = $"You are a direct translator to Japanese. Detect the language of the input text and translate it to Japanese. {politenessInstructionJP} Follow this format exactly: 'Japanese text || romaji'. The Japanese text should come first, followed by \' || \' separator, then the romaji (transliteration to Latin alphabet). Provide no explanations or additional text.";
        }
        else if (sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) && targetLanguage.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            // This is for /en command, target is English
            systemPrompt = $"You are a direct translator to English. Detect the language of the input text and translate it to English. {politenessInstructionEN} If the source appears to be Japanese, include romaji (Japanese transliterated into Latin alphabet) in parentheses after the translation. Format as: 'English translation (romaji)' for Japanese or just 'English translation' for other languages. Return ONLY the translated text, no explanations or additional context.";
        }
        else if (sourceLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase) && targetLanguage.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            systemPrompt = $"You are a direct translator from Japanese to English. {politenessInstructionEN} Translate the content and include romaji (Japanese transliterated into Latin alphabet) in parentheses after the translation. Format as: 'English translation (romaji)'. Return ONLY the translated text with romaji, no explanations or additional context.";
        }
        else if (sourceLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) && targetLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
        {
            // This case is not directly used by /jp if "auto" is effective, but good to have.
            systemPrompt = $"You are a direct translator from English to Japanese. {politenessInstructionJP} Follow this format exactly: 'Japanese text || romaji'. The Japanese text should come first, followed by \' || \' separator, then the romaji (transliteration to Latin alphabet). Provide no explanations or additional text.";
        }
        else if ((sourceLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)) && 
                 targetLanguage.Equals("Chinese", StringComparison.OrdinalIgnoreCase))
        {
            // Translation to Chinese with pinyin
            string formalityCn = useFormalLanguage ? "formal" : "casual/conversational";
            systemPrompt = $"You are a direct translator to Chinese (Simplified). Detect the input language and translate it to {formalityCn} Chinese. Include pinyin in parentheses after the translation. Format as: 'Chinese translation (pinyin)'. Return ONLY the translated text with pinyin, no explanations or additional context.";
        }
        else if ((sourceLanguage.Equals("Japanese", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("English", StringComparison.OrdinalIgnoreCase) || 
                 sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)) && 
                 targetLanguage.Equals("Indonesian", StringComparison.OrdinalIgnoreCase))
        {
            // Translation to Indonesian
            string formalityId = useFormalLanguage ? "formal (baku)" : "informal/conversational (tidak baku)";
            systemPrompt = $"You are a direct translator to Indonesian. Detect if the input is Japanese or English and translate it to {formalityId} Indonesian. Provide only the translated text without explanations or additional context.";
        }
        else
        {
            // Default or unsupported language pair - add generic politeness if possible, or leave as is if too complex.
            // For simplicity, we'll not add politeness to this generic fallback for now.
            systemPrompt = $"Translate the following text from {sourceLanguage} to {targetLanguage}. Provide only the translated text.";
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