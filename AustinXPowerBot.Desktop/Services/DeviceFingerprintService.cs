using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public sealed class DeviceFingerprintService : IDeviceFingerprintService
{
    public DeviceInfoDto GetDeviceInfo()
    {
        var machine = Environment.MachineName;
        var processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "UnknownProcessor";

        var deviceModel = ReadWmi("Win32_ComputerSystem", "Model")
                          ?? ReadWmi("Win32_ComputerSystem", "Manufacturer")
                          ?? machine;
        var boardSerial = ReadWmi("Win32_BaseBoard", "SerialNumber") ?? machine;
        var biosSerial = ReadWmi("Win32_BIOS", "SerialNumber") ?? processor;
        var osVersion = RuntimeInformation.OSDescription;

        var stableRaw = $"{deviceModel}|{boardSerial}|{biosSerial}|{machine}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableRaw));
        var hashHex = Convert.ToHexString(hash);
        var displayId = hashHex.Length >= 12 ? hashHex[..12] : hashHex;

        return new DeviceInfoDto(machine, deviceModel, hashHex, displayId, osVersion);
    }

    private static string? ReadWmi(string className, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {className}");
            foreach (var result in searcher.Get())
            {
                var value = result[property]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        catch
        {
        }

        return null;
    }
}
