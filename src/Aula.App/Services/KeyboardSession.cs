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

    public string? Error { get; private set; }

    public void Open(string? modelId = null)
    {
        Error = null;
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
            if (_keyboard is null)
            {
                Error = "TryOpen returned null (no device found).";
            }
        }
        catch (Exception ex)
        {
            _keyboard = null;
            Error = ex.GetType().Name + ": " + ex.Message;
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
