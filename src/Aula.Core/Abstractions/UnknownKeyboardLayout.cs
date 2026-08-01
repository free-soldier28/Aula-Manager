namespace Aula.Core.Abstractions;

public sealed class UnknownKeyboardLayout : IKeyboardLayout
{
    public static readonly UnknownKeyboardLayout Instance = new();

    public IReadOnlyList<string> Keys => Array.Empty<string>();

    public int GetLedIndex(string keyName) => -1;

    public bool TryGetLedIndex(string keyName, out int index)
    {
        index = -1;
        return false;
    }
}
