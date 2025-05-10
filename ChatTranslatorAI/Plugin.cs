using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ChatTranslatorAI.Windows;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ImGuiNET;
using System.Numerics;

namespace ChatTranslatorAI;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CommandName = "/pmycommand";
    private const string ConfigCommandName = "/transconfig";
    private const string JpTranslateCommandName = "/jp";
    private const string EnTranslateCommandName = "/en";
    private const string CnTranslateCommandName = "/cn";

    public Configuration Configuration { get; init; }
    private readonly OpenRouterTranslator _translator;

    public readonly WindowSystem WindowSystem = new("Chat Translator AI");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        _translator = new OpenRouterTranslator();

        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        CommandManager.AddHandler(ConfigCommandName, new CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Open the Chat Translator AI configuration window."
        });

        CommandManager.AddHandler(JpTranslateCommandName, new CommandInfo(OnJpTranslateCommand)
        {
            HelpMessage = "Translates your English message to Japanese and sends it. Usage: /jp <message> or /jp <channel> <message> (e.g., /jp say Hello or /jp party Hello)"
        });

        CommandManager.AddHandler(EnTranslateCommandName, new CommandInfo(OnEnTranslateCommand)
        {
            HelpMessage = "Translates any language message to English. Usage: /en <message>"
        });

        CommandManager.AddHandler(CnTranslateCommandName, new CommandInfo(OnCnTranslateCommand)
        {
            HelpMessage = "Translates any language message to Chinese with pinyin. Usage: /cn <message>"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        ChatGui.ChatMessage += OnChatMessage;

        Log.Information($"Chat Translator AI Plugin loaded successfully.");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        Configuration.Save();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(ConfigCommandName);
        CommandManager.RemoveHandler(JpTranslateCommandName);
        CommandManager.RemoveHandler(EnTranslateCommandName);
        CommandManager.RemoveHandler(CnTranslateCommandName);
        ChatGui.ChatMessage -= OnChatMessage;
        Log.Information("Chat Translator AI Plugin disposed.");
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    private void OnConfigCommand(string command, string args)
    {
        ConfigWindow.Toggle();
    }

    private async void OnJpTranslateCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            ChatGui.PrintError("Usage: /jp <message> or /jp <channel> <message> (e.g., /jp say Hello or /jp party Hello)", "ChatTL");
            return;
        }

        if (string.IsNullOrWhiteSpace(Configuration.OpenRouterApiKey) || 
            string.IsNullOrWhiteSpace(Configuration.OpenRouterModel))
        {
            ChatGui.PrintError("Chat Translator AI is not configured. Please set API key and model via /transconfig.", "ChatTL Error", (ushort)0xE05B);
            return;
        }

        // Parse the args to check if a channel is specified
        string targetChannel = "say"; // Default to /say
        string textToTranslate = args;

        string[] argParts = args.Split(new[] { ' ' }, 2);
        if (argParts.Length > 1)
        {
            string potentialChannel = argParts[0].ToLower();
            if (IsValidChatChannel(potentialChannel))
            {
                targetChannel = potentialChannel;
                textToTranslate = argParts[1];
            }
        }

        try
        {
            // Indicate that translation is in progress
            ChatGui.Print($"Translating to Japanese...", "ChatTL");
            
            // Instead of specifying English as the source language, we'll use "auto"
            // to let the AI model detect the language automatically
            string? translatedJpText = await _translator.TranslateTextAsync(
                textToTranslate, 
                Configuration.OpenRouterApiKey, 
                Configuration.OpenRouterModel,
                "auto", // Auto-detect source language
                "Japanese",
                Configuration.UseFormalLanguage
            );

            if (!string.IsNullOrWhiteSpace(translatedJpText) && !translatedJpText.StartsWith("Error:"))
            {
                Log.Debug($"Successfully translated to JP: {translatedJpText}");
                
                // Parse the result which should be in the format "Japanese text || romaji"
                string japaneseTextOnly = translatedJpText;
                string displayText = translatedJpText;
                
                // Check if the response contains the separator
                if (translatedJpText.Contains(" || "))
                {
                    string[] parts = translatedJpText.Split(new[] { " || " }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        japaneseTextOnly = parts[0].Trim();
                        string romaji = parts[1].Trim();
                        displayText = $"{japaneseTextOnly} ({romaji})";
                    }
                }
                
                // Display the translated text with romaji if available, using the configured JP command color
                var messageBuilder = new SeStringBuilder()
                    .AddUiForeground("➤ ", (ushort)ColorToUiForegroundId(Configuration.JpCommandColor))
                    .AddUiForeground(displayText, (ushort)ColorToUiForegroundId(Configuration.JpCommandColor));
                var message = messageBuilder.Build();
                
                ChatGui.Print(message);
                
                // Silently copy ONLY the Japanese text to clipboard without notification
                try
                {
                    ImGui.SetClipboardText(japaneseTextOnly);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not copy to clipboard");
                }
            }
            else
            {
                string errorMessage = string.IsNullOrWhiteSpace(translatedJpText) ? "Translation returned empty." : translatedJpText;
                ChatGui.PrintError($"Failed to translate to Japanese: {errorMessage}", "ChatTL Error", (ushort)0xE05B);
                Log.Warning($"Translation service reported an issue: {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            ChatGui.PrintError($"Exception during translation: {ex.Message}", "ChatTL Error", (ushort)0xE05B);
            Log.Error(ex, "Exception during OnJpTranslateCommand.");
        }
    }

    private async void OnEnTranslateCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            ChatGui.PrintError("Usage: /en <message>", "ChatTL");
            return;
        }

        if (string.IsNullOrWhiteSpace(Configuration.OpenRouterApiKey) || 
            string.IsNullOrWhiteSpace(Configuration.OpenRouterModel))
        {
            ChatGui.PrintError("Chat Translator AI is not configured. Please set API key and model via /transconfig.", "ChatTL Error", (ushort)0xE05B);
            return;
        }

        string textToTranslate = args;

        try
        {
            ChatGui.Print($"Translating to English...", "ChatTL");
            
            string? translatedEnText = await _translator.TranslateTextAsync(
                textToTranslate, 
                Configuration.OpenRouterApiKey, 
                Configuration.OpenRouterModel,
                "auto", // Changed from "Japanese" to "auto" to detect any language
                "English",
                Configuration.UseFormalLanguage
            );

            if (!string.IsNullOrWhiteSpace(translatedEnText) && !translatedEnText.StartsWith("Error:"))
            {
                Log.Debug($"Successfully translated to EN: {translatedEnText}");
                
                // Display the translated text (which includes romaji from the prompt), using the configured EN command color
                var messageBuilder = new SeStringBuilder()
                    .AddUiForeground("[ChatTL-EN]: ", (ushort)ColorToUiForegroundId(Configuration.EnCommandColor))
                    .AddUiForeground(translatedEnText, (ushort)ColorToUiForegroundId(Configuration.EnCommandColor));
                ChatGui.Print(messageBuilder.Build());

                // Extract and copy only the English part to clipboard
                string englishTextOnly = translatedEnText; // Default to the full text
                int lastOpenParenIndex = translatedEnText.LastIndexOf('(');

                if (lastOpenParenIndex > 0) // Check if '(' exists and is not the first character
                {
                    int lastCloseParenIndex = translatedEnText.LastIndexOf(')');
                    // Ensure the ')' comes after the '('
                    if (lastCloseParenIndex > lastOpenParenIndex)
                    {
                        englishTextOnly = translatedEnText.Substring(0, lastOpenParenIndex).Trim();
                    }
                }

                try
                {
                    ImGui.SetClipboardText(englishTextOnly);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not copy EN translation to clipboard");
                }
            }
            else
            {
                string errorMessage = string.IsNullOrWhiteSpace(translatedEnText) ? "Translation returned empty." : translatedEnText;
                ChatGui.PrintError($"Failed to translate to English: {errorMessage}", "ChatTL Error", (ushort)0xE05B);
                Log.Warning($"EN Translation service reported an issue: {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            ChatGui.PrintError($"Exception during EN translation: {ex.Message}", "ChatTL Error", (ushort)0xE05B);
            Log.Error(ex, "Exception during OnEnTranslateCommand.");
        }
    }

    private async void OnCnTranslateCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            ChatGui.PrintError("Usage: /cn <message>", "ChatTL");
            return;
        }

        if (string.IsNullOrWhiteSpace(Configuration.OpenRouterApiKey) || 
            string.IsNullOrWhiteSpace(Configuration.OpenRouterModel))
        {
            ChatGui.PrintError("Chat Translator AI is not configured. Please set API key and model via /transconfig.", "ChatTL Error", (ushort)0xE05B);
            return;
        }

        string textToTranslate = args;

        try
        {
            ChatGui.Print($"Translating to Chinese with pinyin...", "ChatTL");
            
            string? translatedCnText = await _translator.TranslateTextAsync(
                textToTranslate, 
                Configuration.OpenRouterApiKey, 
                Configuration.OpenRouterModel,
                "auto", // Auto-detect source language
                "Chinese",
                Configuration.UseFormalLanguage
            );

            if (!string.IsNullOrWhiteSpace(translatedCnText) && !translatedCnText.StartsWith("Error:"))
            {
                Log.Debug($"Successfully translated to CN: {translatedCnText}");
                
                // Display the translated text (which includes pinyin from the prompt), using the configured CN command color
                var messageBuilder = new SeStringBuilder()
                    .AddUiForeground("[ChatTL-CN]: ", (ushort)ColorToUiForegroundId(Configuration.CnCommandColor))
                    .AddUiForeground(translatedCnText, (ushort)ColorToUiForegroundId(Configuration.CnCommandColor));
                ChatGui.Print(messageBuilder.Build());

                // Extract and copy only the Chinese part to clipboard
                string chineseTextOnly = translatedCnText; // Default to the full text
                int lastOpenParenIndex = translatedCnText.LastIndexOf('(');

                if (lastOpenParenIndex > 0) // Check if '(' exists and is not the first character
                {
                    int lastCloseParenIndex = translatedCnText.LastIndexOf(')');
                    // Ensure the ')' comes after the '('
                    if (lastCloseParenIndex > lastOpenParenIndex)
                    {
                        chineseTextOnly = translatedCnText.Substring(0, lastOpenParenIndex).Trim();
                    }
                }

                try
                {
                    ImGui.SetClipboardText(chineseTextOnly);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not copy CN translation to clipboard");
                }
            }
            else
            {
                string errorMessage = string.IsNullOrWhiteSpace(translatedCnText) ? "Translation returned empty." : translatedCnText;
                ChatGui.PrintError($"Failed to translate to Chinese: {errorMessage}", "ChatTL Error", (ushort)0xE05B);
                Log.Warning($"CN Translation service reported an issue: {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            ChatGui.PrintError($"Exception during CN translation: {ex.Message}", "ChatTL Error", (ushort)0xE05B);
            Log.Error(ex, "Exception during OnCnTranslateCommand.");
        }
    }

    // List of valid chat channel commands
    private bool IsValidChatChannel(string channel)
    {
        string[] validChannels = {
            "say", "s",
            "yell", "y",
            "shout", "sh",
            "tell", "t", "r", "whisper", "w",
            "party", "p",
            "alliance", "a",
            "freecompany", "fc",
            "linkshell1", "ls1", "linkshell2", "ls2", "linkshell3", "ls3", 
            "linkshell4", "ls4", "linkshell5", "ls5", "linkshell6", "ls6",
            "linkshell7", "ls7", "linkshell8", "ls8",
            "cwlinkshell1", "cwls1", "cwlinkshell2", "cwls2", "cwlinkshell3", "cwls3",
            "cwlinkshell4", "cwls4", "cwlinkshell5", "cwls5", "cwlinkshell6", "cwls6",
            "cwlinkshell7", "cwls7", "cwlinkshell8", "cwls8",
            "echo", "e"
        };
        return Array.Exists(validChannels, c => c == channel);
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!Configuration.EnableTranslation)
        {
            return;
        }
        
        if (string.IsNullOrWhiteSpace(Configuration.OpenRouterApiKey) || 
            string.IsNullOrWhiteSpace(Configuration.OpenRouterModel))
        {
            if (ContainsJapanese(message.TextValue))
            {
                ShowApiConfigurationError();
            }
            return;
        }

        if (!IsChannelEnabled(type))
        {
            return;
        }

        string messageText = message.TextValue;
        string senderText = sender.TextValue;

        // ---- START DEBUG LOGGING ----
        Log.Debug($"OnChatMessage triggered for type: {type}");
        Log.Debug($"Received sender.TextValue: '{senderText}'");
        if (ClientState.LocalPlayer != null)
        {
            Log.Debug($"ClientState.LocalPlayer.Name.TextValue: '{ClientState.LocalPlayer.Name.TextValue}'");
        }
        else
        {
            Log.Debug("ClientState.LocalPlayer is null.");
        }
        Log.Debug($"IsSelfMessage check result for '{senderText}': {IsSelfMessage(senderText)}");
        // ---- END DEBUG LOGGING ----

        if (messageText.Contains("[ChatTL]") || messageText.Contains("[TR]:"))
        {
            return;
        }

        if (IsSelfMessage(senderText))
        {
            return;
        }

        if (!ContainsJapanese(messageText))
        {
            return;
        }

        Log.Debug($"Attempting to translate Japanese message: {messageText}");

        Task.Run(async () =>
        {
            try
            {
                string? translatedText = await _translator.TranslateTextAsync(
                    messageText, 
                    Configuration.OpenRouterApiKey, 
                    Configuration.OpenRouterModel,
                    "Japanese",
                    "English",
                    Configuration.UseFormalLanguage
                );

                if (!string.IsNullOrWhiteSpace(translatedText) && !translatedText.StartsWith("Error:"))
                {
                    // Moved the English translation display to the conditional blocks above
                    Log.Debug($"Translation successful: {translatedText}");
                    
                    // Process additional language translations if any are enabled
                    bool needsEnglishTranslation = Configuration.EnabledLanguages.TryGetValue("English", out bool isEnglishEnabled) && isEnglishEnabled;
                    
                    // Only continue with English translation if it's enabled
                    if (needsEnglishTranslation)
                    {
                        // Get the appropriate color for this chat type
                        Vector4 textColor = new Vector4(1, 1, 1, 1); // Default white
                        if (Configuration.ChatColors.TryGetValue(type, out Vector4 channelColor))
                        {
                            textColor = channelColor;
                        }
                        
                        // Create a colored message
                        var translatedMessage = new SeStringBuilder()
                            .AddUiForeground($"[EN][{senderText}]: ", (ushort)ColorToUiForegroundId(textColor))
                            .AddUiForeground(translatedText, (ushort)ColorToUiForegroundId(textColor))
                            .Build();

                        ChatGui.Print(translatedMessage, "ChatTL");
                    }
                    
                    // Process additional language translations (any enabled language)
                    foreach (var languagePair in Configuration.EnabledLanguages)
                    {
                        string language = languagePair.Key;
                        bool isEnabled = languagePair.Value;
                        
                        // Skip languages that aren't enabled and skip English as we already handled it
                        if (!isEnabled || language == "English")
                        {
                            continue;
                        }
                        
                        // Translate to the selected language
                        TranslateToLanguage(messageText, senderText, type, language);
                    }
                }
                else if (translatedText != null)
                {
                    if (translatedText.Contains("API Key"))
                    {
                        ShowApiKeyError();
                    }
                    else if (translatedText.Contains("Model"))
                    {
                        ShowModelError(Configuration.OpenRouterModel);
                    }
                    else
                    {
                        ShowTranslationError(translatedText);
                    }
                    
                    Log.Warning($"Translation service reported an issue: {translatedText}");
                }
            }
            catch (Exception ex)
            {
                ShowTranslationError($"Error: {ex.Message}");
                Log.Error(ex, "Exception during asynchronous translation task.");
            }
        });
    }

    private void ShowApiConfigurationError()
    {
        if (_lastApiErrorTime.AddMinutes(5) < DateTime.Now)
        {
            ChatGui.Print(
                "Japanese text detected but Chat Translator AI is not configured correctly. Please set your OpenRouter API key and model using /transconfig", 
                "ChatTL Error", 
                (ushort)0xE05B);
            
            _lastApiErrorTime = DateTime.Now;
        }
    }

    private void ShowApiKeyError()
    {
        ChatGui.Print(
            "Error with OpenRouter API key. Please check your API key in the Chat Translator AI settings (/transconfig)", 
            "ChatTL Error", 
            (ushort)0xE05B);
    }

    private void ShowModelError(string modelName)
    {
        ChatGui.Print(
            $"Error with model \"{modelName}\". Please check your model selection in Chat Translator AI settings (/transconfig)", 
            "ChatTL Error", 
            (ushort)0xE05B);
    }

    private void ShowTranslationError(string errorDetails)
    {
        ChatGui.Print(
            $"Translation failed: {errorDetails}. Please try again later or check your settings (/transconfig)", 
            "ChatTL Error", 
            (ushort)0xE05B);
    }

    private DateTime _lastApiErrorTime = DateTime.MinValue;

    private bool IsChannelEnabled(XivChatType type)
    {
        if (Configuration.EnabledChatTypes.TryGetValue(type, out bool isEnabled))
        {
            return isEnabled;
        }
        return false;
    }

    private bool IsSelfMessage(string senderName)
    {
        if (ClientState.LocalPlayer == null || string.IsNullOrEmpty(ClientState.LocalPlayer.Name?.TextValue))
        {
            return false;
        }
        // Check if the received senderName ends with the LocalPlayer's name.
        // This handles cases where prefixes (like party icons) are added to the sender's name in chat.
        return senderName.EndsWith(ClientState.LocalPlayer.Name.TextValue, StringComparison.Ordinal);
    }

    private bool ContainsJapanese(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        
        var japaneseRegex = new Regex(@"[\u3040-\u309F\u30A0-\u30FF\uFF00-\uFFEF\u4E00-\u9FAF\u3000-\u303F]");
        var matches = japaneseRegex.Matches(text);
        
        if (matches.Count >= 2)
        {
            return true;
        }
        
        var japaneseCharCount = matches.Count;
        var totalCharCount = text.Length;
        
        return japaneseCharCount > 0 && ((double)japaneseCharCount / totalCharCount) >= 0.3;
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();

    // Helper method to convert Vector4 color to UI foreground ID
    private uint ColorToUiForegroundId(Vector4 color)
    {
        // FFXIV UI colors are represented as 0xAABBGGRR (ABGR) in hex
        // This method converts our RGBA Vector4 to the closest UI foreground ID
        
        // Convert 0-1 range to 0-255 range
        byte r = (byte)(color.X * 255);
        byte g = (byte)(color.Y * 255);
        byte b = (byte)(color.Z * 255);
        
        // For simplicity, we'll map to one of the 16 standard colors
        // This can be expanded to more sophisticated color matching if needed
        return GetClosestColorId(r, g, b);
    }
    
    // Simple mapping to standard FFXIV colors
    private uint GetClosestColorId(byte r, byte g, byte b)
    {
        // Basic color table of common FFXIV chat colors mapped to their UI foreground IDs
        // This is a simplified approach - a more comprehensive solution would use 
        // actual distance calculations between colors
        
        // Whites and Grays
        if (r > 200 && g > 200 && b > 200) return 1; // White
        if (r > 150 && g > 150 && b > 150) return 2; // Light Gray
        
        // Blues
        if (b > 200 && r < 100 && g < 100) return 37; // Dark Blue
        if (b > 180 && r < 150 && g > 150) return 506; // Light Blue
        if (b > 180 && g > 100 && r < 100) return 45; // Blue
        
        // Greens
        if (g > 200 && r < 100 && b < 100) return 42; // Green
        if (g > 180 && r > 180 && b < 100) return 55; // Yellow-Green
        
        // Reds and Pinks
        if (r > 200 && g < 100 && b < 100) return 14; // Red
        if (r > 200 && g < 150 && b > 150) return 541; // Pink
        
        // Yellows and Oranges
        if (r > 200 && g > 200 && b < 100) return 24; // Yellow
        if (r > 200 && g > 130 && b < 100) return 34; // Orange
        
        // Purples
        if (r > 150 && g < 100 && b > 150) return 65; // Purple
        
        // Cyans
        if (g > 150 && b > 150 && r < 100) return 52; // Cyan
        
        // Default
        return 0; // Default color
    }

    // Helper method for translating to a specific language
    private void TranslateToLanguage(string messageText, string senderText, XivChatType type, string language)
    {
        Task.Run(async () =>
        {
            try
            {
                // Get language code for display
                string langCode = GetLanguageCode(language);
                
                string? translatedText = await _translator.TranslateTextAsync(
                    messageText,
                    Configuration.OpenRouterApiKey,
                    Configuration.OpenRouterModel,
                    "auto", // Auto-detect source language
                    language,
                    Configuration.UseFormalLanguage
                );

                if (!string.IsNullOrWhiteSpace(translatedText) && !translatedText.StartsWith("Error:"))
                {
                    // Use a specific color based on the language
                    Vector4 languageColor = GetLanguageColor(language);
                    
                    // Create a colored message with language indicator
                    var translatedMessage = new SeStringBuilder()
                        .AddUiForeground($"[{langCode}][{senderText}]: ", (ushort)ColorToUiForegroundId(languageColor))
                        .AddUiForeground(translatedText, (ushort)ColorToUiForegroundId(languageColor))
                        .Build();

                    ChatGui.Print(translatedMessage, "ChatTL");
                    Log.Debug($"{language} translation successful: {translatedText}");
                }
                else if (translatedText != null)
                {
                    Log.Warning($"{language} translation service reported an issue: {translatedText}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Exception during {language} translation task.");
            }
        });
    }
    
    // Helper method to get a language code for display
    private string GetLanguageCode(string language)
    {
        // Return standard 2-3 letter codes for each language
        switch (language)
        {
            case "English": return "EN";
            case "Indonesian": return "ID";
            case "Spanish": return "ES";
            case "Japanese": return "JP";
            case "Korean": return "KR";
            case "Chinese": return "CN";
            case "French": return "FR";
            case "German": return "DE";
            case "Russian": return "RU";
            case "Portuguese": return "PT";
            case "Italian": return "IT";
            case "Arabic": return "AR";
            case "Hindi": return "HI";
            case "Turkish": return "TR";
            case "Vietnamese": return "VI";
            case "Thai": return "TH";
            default: return language.Substring(0, 2).ToUpper(); // Fallback to first 2 letters
        }
    }
    
    // Helper method to get a color for each language
    private Vector4 GetLanguageColor(string language)
    {
        // Return a unique color for each language
        switch (language)
        {
            case "Indonesian": return new Vector4(1.0f, 0.7f, 0.4f, 1.0f); // Orange
            case "Spanish": return new Vector4(0.9f, 0.2f, 0.2f, 1.0f); // Red
            case "Japanese": return new Vector4(0.9f, 0.4f, 0.7f, 1.0f); // Pink
            case "Korean": return new Vector4(0.3f, 0.6f, 0.9f, 1.0f); // Blue
            case "Chinese": return new Vector4(0.8f, 0.2f, 0.2f, 1.0f); // Red
            case "French": return new Vector4(0.1f, 0.3f, 0.8f, 1.0f); // Blue
            case "German": return new Vector4(0.3f, 0.3f, 0.3f, 1.0f); // Dark gray
            case "Russian": return new Vector4(0.7f, 0.0f, 0.0f, 1.0f); // Dark red
            case "Portuguese": return new Vector4(0.0f, 0.8f, 0.4f, 1.0f); // Green
            case "Italian": return new Vector4(0.0f, 0.7f, 0.2f, 1.0f); // Green
            case "Arabic": return new Vector4(0.0f, 0.5f, 0.3f, 1.0f); // Dark green
            case "Hindi": return new Vector4(1.0f, 0.5f, 0.0f, 1.0f); // Orange
            case "Turkish": return new Vector4(0.9f, 0.0f, 0.0f, 1.0f); // Red
            case "Vietnamese": return new Vector4(0.9f, 0.8f, 0.0f, 1.0f); // Yellow
            case "Thai": return new Vector4(0.0f, 0.0f, 0.8f, 1.0f); // Blue
            default: return new Vector4(0.7f, 0.7f, 0.7f, 1.0f); // Gray (fallback)
        }
    }
}
