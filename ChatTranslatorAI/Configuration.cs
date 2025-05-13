using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using Dalamud.Plugin.Services;  // Added for IDalamudPluginInterface
using System.Collections.Generic;
using Dalamud.Game.Text;
using System.Numerics; // For Vector4 (colors)
using System.Collections.Generic; // Added for Queue

namespace ChatTranslatorAI;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    // public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true; // Removing this example property

    // Add your configuration options here
    public string OpenRouterApiKey { get; set; } = string.Empty;
    public string OpenRouterModel { get; set; } = "openai/gpt-3.5-turbo"; // Default model

    // Phase 2 - Refinements & Enhancements
    public bool EnableTranslation { get; set; } = true; // Global enable/disable
    public bool UseFormalLanguage { get; set; } = false; // Default to casual. True for formal, false for casual.
    
    // Language selection for translations (maximum 2 can be enabled)
    public Dictionary<string, bool> EnabledLanguages { get; set; } = new Dictionary<string, bool>
    {
        { "English", true },  // Default is enabled, but can be disabled
        { "Indonesian", false },
        { "Spanish", false },
        { "Japanese", false },
        { "Korean", false },
        { "Chinese", false },
        { "ChineseTraditional", false },
        { "French", false },
        { "German", false },
        { "Russian", false },
        { "Portuguese", false },
        { "Italian", false },
        { "Arabic", false },
        { "Hindi", false },
        { "Turkish", false },
        { "Vietnamese", false },
        { "Thai", false }
    };
    
    // Color settings
    public Dictionary<XivChatType, Vector4> ChatColors { get; set; } = new Dictionary<XivChatType, Vector4>
    {
        { XivChatType.Say, new Vector4(0.70f, 0.70f, 0.70f, 1.0f) },         // Light gray
        { XivChatType.Party, new Vector4(0.20f, 0.65f, 0.90f, 1.0f) },        // Blue
        { XivChatType.TellIncoming, new Vector4(0.90f, 0.60f, 0.80f, 1.0f) }, // Pink
        { XivChatType.Shout, new Vector4(1.00f, 0.65f, 0.20f, 1.0f) },        // Orange
        { XivChatType.Yell, new Vector4(0.95f, 0.80f, 0.20f, 1.0f) },         // Yellow
        { XivChatType.FreeCompany, new Vector4(0.60f, 0.90f, 0.60f, 1.0f) },  // Green
        { XivChatType.NoviceNetwork, new Vector4(0.80f, 0.80f, 0.50f, 1.0f) },// Tan
        // Default colors for LinkShells (light green variants)
        { XivChatType.Ls1, new Vector4(0.50f, 0.80f, 0.50f, 1.0f) },
        { XivChatType.Ls2, new Vector4(0.52f, 0.82f, 0.52f, 1.0f) },
        { XivChatType.Ls3, new Vector4(0.54f, 0.84f, 0.54f, 1.0f) },
        { XivChatType.Ls4, new Vector4(0.56f, 0.86f, 0.56f, 1.0f) },
        { XivChatType.Ls5, new Vector4(0.58f, 0.88f, 0.58f, 1.0f) },
        { XivChatType.Ls6, new Vector4(0.60f, 0.90f, 0.60f, 1.0f) },
        { XivChatType.Ls7, new Vector4(0.62f, 0.92f, 0.62f, 1.0f) },
        { XivChatType.Ls8, new Vector4(0.64f, 0.94f, 0.64f, 1.0f) },
        // Default colors for CrossLinkShells (light cyan variants)
        { XivChatType.CrossLinkShell1, new Vector4(0.50f, 0.80f, 0.80f, 1.0f) },
        { XivChatType.CrossLinkShell2, new Vector4(0.52f, 0.82f, 0.82f, 1.0f) },
        { XivChatType.CrossLinkShell3, new Vector4(0.54f, 0.84f, 0.84f, 1.0f) },
        { XivChatType.CrossLinkShell4, new Vector4(0.56f, 0.86f, 0.86f, 1.0f) },
        { XivChatType.CrossLinkShell5, new Vector4(0.58f, 0.88f, 0.88f, 1.0f) },
        { XivChatType.CrossLinkShell6, new Vector4(0.60f, 0.90f, 0.90f, 1.0f) },
        { XivChatType.CrossLinkShell7, new Vector4(0.62f, 0.92f, 0.92f, 1.0f) },
        { XivChatType.CrossLinkShell8, new Vector4(0.64f, 0.94f, 0.94f, 1.0f) },
    };
    
    // Special color settings for manual translation commands
    public Vector4 JpCommandColor { get; set; } = new Vector4(0.9f, 0.4f, 0.7f, 1.0f); // Pink
    public Vector4 EnCommandColor { get; set; } = new Vector4(0.3f, 0.6f, 0.9f, 1.0f); // Blue
    public Vector4 CnCommandColor { get; set; } = new Vector4(0.8f, 0.2f, 0.2f, 1.0f); // Red
    public Vector4 CntCommandColor { get; set; } = new Vector4(0.8f, 0.3f, 0.3f, 1.0f); // Darker Red
    
    // Chat channel filtering
    public Dictionary<XivChatType, bool> EnabledChatTypes { get; set; } = new Dictionary<XivChatType, bool>
    {
        { XivChatType.Say, true },
        { XivChatType.Party, true },
        { XivChatType.TellIncoming, true },
        { XivChatType.Shout, true },
        { XivChatType.Yell, true },
        { XivChatType.FreeCompany, true },
        { XivChatType.CrossLinkShell1, true },
        { XivChatType.CrossLinkShell2, true },
        { XivChatType.CrossLinkShell3, true },
        { XivChatType.CrossLinkShell4, true },
        { XivChatType.CrossLinkShell5, true },
        { XivChatType.CrossLinkShell6, true },
        { XivChatType.CrossLinkShell7, true },
        { XivChatType.CrossLinkShell8, true },
        { XivChatType.Ls1, true },
        { XivChatType.Ls2, true },
        { XivChatType.Ls3, true },
        { XivChatType.Ls4, true },
        { XivChatType.Ls5, true },
        { XivChatType.Ls6, true },
        { XivChatType.Ls7, true },
        { XivChatType.Ls8, true },
        { XivChatType.NoviceNetwork, true },
    };

    // Context Memory
    public bool EnableContextMemory { get; set; } = false;
    public int MaxContextMessages { get; set; } = 3;
    
    // Saved per channel context
    [NonSerialized]
    public Dictionary<string, List<ContextMessage>> ChannelContexts = new Dictionary<string, List<ContextMessage>>();
    
    // Context class for storing message information
    [Serializable]
    public class ContextMessage
    {
        public string Sender { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Sender}: {Message}";
        }
    }

    // Using IDalamudPluginInterface instead of DalamudPluginInterface
    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pInterface)
    {
        this.pluginInterface = pInterface;
    }

    public void Save()
    {
        if (this.pluginInterface != null)
        {
            this.pluginInterface.SavePluginConfig(this);
        }
        else
        {
            // Log or handle the case where pluginInterface is not initialized
            Plugin.Log.Error("Plugin interface was not initialized before attempting to save configuration.");
        }
    }
}
