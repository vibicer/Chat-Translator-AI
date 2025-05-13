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
using System.Linq;
using System.Text;
using System.Collections.Generic;

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
    private const string CntTranslateCommandName = "/cnt";

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
            HelpMessage = "Translates any language message to Chinese Simplified with pinyin. Usage: /cn <message>"
        });

        CommandManager.AddHandler(CntTranslateCommandName, new CommandInfo(OnCntTranslateCommand)
        {
            HelpMessage = "Translates any language message to Chinese Traditional with pinyin. Usage: /cnt <message>"
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
        CommandManager.RemoveHandler(CntTranslateCommandName);
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
            
            // Get conversation context if enabled
            string contextPrefix = string.Empty;
            if (Configuration.EnableContextMemory)
            {
                contextPrefix = GetChannelContext(targetChannel);
                if (!string.IsNullOrWhiteSpace(contextPrefix))
                {
                    Log.Debug($"Including context for /jp command in channel {targetChannel}: {contextPrefix}");
                }
            }
            
            // Prepare the text to translate with context if available
            string textWithContext = string.IsNullOrWhiteSpace(contextPrefix) 
                ? textToTranslate 
                : $"{contextPrefix}\n\nTranslate this message to Japanese: {textToTranslate}";
            
            // Instead of specifying English as the source language, we'll use "auto"
            // to let the AI model detect the language automatically
            string? translatedJpText = await _translator.TranslateTextAsync(
                textWithContext, 
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

        // Get target channel and message text
        string targetChannel = "say"; // Default to say
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
            ChatGui.Print($"Translating to English...", "ChatTL");
            
            // Get conversation context if enabled
            string contextPrefix = string.Empty;
            if (Configuration.EnableContextMemory)
            {
                contextPrefix = GetChannelContext(targetChannel);
                if (!string.IsNullOrWhiteSpace(contextPrefix))
                {
                    Log.Debug($"Including context for /en command in channel {targetChannel}: {contextPrefix}");
                }
            }
            
            // Prepare the text to translate with context if available
            string textWithContext = string.IsNullOrWhiteSpace(contextPrefix) 
                ? textToTranslate 
                : $"{contextPrefix}\n\nTranslate this message to English: {textToTranslate}";
            
            string? translatedEnText = await _translator.TranslateTextAsync(
                textWithContext, 
                Configuration.OpenRouterApiKey, 
                Configuration.OpenRouterModel,
                "auto", // Changed from "Japanese" to "auto" to detect any language
                "English",
                Configuration.UseFormalLanguage
            );

            if (!string.IsNullOrWhiteSpace(translatedEnText) && !translatedEnText.StartsWith("Error:"))
            {
                Log.Debug($"Successfully translated to EN: {translatedEnText}");
                
                // Parse the result which may be in format "English text || romaji" or just English text
                string englishTextOnly = translatedEnText;
                string displayText = translatedEnText;
                
                // Check if the response contains the separator
                if (translatedEnText.Contains(" || "))
                {
                    string[] parts = translatedEnText.Split(new[] { " || " }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        englishTextOnly = parts[0].Trim();
                        string romaji = parts[1].Trim();
                        displayText = $"{englishTextOnly} ({romaji})";
                    }
                }
                
                // Display the translated text (which includes romaji from the prompt), using the configured EN command color
                var messageBuilder = new SeStringBuilder()
                    .AddUiForeground("[ChatTL-EN]: ", (ushort)ColorToUiForegroundId(Configuration.EnCommandColor))
                    .AddUiForeground(displayText, (ushort)ColorToUiForegroundId(Configuration.EnCommandColor));
                ChatGui.Print(messageBuilder.Build());

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

        // Get target channel and message text
        string targetChannel = "say"; // Default to say
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
            ChatGui.Print($"Translating to Chinese with pinyin...", "ChatTL");
            
            // Get conversation context if enabled
            string contextPrefix = string.Empty;
            if (Configuration.EnableContextMemory)
            {
                contextPrefix = GetChannelContext(targetChannel);
                if (!string.IsNullOrWhiteSpace(contextPrefix))
                {
                    Log.Debug($"Including context for /cn command in channel {targetChannel}: {contextPrefix}");
                }
            }
            
            // Prepare the text to translate with context if available
            string textWithContext = string.IsNullOrWhiteSpace(contextPrefix) 
                ? textToTranslate 
                : $"{contextPrefix}\n\nTranslate this message to Chinese (Simplified): {textToTranslate}";
            
            string? translatedCnText = await _translator.TranslateTextAsync(
                textWithContext, 
                Configuration.OpenRouterApiKey, 
                Configuration.OpenRouterModel,
                "auto", // Auto-detect source language
                "Chinese",
                Configuration.UseFormalLanguage
            );

            if (!string.IsNullOrWhiteSpace(translatedCnText) && !translatedCnText.StartsWith("Error:"))
            {
                Log.Debug($"Successfully translated to CN: {translatedCnText}");
                
                // Parse the result which may be in format "Chinese text || pinyin" or just Chinese text
                string chineseTextOnly = translatedCnText;
                string displayText = translatedCnText;
                
                // Check if the response contains the separator
                if (translatedCnText.Contains(" || "))
                {
                    string[] parts = translatedCnText.Split(new[] { " || " }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        chineseTextOnly = parts[0].Trim();
                        string pinyin = parts[1].Trim();
                        displayText = $"{chineseTextOnly} ({pinyin})";
                    }
                }
                
                // Display the translated text with proper formatting
                var messageBuilder = new SeStringBuilder()
                    .AddUiForeground("[ChatTL-CN]: ", (ushort)ColorToUiForegroundId(Configuration.CnCommandColor))
                    .AddUiForeground(displayText, (ushort)ColorToUiForegroundId(Configuration.CnCommandColor));
                ChatGui.Print(messageBuilder.Build());

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

    private async void OnCntTranslateCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            ChatGui.PrintError("Usage: /cnt <message>", "ChatTL");
            return;
        }

        if (string.IsNullOrWhiteSpace(Configuration.OpenRouterApiKey) || 
            string.IsNullOrWhiteSpace(Configuration.OpenRouterModel))
        {
            ChatGui.PrintError("Chat Translator AI is not configured. Please set API key and model via /transconfig.", "ChatTL Error", (ushort)0xE05B);
            return;
        }

        // Get target channel and message text
        string targetChannel = "say"; // Default to say
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
            ChatGui.Print($"Translating to Chinese Traditional with pinyin...", "ChatTL");
            
            // Get conversation context if enabled
            string contextPrefix = string.Empty;
            if (Configuration.EnableContextMemory)
            {
                contextPrefix = GetChannelContext(targetChannel);
                if (!string.IsNullOrWhiteSpace(contextPrefix))
                {
                    Log.Debug($"Including context for /cnt command in channel {targetChannel}: {contextPrefix}");
                }
            }
            
            // Prepare the text to translate with context if available
            string textWithContext = string.IsNullOrWhiteSpace(contextPrefix) 
                ? textToTranslate 
                : $"{contextPrefix}\n\nTranslate this message to Chinese (Traditional): {textToTranslate}";
            
            string? translatedCntText = await _translator.TranslateTextAsync(
                textWithContext, 
                Configuration.OpenRouterApiKey, 
                Configuration.OpenRouterModel,
                "auto", // Auto-detect source language
                "ChineseTraditional",
                Configuration.UseFormalLanguage
            );

            if (!string.IsNullOrWhiteSpace(translatedCntText) && !translatedCntText.StartsWith("Error:"))
            {
                Log.Debug($"Successfully translated to CNT: {translatedCntText}");
                
                // Parse the result which may be in format "Chinese text || pinyin" or just Chinese text
                string chineseTextOnly = translatedCntText;
                string displayText = translatedCntText;
                
                // Check if the response contains the separator
                if (translatedCntText.Contains(" || "))
                {
                    string[] parts = translatedCntText.Split(new[] { " || " }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        chineseTextOnly = parts[0].Trim();
                        string pinyin = parts[1].Trim();
                        displayText = $"{chineseTextOnly} ({pinyin})";
                    }
                }
                
                // Display the translated text with proper formatting
                var messageBuilder = new SeStringBuilder()
                    .AddUiForeground("[ChatTL-CNT]: ", (ushort)ColorToUiForegroundId(Configuration.CntCommandColor))
                    .AddUiForeground(displayText, (ushort)ColorToUiForegroundId(Configuration.CntCommandColor));
                ChatGui.Print(messageBuilder.Build());

                try
                {
                    ImGui.SetClipboardText(chineseTextOnly);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not copy CNT translation to clipboard");
                }
            }
            else
            {
                string errorMessage = string.IsNullOrWhiteSpace(translatedCntText) ? "Translation returned empty." : translatedCntText;
                ChatGui.PrintError($"Failed to translate to Chinese Traditional: {errorMessage}", "ChatTL Error", (ushort)0xE05B);
                Log.Warning($"CNT Translation service reported an issue: {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            ChatGui.PrintError($"Exception during CNT translation: {ex.Message}", "ChatTL Error", (ushort)0xE05B);
            Log.Error(ex, "Exception during OnCntTranslateCommand.");
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

        // Store message in context memory regardless of language, if enabled
        if (Configuration.EnableContextMemory)
        {
            StoreMessageInContext(type.ToString(), senderText, messageText);
        }

        // Determine if the message contains Japanese characters
        bool messageIsJapanese = ContainsJapanese(messageText);
        
        // Determine if English translation is enabled and needed
        bool needsEnglishTranslationFromJapanese = Configuration.EnabledLanguages.TryGetValue("English", out bool isEnglishEnabledGlobally) && isEnglishEnabledGlobally && messageIsJapanese;

        // Determine if any other language translation is needed
        bool needsOtherLanguageTranslation = Configuration.EnabledLanguages.Any(lang => lang.Value && lang.Key != "English");

        // If no translation is needed at all, exit
        if (!needsEnglishTranslationFromJapanese && !needsOtherLanguageTranslation)
        {
            // However, if English is globally enabled and the message is NOT Japanese,
            // we might still want to translate it if other languages are selected.
            // This specific case is handled inside the loop for other languages.
            // For now, if English isn't needed for a Japanese message, and no other languages are active, exit.
            if (!Configuration.EnabledLanguages.Any(lang => lang.Value)) // if no languages are selected at all
                 return;
            if (messageIsJapanese && !needsEnglishTranslationFromJapanese && !needsOtherLanguageTranslation) // if Japanese but English not selected, and no other lang
                 return;
            if (!messageIsJapanese && !needsOtherLanguageTranslation) // if not Japanese and no other languages selected
                 return;
        }
        
        Log.Debug($"Processing message: '{messageText}'. IsJapanese: {messageIsJapanese}, NeedsEnglishFromJP: {needsEnglishTranslationFromJapanese}, NeedsOther: {needsOtherLanguageTranslation}");

        Task.Run(async () =>
        {
            try
            {
                string? primaryTranslatedToEnglishText = null;

                if (needsEnglishTranslationFromJapanese)
                {
                    Log.Debug($"Attempting to translate Japanese message to English: {messageText}");
                    primaryTranslatedToEnglishText = await _translator.TranslateTextAsync(
                        messageText,
                        Configuration.OpenRouterApiKey,
                        Configuration.OpenRouterModel,
                        "Japanese", // Source is Japanese
                        "English",  // Target is English
                        false // <--- Ensure automatic JP->EN uses standard (non-formal) translation
                    );

                    if (!string.IsNullOrWhiteSpace(primaryTranslatedToEnglishText) && !primaryTranslatedToEnglishText.StartsWith("Error:"))
                    {
                        Log.Debug($"Primary JP->EN translation successful: {primaryTranslatedToEnglishText}");
                        Vector4 textColor = Configuration.ChatColors.TryGetValue(type, out var channelColor) ? channelColor : new Vector4(1, 1, 1, 1);
                        var translatedMessage = new SeStringBuilder()
                            .AddUiForeground($"[EN][{senderText}]: ", (ushort)ColorToUiForegroundId(textColor))
                            .AddUiForeground(primaryTranslatedToEnglishText, (ushort)ColorToUiForegroundId(textColor))
                            .Build();
                        ChatGui.Print(translatedMessage, "ChatTL");
                    }
                    else if (primaryTranslatedToEnglishText != null) // Error from translation service
                    {
                        HandleTranslationError(primaryTranslatedToEnglishText);
                        Log.Warning($"JP->EN Translation service reported an issue: {primaryTranslatedToEnglishText}");
                        primaryTranslatedToEnglishText = null; // Ensure it's null on error
                    }
                }

                // Process additional enabled language translations
                foreach (var languagePair in Configuration.EnabledLanguages)
                {
                    string targetLanguage = languagePair.Key;
                    bool isEnabled = languagePair.Value;

                    if (!isEnabled || targetLanguage == "English") // Skip if not enabled or if it's English (already handled or not a target for this loop)
                    {
                        continue;
                    }

                    // If the original message was Japanese, translate from Japanese to the targetLanguage
                    if (messageIsJapanese)
                    {
                        Log.Debug($"Translating original Japanese message ('{messageText}') to {targetLanguage}");
                        TranslateToLanguage(messageText, senderText, type, targetLanguage, "Japanese");
                    }
                    // If the original message was NOT Japanese (e.g., English), translate from auto-detected source (likely English) to the targetLanguage
                    else 
                    {
                        // This is where we handle non-Japanese source, like English to Indonesian
                        Log.Debug($"Original message ('{messageText}') is not Japanese. Attempting to translate to {targetLanguage} (assuming auto-detect for source).");
                        TranslateToLanguage(messageText, senderText, type, targetLanguage, "auto");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowTranslationError($"Error: {ex.Message}");
                Log.Error(ex, "Exception during asynchronous translation task in OnChatMessage.");
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

    private void HandleTranslationError(string? errorText)
    {
        if (string.IsNullOrWhiteSpace(errorText)) return;

        if (errorText.Contains("API Key"))
        {
            ShowApiKeyError();
        }
        else if (errorText.Contains("Model"))
        {
            ShowModelError(Configuration.OpenRouterModel);
        }
        else
        {
            ShowTranslationError(errorText);
        }
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
    private void TranslateToLanguage(string messageText, string senderText, XivChatType type, string language, string sourceLanguageHint = "auto")
    {
        Task.Run(async () =>
        {
            try
            {
                // Special handling for Indonesian: only translate from English or Japanese
                if (language == "Indonesian")
                {
                    // If we already know it's Japanese, proceed
                    if (sourceLanguageHint != "Japanese")
                    {
                        // Otherwise, check if the message is in English or Japanese
                        bool isEnglishOrJapanese = await IsEnglishOrJapaneseText(messageText);
                        
                        if (!isEnglishOrJapanese)
                        {
                            // Skip translation if not English or Japanese
                            Log.Debug($"Skipping Indonesian translation for message '{messageText}' because it's not detected as English or Japanese.");
                            return;
                        }
                    }
                }
                
                string langCode = GetLanguageCode(language);
                Log.Debug($"TranslateToLanguage called: Source='{sourceLanguageHint}', Target='{language}', Message='{messageText}'");

                string? translatedText = await _translator.TranslateTextAsync(
                    messageText,
                    Configuration.OpenRouterApiKey,
                    Configuration.OpenRouterModel,
                    sourceLanguageHint, // Use the provided source language hint
                    language,
                    false // <--- Ensure automatic translations to other languages use standard (non-formal) translation
                );

                if (!string.IsNullOrWhiteSpace(translatedText) && !translatedText.StartsWith("Error:"))
                {
                    Vector4 languageColor = GetLanguageColor(language);
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
                    // Optionally, display this error to the user for non-primary translations too
                    // HandleTranslationError(translatedText); 
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Exception during {language} translation task.");
            }
        });
    }
    
    // Helper method to detect if text is English or Japanese using LLM
    private async Task<bool> IsEnglishOrJapaneseText(string text)
    {
        if (ContainsJapanese(text))
        {
            return true; // Use the existing method for Japanese detection
        }
        
        try
        {
            // Use the translator to detect the language
            string prompt = "Is the following text in English (including slang, shorthand, or casual English)? Please respond with ONLY 'yes' or 'no'.\n\nText: " + text;
            
            string? response = await _translator.TranslateTextAsync(
                prompt,
                Configuration.OpenRouterApiKey,
                Configuration.OpenRouterModel,
                "auto",
                "English",
                false
            );
            
            if (!string.IsNullOrWhiteSpace(response))
            {
                // Simple check for affirmative response
                string cleanResponse = response.ToLower().Trim();
                return cleanResponse.Contains("yes") || 
                       cleanResponse.Contains("english") || 
                       !cleanResponse.Contains("no");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during language detection");
        }
        
        // Default to allowing translation in case of errors
        return true;
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
            case "ChineseTraditional": return "CNT";
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
            case "ChineseTraditional": return new Vector4(0.8f, 0.3f, 0.3f, 1.0f); // Darker Red
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

    // Helper method to store message in context
    private void StoreMessageInContext(string channel, string sender, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(channel))
            return;
            
        // Create new context message
        var contextMessage = new Configuration.ContextMessage
        {
            Sender = sender,
            Message = message,
            Timestamp = DateTime.Now
        };
        
        // Initialize list if needed
        if (!Configuration.ChannelContexts.TryGetValue(channel, out var messageList))
        {
            messageList = new List<Configuration.ContextMessage>();
            Configuration.ChannelContexts[channel] = messageList;
        }
        
        // Add message to list
        messageList.Add(contextMessage);
        
        // Ensure list doesn't exceed maximum size
        while (messageList.Count > Configuration.MaxContextMessages)
        {
            messageList.RemoveAt(0);
        }
        
        Log.Debug($"Added message to context for channel {channel}: {contextMessage}");
    }
    
    // Helper method to get context for a channel
    private string GetChannelContext(string channel)
    {
        if (!Configuration.EnableContextMemory || string.IsNullOrWhiteSpace(channel))
            return string.Empty;
            
        if (!Configuration.ChannelContexts.TryGetValue(channel, out var messageList) || messageList.Count == 0)
            return string.Empty;
            
        StringBuilder contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Recent conversation context:");
        
        foreach (var msg in messageList)
        {
            contextBuilder.AppendLine($"{msg.Sender}: {msg.Message}");
        }
        
        return contextBuilder.ToString();
    }
    
    // Helper method to clear context for a channel
    public void ClearChannelContext(string channel)
    {
        if (Configuration.ChannelContexts.ContainsKey(channel))
        {
            Configuration.ChannelContexts[channel].Clear();
            Log.Information($"Cleared context for channel {channel}");
        }
    }
    
    // Helper method to clear all context
    public void ClearAllContext()
    {
        Configuration.ChannelContexts.Clear();
        Log.Information("Cleared all channel contexts");
    }
}
