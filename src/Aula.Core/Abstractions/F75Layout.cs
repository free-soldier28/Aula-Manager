using System.Globalization;

namespace Aula.Core.Abstractions;

public sealed class F75Layout : IKeyboardLayout
{
    public const int LedCount = 126;

    public static readonly F75Layout Instance = new();

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

    private F75Layout()
    {
    }

    public IReadOnlyList<string> Keys => IndexByName.Keys.ToList();

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
