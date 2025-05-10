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
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
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
        
        var translateOwnMessages = configuration.TranslateOwnMessages;
        if (ImGui.Checkbox("Translate My Messages", ref translateOwnMessages))
        {
            configuration.TranslateOwnMessages = translateOwnMessages;
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
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
    }
}
