namespace Aula.Core.Abstractions;

public sealed class UnknownKeyboardLayout : IKeyboardLayout
{
    public static readonly UnknownKeyboardLayout Instance = new();

    public int LedCount => 0;

    public IReadOnlyList<string> Keys => Array.Empty<string>();

    public IReadOnlyList<IReadOnlyList<KeyShape>> Rows => Array.Empty<IReadOnlyList<KeyShape>>();

    public int GetLedIndex(string keyName) => -1;

    public bool TryGetLedIndex(string keyName, out int index)
    {
        index = -1;
        return false;
    }
}
