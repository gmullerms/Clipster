# Clipster

A modern AI-powered desktop assistant for Windows. Clipster is a friendly paperclip character that floats on your desktop, offering smart assistance powered by OpenAI.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple) ![WPF](https://img.shields.io/badge/WPF-Windows-blue) ![OpenAI](https://img.shields.io/badge/OpenAI-GPT--4o-green)

## Features

### Chat Assistant
Click Clipster to open a full chat window. Ask questions, get help with code, brainstorm ideas, or just chat. Powered by GPT-4o with a fun, helpful personality.

### Quick Prompt (Ctrl+Shift+Space)
Press **Ctrl+Shift+Space** from anywhere to open a quick prompt bar. Type your request and hit Enter:
- **Commands**: "git command to squash last 3 commits" -- copies `git rebase -i HEAD~3` to clipboard
- **Translations**: "translate 'hello' to Japanese" -- copies the translation
- **Code**: "regex for email validation" -- copies the regex
- **Questions**: "explain async/await" -- shows answer in Clipster's speech bubble

The AI is smart about what goes to your clipboard -- clean, paste-ready content with no extra formatting.

### Clipboard Awareness
Copy text and Clipster reacts with contextual suggestions:
- **Code** -- "Explain this?" / "Improve this?"
- **JSON** -- "Analyze?" / "Format?"
- **URLs** -- "What is this?"
- **Plain text** -- "Summarize?" / "Rewrite?"

### Screen Reading
Click "What's on my screen?" in the chat window. Clipster captures your screen and uses GPT-4o vision to analyze what you're working on and offer help.

### Proactive Tips
Clipster occasionally pops up with helpful tips -- keyboard shortcuts, productivity advice, and context-aware suggestions. Snooze him if you need focus time.

### Animated Character
Two character styles, switchable in settings:
- **Modern** -- smooth vector animations with arms, expressions, and sparkle effects
- **Classic** -- retro-style with blockier movements

Animations react to what's happening: idle floating, greeting waves, thinking poses, talking bounces, confused wobbles, and celebration spins.

## Getting Started

### Prerequisites
- Windows 10 or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An [OpenAI API key](https://platform.openai.com/api-keys)

### Build & Run
```bash
git clone https://github.com/gmullerms/Clipster.git
cd Clipster
dotnet build
dotnet run --project src/Clipster.App
```

### Setup
1. Clipster appears in the bottom-right corner of your screen
2. Right-click the **system tray icon** and select **Settings**
3. Enter your **OpenAI API key** and click Save
4. Click Clipster to chat, or press **Ctrl+Shift+Space** for quick prompts

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| **Ctrl+Shift+Space** | Quick prompt (response copied to clipboard) |
| **Click Clipster** | Open chat window |
| **Drag Clipster** | Move him anywhere on screen |

## Architecture

```
Clipster.sln
  src/
    Clipster.Core          -- Interfaces, models, events (no dependencies)
    Clipster.Services      -- OpenAI, clipboard monitor, OCR, tips, settings
    Clipster.ViewModels    -- MVVM ViewModels (CommunityToolkit.Mvvm)
    Clipster.App           -- WPF views, DI bootstrap, system tray
```

- **MVVM** with dependency injection via `Microsoft.Extensions.Hosting`
- **OpenAI SDK** (v2.x) for chat, vision, and tip generation
- **Win32 interop** for clipboard monitoring (`AddClipboardFormatListener`) and global hotkeys (`RegisterHotKey`)
- **GDI+** screen capture for the screen reading feature

## Configuration

Settings are stored at `%APPDATA%/Clipster/settings.json`:

| Setting | Default | Description |
|---|---|---|
| ApiKey | *(empty)* | Your OpenAI API key |
| ModelName | `gpt-4o` | OpenAI model to use |
| CharacterStyle | Classic | `Classic` or `Modern` |
| EnableClipboardMonitoring | true | React to clipboard changes |
| EnableProactiveTips | true | Show periodic tips |
| EnableOcr | true | Enable screen reading |
| TipIntervalMinMinutes | 20 | Minimum minutes between tips |
| TipIntervalMaxMinutes | 40 | Maximum minutes between tips |

## License

MIT

## Acknowledgments

Built with [Claude Code](https://claude.ai/claude-code) by Anthropic.
