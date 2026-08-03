using System.Globalization;

namespace Aula.Core.Abstractions;

public sealed class F75Layout : IKeyboardLayout
{
    public static readonly F75Layout Instance = new();

    public int LedCount => 126;

    private static readonly IReadOnlyDictionary<string, int> IndexByName =
        new Dictionary<string, int>
        {
            ["esc"] = 0, ["f1"] = 12, ["f2"] = 18, ["f3"] = 24, ["f4"] = 30,
            ["f5"] = 36, ["f6"] = 42, ["f7"] = 48, ["f8"] = 54, ["f9"] = 60,
            ["f10"] = 66, ["f11"] = 72, ["f12"] = 78, ["prtsc"] = 84,
            ["scrlk"] = 90, ["pause"] = 96,
            ["`"] = 1, ["1"] = 7, ["2"] = 13, ["3"] = 19, ["4"] = 25, ["5"] = 31,
            ["6"] = 37, ["7"] = 43, ["8"] = 49, ["9"] = 55, ["0"] = 61, ["-"] = 67,
            ["="] = 73, ["backspace"] = 79, ["ins"] = 85, ["home"] = 91, ["pgup"] = 97,
            ["tab"] = 2, ["q"] = 8, ["w"] = 14, ["e"] = 20, ["r"] = 26, ["t"] = 32,
            ["y"] = 38, ["u"] = 44, ["i"] = 50, ["o"] = 56, ["p"] = 62, ["["] = 68,
            ["]"] = 74, ["\\"] = 80, ["del"] = 86, ["end"] = 92, ["pgdn"] = 98,
            ["caps"] = 3, ["a"] = 9, ["s"] = 15, ["d"] = 21, ["f"] = 27,
            ["g"] = 33, ["h"] = 39, ["j"] = 45, ["k"] = 51, ["l"] = 57, [";"] = 63,
            ["'"] = 69, ["enter"] = 81,
            ["lshift"] = 4, ["z"] = 10, ["x"] = 16, ["c"] = 22, ["v"] = 28,
            ["b"] = 34, ["n"] = 40, ["m"] = 46, [","] = 52, ["."] = 58, ["/"] = 64,
            ["rshift"] = 82, ["up"] = 94,
            ["lctrl"] = 5, ["lwin"] = 11, ["lalt"] = 17, ["space"] = 35,
            ["ralt"] = 53, ["fn"] = 59, ["app"] = 65, ["rctrl"] = 83,
            ["left"] = 89, ["down"] = 95, ["right"] = 101, ["iso"] = 76,
        };

    public static readonly IReadOnlyDictionary<int, string> NameByIndex =
        IndexByName.ToDictionary(kv => kv.Value, kv => kv.Key);

