namespace Aula.Core.Devices;

public sealed record DeviceInfo(
    string DevicePath,
    int VendorId,
    int ProductId,
    string? SerialNumber,
    string? ProductName,
    int MaxFeatureReportLength = 0)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(ProductName) ? $"AULA ({VendorId:X4}:{ProductId:X4})" : ProductName;
}
