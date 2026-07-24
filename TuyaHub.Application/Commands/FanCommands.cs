using Microsoft.Extensions.Logging;
using TuyaHub.Application.Abstractions;
using TuyaHub.Domain;
using TuyaHub.Domain.ValueObjects;

namespace TuyaHub.Application.Commands;

public sealed record SetFanPowerCommand(DeviceName Device, bool On) : IDeviceCommand;

public sealed record SetFanSpeedCommand(DeviceName Device, int Percent) : IDeviceCommand;

public sealed record SetFanDirectionCommand(DeviceName Device, FanDirection Direction) : IDeviceCommand;

public sealed record SetFanTimerCommand(DeviceName Device, int Minutes) : IDeviceCommand;

public sealed class SetFanPowerHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetFanPowerHandler> logger)
    : DeviceCommandHandler<SetFanPowerCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetFanPowerCommand request)
        => device.SetFanPower(request.On);
}

public sealed class SetFanSpeedHandler(
    IDeviceRegistry registry, IDeviceGateway gateway, ILogger<SetFanSpeedHandler> logger)
    : DeviceCommandHandler<SetFanSpeedCommand>(registry, gateway, logger)
{
    protected override DeviceCommand Apply(Device device, SetFanSpeedCommand request)
        => device.SetFanSpeedPercent(request.Percent);
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
