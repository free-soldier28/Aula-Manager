using Aula.Core.Devices;
using Aula.Core.Models;

namespace Aula.Core.Abstractions;

public interface IKeyboardDriver
{
    ModelConfig Model { get; }

    KeyboardCapabilities Capabilities { get; }

    bool Matches(DeviceInfo device);

    IAulaKeyboard Open(DeviceInfo device);
}
