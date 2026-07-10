using Microsoft.Extensions.Logging;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Application.Commands;

public sealed record SetLightPowerCommand(DeviceName Device, bool On) : IDeviceCommand;

/// <summary>Brightness as a KNX percentage (0..100). 0 % switches the light off.</summary>
public sealed record SetLightBrightnessCommand(DeviceName Device, int Percent) : IDeviceCommand;

/// <summary>Colour temperature as a KNX percentage (0..100), snapped to the nearest step.</summary>
public sealed record SetLightCctCommand(DeviceName Device, int Percent) : IDeviceCommand;

public sealed class SetLightPowerHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetLightPowerHandler> logger)
    : DeviceCommandHandler<SetLightPowerCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetLightPowerCommand request)
        => device.SetLightPower(request.On);
}

public sealed class SetLightBrightnessHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetLightBrightnessHandler> logger)
    : DeviceCommandHandler<SetLightBrightnessCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetLightBrightnessCommand request)
        => device.SetLightBrightness(Brightness.FromPercent(request.Percent));
}

public sealed class SetLightCctHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetLightCctHandler> logger)
    : DeviceCommandHandler<SetLightCctCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetLightCctCommand request)
        => device.SetLightColourTemperature(ColourTemperature.FromPercent(request.Percent));
}
