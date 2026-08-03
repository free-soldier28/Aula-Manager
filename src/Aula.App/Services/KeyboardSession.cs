using Aula.Core;
using Aula.Core.Abstractions;
using Aula.Core.Devices;
using Aula.Core.Logging;
using Aula.Core.Models;
using Aula.Core.Services;
using Microsoft.Extensions.Logging;

namespace Aula.App.Services;

public sealed class KeyboardSession : IDisposable
{
    private readonly KeyboardDeviceFactory _factory = new();
    private readonly IHidDeviceListWatcher _watcher;
    private readonly ILogger<KeyboardSession> _log = AulaLogging.Logger<KeyboardSession>();
    private IAulaKeyboard? _keyboard;
    private readonly object _gate = new();

    public event Action? Changed;

    public IAulaKeyboard? Current => _keyboard;

    public bool IsConnected => _keyboard is not null;

    public string? Error { get; private set; }

    public KeyboardSession(IHidDeviceListWatcher? watcher = null)
    {
        _watcher = watcher ?? new HidSharpDeviceListWatcher();
        _watcher.DeviceListChanged += OnDeviceListChanged;
        _watcher.Start();
    }

    public void Open(string? modelId = null)
    {
        lock (_gate)
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
    }

    public void Refresh(string? modelId = null)
    {
        lock (_gate)
        {
            _keyboard?.Dispose();
            _keyboard = null;
            Open(modelId);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _watcher.DeviceListChanged -= OnDeviceListChanged;
            _watcher.Dispose();
            _keyboard?.Dispose();
            _keyboard = null;
        }
    }

    private void OnDeviceListChanged(object? sender, EventArgs e)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            HandleDeviceListChanged();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(HandleDeviceListChanged);
        }
    }

    private void HandleDeviceListChanged()
    {
        IReadOnlyList<DeviceInfo> present;
        try
        {
            present = _factory.PresentDevices();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Device list changed but scanning failed");
            return;
        }

        ReconnectAction action = ReconnectPlanner.Decide(present, _keyboard?.Info.DevicePath);
        switch (action)
        {
            case ReconnectAction.Open:
                _log.LogInformation("AULA device appeared; (re)connecting");
                Error = null;
                _keyboard?.Dispose();
                _keyboard = _factory.TryOpen();
                if (_keyboard is null)
                {
                    Error = "TryOpen returned null (no device found).";
                }
                else
                {
                    _log.LogInformation("Connected: {Model} on {Path}", _keyboard.Model.Id, _keyboard.Info.DevicePath);
                }

                Changed?.Invoke();
                break;

            case ReconnectAction.Release:
                _log.LogInformation("Connected device disappeared; releasing");
                _keyboard?.Dispose();
                _keyboard = null;
                Error = "AULA keyboard disconnected.";
                Changed?.Invoke();
                break;

            case ReconnectAction.Keep:
                break;
        }
    }
}
