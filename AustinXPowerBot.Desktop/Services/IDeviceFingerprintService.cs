using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public interface IDeviceFingerprintService
{
    DeviceInfoDto GetDeviceInfo();
}
