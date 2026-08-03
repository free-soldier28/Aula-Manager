namespace Aula.Core.Abstractions;

public interface IKeyboardLayout
{
    int LedCount { get; }

    IReadOnlyList<string> Keys { get; }

    IReadOnlyList<IReadOnlyList<KeyShape>> Rows { get; }

    int GetLedIndex(string keyName);

    bool TryGetLedIndex(string keyName, out int index);
}
