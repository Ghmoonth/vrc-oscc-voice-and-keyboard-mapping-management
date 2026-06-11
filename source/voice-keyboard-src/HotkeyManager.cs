using System.Runtime.InteropServices;

namespace OSCC;

public sealed class HotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly Form owner;
    private readonly Action<string> onHotkey;
    private readonly Action<string> log;
    private readonly Dictionary<int, string> idToMapping = new();
    private int nextId = 1;

    public HotkeyManager(Form owner, Action<string> onHotkey, Action<string> log)
    {
        this.owner = owner;
        this.onHotkey = onHotkey;
        this.log = log;
    }

    public void RegisterAll(IEnumerable<KeyboardMapping> mappings)
    {
        UnregisterAll();
        foreach (var mapping in mappings.Where(m => m.Enabled))
        {
            if (!TryParse(mapping.Hotkey, out var modifiers, out var key))
            {
                log("璺宠繃鏃犳硶璇嗗埆鐨勭儹閿? " + mapping.Hotkey);
                continue;
            }

            var id = nextId++;
            if (RegisterHotKey(owner.Handle, id, modifiers | ModNoRepeat, key))
            {
                idToMapping[id] = mapping.Id;
            }
            else
            {
                log("娉ㄥ唽澶辫触锛屽彲鑳界儹閿鍗犵敤: " + mapping.Hotkey);
            }
        }
        log("閿洏妯″潡宸插惎鍔紝娉ㄥ唽 " + idToMapping.Count + " 涓寜閿€?);
    }

    public bool HandleMessage(ref Message message)
    {
        if (message.Msg != WmHotkey)
        {
            return false;
        }

        if (idToMapping.TryGetValue(message.WParam.ToInt32(), out var id))
        {
            onHotkey(id);
            return true;
        }
        return false;
    }

    public void UnregisterAll()
    {
        foreach (var id in idToMapping.Keys.ToArray())
        {
            UnregisterHotKey(owner.Handle, id);
        }
        idToMapping.Clear();
        nextId = 1;
    }

    public void Dispose() => UnregisterAll();

    public static bool TryParse(string text, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        foreach (var raw in text.Replace("-", "+").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = raw.Equals("Control", StringComparison.OrdinalIgnoreCase) ? "Ctrl" : raw;
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModControl;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModAlt;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModShift;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= ModWin;
            else if (TryKey(part, out var parsed)) key = parsed;
        }
        return key != 0;
    }

    private static bool TryKey(string part, out uint key)
    {
        key = 0;
        if (part.Length == 1)
        {
            var ch = char.ToUpperInvariant(part[0]);
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
            {
                key = ch;
                return true;
            }
        }
        if (part.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(part[1..], out var f)
            && f is >= 1 and <= 24)
        {
            key = (uint)(0x70 + f - 1);
            return true;
        }

        var named = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["BackSpace"] = 0x08,
            ["Tab"] = 0x09,
            ["Enter"] = 0x0D,
            ["Return"] = 0x0D,
            ["Esc"] = 0x1B,
            ["Escape"] = 0x1B,
            ["Space"] = 0x20,
            ["PageUp"] = 0x21,
            ["PageDown"] = 0x22,
            ["End"] = 0x23,
            ["Home"] = 0x24,
            ["Left"] = 0x25,
            ["Up"] = 0x26,
            ["Right"] = 0x27,
            ["Down"] = 0x28,
            ["Insert"] = 0x2D,
            ["Delete"] = 0x2E,
            ["NumLock"] = 0x90
        };
        return named.TryGetValue(part, out key);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
