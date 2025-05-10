using System;
using System.Threading.Tasks;
using UnityEngine;
using SeString;

public class Plugin : MonoBehaviour
{
    private void OnChatMessage(XivChatType type, string senderText, string messageText)
    {
        if (Configuration.EnableIndonesianTranslation)
        {
            TranslateToIndonesian(messageText, senderText, type);
        }
    }

    // Helper method for translating to Indonesian
    private void TranslateToIndonesian(string messageText, string senderText, XivChatType type)
    {
        Task.Run(async () =>
        {
            try
            {
                string? translatedIdText = await _translator.TranslateTextAsync(
                    messageText,
                    Configuration.OpenRouterApiKey,
                    Configuration.OpenRouterModel,
                    "auto", // Auto-detect language
                    "Indonesian",
                    Configuration.UseFormalLanguage
                );

                if (!string.IsNullOrWhiteSpace(translatedIdText) && !translatedIdText.StartsWith("Error:"))
                {
                    // Use a specific color for Indonesian translations - orange tint
                    Vector4 indonesianColor = new Vector4(1.0f, 0.7f, 0.4f, 1.0f);
                    
                    // Create a colored message with ID indicator
                    var translatedMessage = new SeStringBuilder()
                        .AddUiForeground($"[ID][{senderText}]: ", (ushort)ColorToUiForegroundId(indonesianColor))
                        .AddUiForeground(translatedIdText, (ushort)ColorToUiForegroundId(indonesianColor))
                        .Build();

                    ChatGui.Print(translatedMessage, "ChatTL");
                    Log.Debug($"Indonesian translation successful: {translatedIdText}");
                }
                else if (translatedIdText != null)
                {
                    Log.Warning($"Indonesian translation service reported an issue: {translatedIdText}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception during Indonesian translation task.");
            }
        });
    }

    private void DrawUI() => WindowSystem.Draw();
} 