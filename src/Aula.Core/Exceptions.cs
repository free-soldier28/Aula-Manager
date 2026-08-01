namespace Aula.Core;

public class AulaException : Exception
{
    public AulaException(string message) : base(message)
    {
    }

    public AulaException(string message, Exception inner) : base(message, inner)
    {
    }
}

public sealed class AulaDeviceNotFoundException : AulaException
{
    public AulaDeviceNotFoundException()
        : base("No AULA device found. Connect the keyboard via USB cable and check permissions.")
    {
    }
}

public sealed class AulaTransportException : AulaException
{
    public AulaTransportException(string message) : base(message)
    {
    }

    public AulaTransportException(string message, Exception inner) : base(message, inner)
    {
    }
}

public sealed class AulaProtocolException : AulaException
{
    public AulaProtocolException(string message) : base(message)
    {
    }
}
