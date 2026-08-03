using Aula.Core.Models;
using Aula.Core.Services;

namespace Aula.Core.Abstractions;

public interface ILightingController
{
    KeyboardConfig ReadConfig();

    void Apply(LightingConfig config);

    void TurnOff();

    void Reset();

    LedEffect? FindEffect(int id);

    LedEffect? FindEffect(string name);
}
