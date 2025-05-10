using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Dalamud.Logging;
using Dalamud.Game.Text;
using System.Collections.Generic;
using System.Linq;

namespace ChatTranslatorAI.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration configuration;
    private Plugin plugin;
    private Vector4 headerColor = new Vector4(0.8f, 0.8f, 0.15f, 1.0f);
    private readonly string[] commonChatTypes = new[] 
    { 
        "Say", "Party", "Tell", "Shout", "Yell", 
        "Free Company", "Novice Network" 
    };
    private readonly XivChatType[] commonChatTypeValues = new[] 
    { 
        XivChatType.Say, XivChatType.Party, XivChatType.TellIncoming, 
        XivChatType.Shout, XivChatType.Yell, XivChatType.FreeCompany, 
        XivChatType.NoviceNetwork 
    };

    // We give this window a hidden ID using ##
    // So that the user will see "Chat Translator Configuration" as window title,
    // but for ImGui the ID is "Chat Translator Configuration##ConfigWindow"
    public ConfigWindow(Plugin plugin)
        : base(
        "Chat Translator Configuration##ConfigWindow",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
        ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        this.configuration = plugin.Configuration;
        
        Size = new Vector2(450, 550);
        SizeCondition = ImGuiCond.Always;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("ConfigTabBar"))
        {
            if (ImGui.BeginTabItem("General Settings"))
            {
                DrawGeneralSettings();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Chat Channels"))
            {
                DrawChatChannelSettings();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("Colors"))
            {
                DrawColorSettings();
                ImGui.EndTabItem();
            }
            
            if (ImGui.BeginTabItem("About"))
            {
                DrawAboutTab();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
        
        ImGui.Separator();
        
        if (ImGui.Button("Save Configuration"))
        {
            configuration.Save();
            Plugin.Log.Info("Translator configuration saved.");
        }

        ImGui.SameLine();
        // Checkbox for movable window
        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
        }
    }
    
    private void DrawGeneralSettings()
    {
        // Global Enable/Disable
        ImGui.TextColored(headerColor, "Translation Settings");
        
        var enableTranslation = configuration.EnableTranslation;
        if (ImGui.Checkbox("Enable Translation", ref enableTranslation))
        {
            configuration.EnableTranslation = enableTranslation;
        }
        
        var useFormalLanguage = configuration.UseFormalLanguage;
        if (ImGui.Checkbox("Use Formal Language", ref useFormalLanguage))
        {
            configuration.UseFormalLanguage = useFormalLanguage;
        }
        
        ImGui.Spacing();
        ImGui.TextColored(headerColor, "Display translations in selected languages");
        ImGui.TextWrapped("Select up to 2 languages to display translations in:");
        
        // Count how many languages are currently enabled
        int enabledCount = configuration.EnabledLanguages.Count(lang => lang.Value);
        bool isEnglishEnabled = configuration.EnabledLanguages["English"];
        
        // Display language selection in 2 columns
        ImGui.Columns(2);
        
        foreach (var language in configuration.EnabledLanguages.Keys.ToList())
        {
            bool isEnabled = configuration.EnabledLanguages[language];
            
            // Other languages can be toggled if we haven't hit the limit
            bool canEnable = isEnabled || enabledCount < 2;
            
            if (!canEnable)
            {
                ImGui.BeginDisabled();
            }
            
            if (ImGui.Checkbox($"{language}", ref isEnabled))
            {
                configuration.EnabledLanguages[language] = isEnabled;
                // Recalculate enabled count
                enabledCount = configuration.EnabledLanguages.Count(lang => lang.Value);
            }
            
            if (!canEnable)
            {
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Maximum 2 languages can be enabled at once.\nDisable another language first.");
                }
            }
            
            // Every other language, move to the next column
            if ((configuration.EnabledLanguages.Keys.ToList().IndexOf(language) % 2) == 0)
            {
                ImGui.NextColumn();
            }
        }
        
        ImGui.Columns(1);
        
        ImGui.Spacing();
        ImGui.Separator();
        
        // API Settings
        ImGui.TextColored(headerColor, "API Settings");
        
        var apiKey = configuration.OpenRouterApiKey ?? string.Empty;
        ImGui.Text("OpenRouter API Key:");
        if (ImGui.InputText("##OpenRouterApiKey", ref apiKey, 256, ImGuiInputTextFlags.Password | ImGuiInputTextFlags.AutoSelectAll))
        {
            configuration.OpenRouterApiKey = apiKey;
        }

        ImGui.Spacing();

        var model = configuration.OpenRouterModel ?? string.Empty;
        ImGui.Text("OpenRouter Model:");
        if (ImGui.InputText("##OpenRouterModel", ref model, 100, ImGuiInputTextFlags.AutoSelectAll))
        {
            configuration.OpenRouterModel = model;
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("e.g., openai/gpt-3.5-turbo, anthropic/claude-2");
        }
    }
    
    private void DrawChatChannelSettings()
    {
        ImGui.TextColored(headerColor, "Select which chat channels to translate:");
        ImGui.Spacing();
        
        // Common chat channels first
        ImGui.TextColored(headerColor, "Common Channels");
        ImGui.Columns(2);
        
        for (int i = 0; i < commonChatTypes.Length; i++)
        {
            XivChatType chatType = commonChatTypeValues[i];
            bool enabled = configuration.EnabledChatTypes.TryGetValue(chatType, out bool isEnabled) ? isEnabled : false;
            
            if (ImGui.Checkbox($"{commonChatTypes[i]}##ChatType{(int)chatType}", ref enabled))
            {
                configuration.EnabledChatTypes[chatType] = enabled;
            }
            
            if (i % 2 == 0)
                ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
        ImGui.Separator();
        
        // LinkShells
        ImGui.TextColored(headerColor, "LinkShells");
        ImGui.Columns(4);
        
        for (int i = 1; i <= 8; i++)
        {
            XivChatType lsType = (XivChatType)Enum.Parse(typeof(XivChatType), $"Ls{i}");
            bool lsEnabled = configuration.EnabledChatTypes.TryGetValue(lsType, out bool isEnabled) ? isEnabled : false;
            
            if (ImGui.Checkbox($"LS {i}##ChatType{(int)lsType}", ref lsEnabled))
            {
                configuration.EnabledChatTypes[lsType] = lsEnabled;
            }
            
            ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
        ImGui.Spacing();
        
        // Cross-world LinkShells
        ImGui.TextColored(headerColor, "Cross-world LinkShells");
        ImGui.Columns(4);
        
        for (int i = 1; i <= 8; i++)
        {
            XivChatType cwlsType = (XivChatType)Enum.Parse(typeof(XivChatType), $"CrossLinkShell{i}");
            bool cwlsEnabled = configuration.EnabledChatTypes.TryGetValue(cwlsType, out bool isEnabled) ? isEnabled : false;
            
            if (ImGui.Checkbox($"CWLS {i}##ChatType{(int)cwlsType}", ref cwlsEnabled))
            {
                configuration.EnabledChatTypes[cwlsType] = cwlsEnabled;
            }
            
            ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
    }
    
    private void DrawColorSettings()
    {
        ImGui.TextColored(headerColor, "Translation Color Settings");
        ImGui.Spacing();
        
        ImGui.TextColored(headerColor, "Command Colors");
        ImGui.Spacing();
        
        // Color for /jp command
        var jpColor = configuration.JpCommandColor;
        if (ImGui.ColorEdit4("JP Command", ref jpColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            configuration.JpCommandColor = jpColor;
        }
        
        // Color for /en command
        var enColor = configuration.EnCommandColor;
        if (ImGui.ColorEdit4("EN Command", ref enColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            configuration.EnCommandColor = enColor;
        }
        
        // Color for /cn command
        var cnColor = configuration.CnCommandColor;
        if (ImGui.ColorEdit4("CN Command", ref cnColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            configuration.CnCommandColor = cnColor;
        }
        
        // Color for /cnt command
        var cntColor = configuration.CntCommandColor;
        if (ImGui.ColorEdit4("CNT Command", ref cntColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            configuration.CntCommandColor = cntColor;
        }
        
        ImGui.Separator();
        ImGui.Spacing();
        
        ImGui.TextColored(headerColor, "Chat Channel Colors");
        ImGui.TextWrapped("Set the color for translations based on the original message's channel:");
        ImGui.Spacing();
        
        // Common channels first
        ImGui.TextColored(headerColor, "Common Channels");
        ImGui.Columns(2);
        
        for (int i = 0; i < commonChatTypes.Length; i++)
        {
            XivChatType chatType = commonChatTypeValues[i];
            
            // Get current color or default white if not found
            Vector4 currentColor = new Vector4(1, 1, 1, 1);
            if (configuration.ChatColors.TryGetValue(chatType, out Vector4 color))
            {
                currentColor = color;
            }
            
            // Color picker
            if (ImGui.ColorEdit4($"##Color{(int)chatType}", ref currentColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            {
                configuration.ChatColors[chatType] = currentColor;
            }
            
            // Channel name next to the color picker
            ImGui.SameLine();
            ImGui.Text(commonChatTypes[i]);
            
            if (i % 2 == 0)
                ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
        ImGui.Separator();
        
        // LinkShells
        ImGui.TextColored(headerColor, "LinkShells");
        ImGui.Columns(4);
        
        for (int i = 1; i <= 8; i++)
        {
            XivChatType lsType = (XivChatType)Enum.Parse(typeof(XivChatType), $"Ls{i}");
            
            // Get current color or default light green if not found
            Vector4 currentColor = new Vector4(0.5f, 0.8f, 0.5f, 1.0f);
            if (configuration.ChatColors.TryGetValue(lsType, out Vector4 color))
            {
                currentColor = color;
            }
            
            // Color picker
            if (ImGui.ColorEdit4($"##ColorLS{i}", ref currentColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            {
                configuration.ChatColors[lsType] = currentColor;
            }
            
            // Channel name next to the color picker
            ImGui.SameLine();
            ImGui.Text($"LS {i}");
            
            ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
        ImGui.Spacing();
        
        // Cross-world LinkShells
        ImGui.TextColored(headerColor, "Cross-world LinkShells");
        ImGui.Columns(4);
        
        for (int i = 1; i <= 8; i++)
        {
            XivChatType cwlsType = (XivChatType)Enum.Parse(typeof(XivChatType), $"CrossLinkShell{i}");
            
            // Get current color or default light cyan if not found
            Vector4 currentColor = new Vector4(0.5f, 0.8f, 0.8f, 1.0f);
            if (configuration.ChatColors.TryGetValue(cwlsType, out Vector4 color))
            {
                currentColor = color;
            }
            
            // Color picker
            if (ImGui.ColorEdit4($"##ColorCWLS{i}", ref currentColor, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            {
                configuration.ChatColors[cwlsType] = currentColor;
            }
            
            // Channel name next to the color picker
            ImGui.SameLine();
            ImGui.Text($"CWLS {i}");
            
            ImGui.NextColumn();
        }
        
        ImGui.Columns(1);
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextWrapped("Note: Color settings are applied to the translated text only, not to the original messages.");
    }
    
    private void DrawAboutTab()
    {
        ImGui.TextColored(headerColor, "Chat Translator AI");
        ImGui.Spacing();
        ImGui.Text("Version: 1.0.0");
        ImGui.Text("Author: Vibicer");
        ImGui.Spacing();
        ImGui.TextWrapped("This plugin automatically translates Japanese chat messages to English in real-time using the OpenRouter API.");
        ImGui.Spacing();
        ImGui.TextWrapped("To use this plugin, you need an OpenRouter API key. You can get one by signing up at https://openrouter.ai/");
        ImGui.Spacing();
        ImGui.TextWrapped("The plugin works by intercepting chat messages, detecting Japanese text, and translating it using the selected AI model.");
        ImGui.Spacing();
        ImGui.TextColored(headerColor, "Features:");
        ImGui.TextWrapped("• Automatic JP → EN translation for chat messages");
        ImGui.TextWrapped("• /jp command to translate your messages into Japanese");
        ImGui.TextWrapped("• /en command to translate any language into English");
        ImGui.TextWrapped("• /cn command to translate any language into Chinese (Simplified) with pinyin");
        ImGui.TextWrapped("• /cnt command to translate any language into Chinese (Traditional) with pinyin");
        ImGui.TextWrapped("• Multi-language translation support (up to 2 languages)");
        ImGui.TextWrapped("• Formal/casual language toggle for translations");
        ImGui.TextWrapped("• Color customization for each chat channel's translations");
        ImGui.TextWrapped("• Selective translation by chat channel type");
    }
}
