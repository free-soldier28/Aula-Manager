namespace Aula.Core.Abstractions;

public interface IKeyboardLayout
{
    IReadOnlyList<string> Keys { get; }

    int GetLedIndex(string keyName);

    bool TryGetLedIndex(string keyName, out int index);
}
