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
                "Japanese"
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
                
                // Display the translated text with romaji if available
                var messageBuilder = new SeStringBuilder()
                    .AddText($"➤ {displayText}");
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

        if (messageText.Contains("[ChatTL]") || messageText.Contains("[TR]:"))
        {
            return;
        }

        if (IsSelfMessage(senderText) && !Configuration.TranslateOwnMessages)
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
                    "English"
                );

                if (!string.IsNullOrWhiteSpace(translatedText) && !translatedText.StartsWith("Error:"))
                {
                    var translatedMessage = new SeStringBuilder()
                        .AddText($"[{senderText}]: ")
                        .AddText(translatedText)
                        .Build();

                    ChatGui.Print(translatedMessage, "ChatTL");
                    Log.Debug($"Translation successful: {translatedText}");
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
        if (ClientState.LocalPlayer == null)
        {
            return false;
        }
        return senderName == ClientState.LocalPlayer.Name.TextValue;
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
}
