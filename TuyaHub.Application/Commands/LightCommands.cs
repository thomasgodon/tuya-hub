using Microsoft.Extensions.Logging;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Application.Commands;

public sealed record SetLightPowerCommand(DeviceName Device, bool On) : IDeviceCommand;

/// <summary>Colour temperature as a KNX percentage (0..100), snapped to the nearest step.</summary>
public sealed record SetLightCctCommand(DeviceName Device, int Percent) : IDeviceCommand;

/// <summary>Relative colour-temperature step (KNX long-press cycle, DPT 3.007). Wraps at the rails.</summary>
public sealed record StepLightCctCommand(DeviceName Device, bool Up) : IDeviceCommand;

public sealed class SetLightPowerHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetLightPowerHandler> logger)
    : DeviceCommandHandler<SetLightPowerCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetLightPowerCommand request)
        => device.SetLightPower(request.On);
}

public sealed class SetLightCctHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetLightCctHandler> logger)
    : DeviceCommandHandler<SetLightCctCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetLightCctCommand request)
        => device.SetLightColourTemperature(ColourTemperature.FromPercent(request.Percent));
}

public sealed class StepLightCctHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<StepLightCctHandler> logger)
    : DeviceCommandHandler<StepLightCctCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, StepLightCctCommand request)
        => device.CycleLightColourTemperature(request.Up);
}