    // Visual rows shown in the per-key editor. Gap is the space in pixels left
    // of a key. The function row is grouped in blocks of four (F1-F4, F5-F8,
    // F9-F12) with small gaps between them, F1 sits above "2" and F12 ends where
    // the Backspace key ends.
    private static readonly KeyShape[][] VisualRows =
    {
        new[]
        {
            new KeyShape("esc", "Esc", 1.5, 0), new KeyShape("f1", "F1", 1, 20), new KeyShape("f2", "F2", 1, 3), new KeyShape("f3", "F3", 1, 3), new KeyShape("f4", "F4", 1, 3),
            new KeyShape("f5", "F5", 1, 17), new KeyShape("f6", "F6", 1, 3), new KeyShape("f7", "F7", 1, 3), new KeyShape("f8", "F8", 1, 3),
            new KeyShape("f9", "F9", 1, 17), new KeyShape("f10", "F10", 1, 3), new KeyShape("f11", "F11", 1, 3), new KeyShape("f12", "F12", 1, 3),
        },
        new[]
        {
            new KeyShape("`", "`", 1, 0), new KeyShape("1", "1", 1, 3), new KeyShape("2", "2", 1, 3), new KeyShape("3", "3", 1, 3), new KeyShape("4", "4", 1, 3), new KeyShape("5", "5", 1, 3),
            new KeyShape("6", "6", 1, 3), new KeyShape("7", "7", 1, 3), new KeyShape("8", "8", 1, 3), new KeyShape("9", "9", 1, 3), new KeyShape("0", "0", 1, 3), new KeyShape("-", "-", 1, 3),
            new KeyShape("=", "=", 1, 3), new KeyShape("backspace", "Bksp", 2.0, 3), new KeyShape("del", "Del", 1, 3),
        },
        new[]
        {
            new KeyShape("tab", "Tab", 1.5, 0), new KeyShape("q", "Q", 1, 3), new KeyShape("w", "W", 1, 3), new KeyShape("e", "E", 1, 3), new KeyShape("r", "R", 1, 3),
            new KeyShape("t", "T", 1, 3), new KeyShape("y", "Y", 1, 3), new KeyShape("u", "U", 1, 3), new KeyShape("i", "I", 1, 3), new KeyShape("o", "O", 1, 3), new KeyShape("p", "P", 1, 3),
            new KeyShape("[", "[", 1, 3), new KeyShape("]", "]", 1, 3), new KeyShape("\\", "\\", 1.5, 3), new KeyShape("pgup", "PgUp", 1, 3),
        },
        new[]
        {
            new KeyShape("caps", "Caps", 1.75, 0), new KeyShape("a", "A", 1, 3), new KeyShape("s", "S", 1, 3), new KeyShape("d", "D", 1, 3), new KeyShape("f", "F", 1, 3),
            new KeyShape("g", "G", 1, 3), new KeyShape("h", "H", 1, 3), new KeyShape("j", "J", 1, 3), new KeyShape("k", "K", 1, 3), new KeyShape("l", "L", 1, 3), new KeyShape(";", ";", 1, 3),
            new KeyShape("'", "'", 1, 3), new KeyShape("enter", "Enter", 2.3571, 3), new KeyShape("pgdn", "PgDn", 1, 3),
        },
        new[]
        {
            new KeyShape("lshift", "Shift", 2.25, 0), new KeyShape("z", "Z", 1, 3), new KeyShape("x", "X", 1, 3), new KeyShape("c", "C", 1, 3), new KeyShape("v", "V", 1, 3),
            new KeyShape("b", "B", 1, 3), new KeyShape("n", "N", 1, 3), new KeyShape("m", "M", 1, 3), new KeyShape(",", ",", 1, 3), new KeyShape(".", ".", 1, 3), new KeyShape("/", "/", 1, 3),
            new KeyShape("rshift", "Shift", 1.75, 3), new KeyShape("up", "↑", 1, 6), new KeyShape("end", "End", 1, 3),
        },
        new[]
        {
            new KeyShape("lctrl", "Ctrl", 1.25, 0), new KeyShape("lwin", "Win", 1.25, 3), new KeyShape("lalt", "Alt", 1.25, 3), new KeyShape("space", "Space", 7.2857, 3),
            new KeyShape("fn", "Fn", 1.25, 3), new KeyShape("rctrl", "Ctrl", 1.25, 3),
            new KeyShape("left", "←", 1, 6), new KeyShape("down", "↓", 1, 3), new KeyShape("right", "→", 1, 3),
        },
    };

    private F75Layout()
    {
    }

    public IReadOnlyList<string> Keys => IndexByName.Keys.ToList();

    public IReadOnlyList<IReadOnlyList<KeyShape>> Rows => VisualRows;

    public int GetLedIndex(string keyName)
    {
        string normalized = keyName.Trim().ToLowerInvariant();
        return IndexByName.TryGetValue(normalized, out int index) ? index : -1;
    }

    public bool TryGetLedIndex(string keyName, out int index)
    {
        index = GetLedIndex(keyName);
        return index >= 0;
    }

    public static string GetKeyName(int ledIndex) =>
        NameByIndex.TryGetValue(ledIndex, out string? name) ? name : $"led{ledIndex}";
}
