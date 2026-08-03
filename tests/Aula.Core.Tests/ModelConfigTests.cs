using Aula.Core.Abstractions;
using Aula.Core.Models;

namespace Aula.Core.Tests;

public class ModelConfigTests
{
    [Fact]
    public void F75_BindsF75Layout()
    {
        Assert.Same(F75Layout.Instance, ModelConfig.F75.Layout);
        Assert.Equal(126, ModelConfig.F75.Layout.LedCount);
    }

    [Fact]
    public void F87_ReusesF75Layout()
    {
        Assert.Same(F75Layout.Instance, ModelConfig.F87.Layout);
    }

    [Fact]
    public void Resolve_UnknownId_FallsBackToF75()
    {
        Assert.Same(ModelConfig.F75, ModelConfig.Resolve(null));
        Assert.Same(ModelConfig.F75, ModelConfig.Resolve("nope"));
    }
}
