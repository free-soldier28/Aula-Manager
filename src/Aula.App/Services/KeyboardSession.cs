using Aula.Core;
using Aula.Core.Abstractions;
using Aula.Core.Logging;
using Aula.Core.Models;
using Aula.Core.Services;
using Microsoft.Extensions.Logging;

namespace Aula.App.Services;

public sealed class KeyboardSession : IDisposable
{
    private readonly KeyboardDeviceFactory _factory = new();
    private readonly ILogger<KeyboardSession> _log = AulaLogging.Logger<KeyboardSession>();
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
                _log.LogWarning("TryOpen returned null (no device found)");
            }
            else
            {
                _log.LogInformation("Connected: {Model} on {Path}", _keyboard.Model.Id, _keyboard.Info.DevicePath);
            }
        }
        catch (Exception ex)
        {
            _keyboard = null;
            Error = ex.GetType().Name + ": " + ex.Message;
            _log.LogError(ex, "Failed to open keyboard");
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
