namespace Aula.Core.Abstractions;

public interface ISinowealthDiagnostics
{
    byte[] QueryModel();

    byte[] ReadColorProfileRaw();
}
