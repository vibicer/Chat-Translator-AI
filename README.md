# Chat Translator AI

**Translate FFXIV chat in real-time!** This plugin uses AI to translate Japanese messages into English and can translate your messages into Japanese.

It connects to [OpenRouter.ai](https://openrouter.ai/) to provide the translations.

## Key Features

*   **Live Translation**: See Japanese chat automatically translated to English.
*   **Translate Your Messages**: Use the `/jp` command to translate your English (or other language) messages into Japanese before sending.
*   **Quick Japanese to English**: Use the `/en` command to translate Japanese text into English on demand.
*   **Formal/Casual Toggle**: Choose between formal or casual language style for all translations.
*   **Color Customization**: Set different colors for translations based on the original chat channel.
*   **Easy Configuration**: Simple in-game menu to set up your API key and preferences.
*   **Flexible Model Choice**: Works with various AI models available on OpenRouter.


## First-Time Setup

1.  **Get an OpenRouter API Key**:
    *   Go to [OpenRouter.ai](https://openrouter.ai/) and create an account.
    *   Find your API key on your account page.
2.  **Configure the Plugin**:
    *   In FFXIV, type `/transconfig` in chat to open the settings window.
    *   Paste your OpenRouter API key into the "OpenRouter API Key" field.
    *   Choose a translation model (e.g., `google/gemini-2.0-flash-lite-001` is a good start very cheap and fast).
    *   Click "Save and Close".

## How to Use

**Automatic Translation (Japanese to English):**

*   Once set up, the plugin will automatically translate incoming Japanese messages in your chatbox.
*   Translated messages will appear with a `[ChatTL]` prefix.
*   Translations include romaji (Japanese written in Latin alphabet) in parentheses to help with pronunciation.

**Translating Your Messages (Any Language to Japanese):**

*   Type `/jp <your message>`.
    *   Example: `/jp Hello, how are you?`
*   The plugin will show you the Japanese translation along with romaji pronunciation in parentheses.
*   Only the Japanese text (without romaji) is automatically copied to your clipboard, so you can easily paste it into the chat input.

**Accessing Settings:**

*   Type `/transconfig` at any time to open the settings window. Here you can:
    *   Change your API key or model.
    *   Enable/disable translations for specific chat channels (Say, Party, Tell, etc.).
    *   Toggle if your own messages are translated by default (for the automatic JP->EN).

## Troubleshooting

*   **"API Key Needed" Error**: Make sure you've entered your OpenRouter API key correctly in `/transconfig`.
*   **No Translations**:
    *   Check that "Enable Translation" is on in `/transconfig`.
    *   Ensure the correct chat channels are enabled.
    *   Verify your API key and selected model are valid on OpenRouter.
*   **See Logs**: For detailed errors, type `/xllog` in chat and look for messages from "ChatTranslatorAI".

## Credits

*   Uses [OpenRouter.ai](https://openrouter.ai/) for AI translation.
*   Built with the Dalamud plugin framework.

## License

MIT License (see `LICENSE` file for details).
