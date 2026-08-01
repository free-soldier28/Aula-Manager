using Aula.Core;
using Aula.Core.Abstractions;
using Aula.Core.Models;
using Aula.Core.Services;

namespace Aula.App.Services;

public sealed class KeyboardSession : IDisposable
{
    private readonly KeyboardDeviceFactory _factory = new();
    private IAulaKeyboard? _keyboard;

    public event Action? Changed;

    public IAulaKeyboard? Current => _keyboard;

    public bool IsConnected => _keyboard is not null;

    public void Open(string? modelId = null)
    {
        if (_keyboard is not null)
        {
            if (_keyboard.Model.Id == (modelId ?? _keyboard.Model.Id))
            {
                return;
            }

            _keyboard.Dispose();
            _keyboard = null;
        }

        try
        {
            _keyboard = _factory.TryOpen(modelId);
        }
        catch (AulaException)
        {
            _keyboard = null;
        }

        Changed?.Invoke();
    }

    public void Refresh(string? modelId = null)
    {
        _keyboard?.Dispose();
        _keyboard = null;
        Open(modelId);
    }

    public void Dispose()
    {
        _keyboard?.Dispose();
        _keyboard = null;
    }
}
