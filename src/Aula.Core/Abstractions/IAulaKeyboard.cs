using Aula.Core.Devices;
using Aula.Core.Models;

namespace Aula.Core.Abstractions;

public interface IAulaKeyboard : IDisposable
{
    DeviceInfo Info { get; }

    ModelConfig Model { get; }

    KeyboardCapabilities Capabilities { get; }

    IKeyboardLayout Layout { get; }

    ILightingController Lighting { get; }
}
