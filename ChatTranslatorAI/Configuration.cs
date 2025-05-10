using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using Dalamud.Plugin.Services;  // Added for IDalamudPluginInterface
using System.Collections.Generic;
using Dalamud.Game.Text;

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
    public bool TranslateOwnMessages { get; set; } = false; // Whether to translate messages from self
    
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
