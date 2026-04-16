namespace Clipster.Services.Tips;

public static class TipRepository
{
    private static readonly string[] Tips =
    [
        "Did you know? Win+V opens your clipboard history — you can paste things you copied earlier!",
        "Pro tip: Win+Shift+S lets you take a screenshot of any area on your screen instantly!",
        "It looks like you're using a computer! Try Win+D to quickly show your desktop.",
        "Did you know? Ctrl+Shift+T reopens the last tab you closed in most browsers!",
        "Pro tip: Win+L locks your computer instantly. Great when you step away!",
        "Hey! You can rename a file by selecting it and pressing F2. No right-clicking needed!",
        "Did you know? Ctrl+Backspace deletes a whole word at a time. Much faster than holding Backspace!",
        "Pro tip: Alt+Tab switches between windows, but Win+Tab gives you a full overview with virtual desktops!",
        "It looks like you've been working hard! Remember to take a break. Your eyes will thank you!",
        "Did you know? You can drag a window to the edge of the screen to snap it to half the display!",
        "Pro tip: Ctrl+Shift+Esc opens Task Manager directly — no need for Ctrl+Alt+Delete first!",
        "Hey! Win+. (period) opens the emoji picker. Express yourself! 😊",
        "Did you know? Holding Shift while right-clicking gives you extra options like 'Copy as path'!",
        "Pro tip: Ctrl+Z works almost everywhere — not just in text editors. Try it in File Explorer to undo a move!",
        "It looks like you might be typing a lot! Ctrl+A selects everything in one go.",
        "Did you know? Win+Arrow keys snap and resize windows without dragging!",
        "Pro tip: Middle-clicking a link opens it in a new tab. Middle-clicking a tab closes it!",
        "Hey! You can press Ctrl+F in almost any app to search for text on the page.",
        "Did you know? Win+E opens File Explorer instantly from anywhere!",
        "Pro tip: If a program freezes, don't force-restart your PC — Ctrl+Shift+Esc → End Task is much safer!",
        "It looks like you're being productive! Win+I opens Windows Settings if you need to tweak anything.",
        "Did you know? You can hold Ctrl and scroll the mouse wheel to zoom in and out in most apps!",
        "Pro tip: Press F11 to go fullscreen in your browser. Press it again to come back!",
        "Hey! Alt+F4 closes the current window. But please don't close me! 📎",
        "Did you know? You can pin your favorite apps to the taskbar by right-clicking them!",
        "Pro tip: Ctrl+D bookmarks the current page in your browser instantly!",
        "It looks like you could use a stretch! Stand up, reach for the ceiling, and twist side to side.",
        "Did you know? Win+P lets you quickly switch between display modes for presentations!",
        "Pro tip: Ctrl+Home jumps to the beginning of a document. Ctrl+End goes to the end!",
        "Hey! Double-clicking a word selects it. Triple-clicking selects the whole paragraph!",
        "Did you know? You can create virtual desktops with Win+Ctrl+D to organize your work!",
        "Pro tip: Win+Number opens or switches to the app pinned at that position on your taskbar!",
        "It looks like you might enjoy this: Ctrl+Shift+N opens an incognito/private window in most browsers!",
        "Did you know? You can drag files directly into a Save/Open dialog to navigate to that folder!",
        "Pro tip: Press and hold the Windows key to see all available keyboard shortcuts displayed on screen!",
    ];

    private static readonly Random Rng = new();

    public static string GetRandomTip()
    {
        return Tips[Rng.Next(Tips.Length)];
    }
}
