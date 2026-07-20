using Microsoft.Extensions.Logging;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Application.Commands;

public sealed record SetFanPowerCommand(DeviceName Device, bool On) : IDeviceCommand;

public sealed record StepFanSpeedCommand(DeviceName Device, bool Up) : IDeviceCommand;

public sealed record SetFanDirectionCommand(DeviceName Device, FanDirection Direction) : IDeviceCommand;

public sealed record SetFanTimerCommand(DeviceName Device, int Minutes) : IDeviceCommand;

public sealed record SetFanBeepCommand(DeviceName Device, bool On) : IDeviceCommand;

public sealed class SetFanPowerHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetFanPowerHandler> logger)
    : DeviceCommandHandler<SetFanPowerCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetFanPowerCommand request)
        => device.SetFanPower(request.On);
}

public sealed class StepFanSpeedHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<StepFanSpeedHandler> logger)
    : DeviceCommandHandler<StepFanSpeedCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, StepFanSpeedCommand request)
        => device.StepFanSpeed(request.Up);
}

public sealed class SetFanDirectionHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetFanDirectionHandler> logger)
    : DeviceCommandHandler<SetFanDirectionCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetFanDirectionCommand request)
        => device.SetFanDirection(request.Direction);
}

public sealed class SetFanTimerHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetFanTimerHandler> logger)
    : DeviceCommandHandler<SetFanTimerCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetFanTimerCommand request)
        => device.SetFanTimer(CountdownTimer.FromMinutes(request.Minutes));
}

public sealed class SetFanBeepHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetFanBeepHandler> logger)
    : DeviceCommandHandler<SetFanBeepCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetFanBeepCommand request)
        => device.SetFanBeep(request.On);
}
