using Application.DTOs;
using Domain.Entities;
using Domain.Ports.Driven;

namespace Application.UseCases;

public class GetOrderSettingsUseCase : IGetOrderSettingsUseCase
{
    private readonly ISettingsPersistencePort _settings;
    private readonly IOrderNumberGeneratorPort _numbers;
    private readonly TimeProvider _clock;

    public GetOrderSettingsUseCase(ISettingsPersistencePort settings, IOrderNumberGeneratorPort numbers, TimeProvider clock)
    {
        _settings = settings;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<OrderSettingsResponse> Execute()
    {
        var setting = await _settings.Get(SettingKeys.OrderNumberPrefix)
            ?? throw new KeyNotFoundException("Order number prefix setting is missing");
        return await OrderSettingsMapper.ToResponse(setting, _numbers, _clock);
    }
}

public class UpdateOrderSettingsUseCase : IUpdateOrderSettingsUseCase
{
    private readonly ISettingsPersistencePort _settings;
    private readonly IOrderNumberGeneratorPort _numbers;
    private readonly TimeProvider _clock;

    public UpdateOrderSettingsUseCase(ISettingsPersistencePort settings, IOrderNumberGeneratorPort numbers, TimeProvider clock)
    {
        _settings = settings;
        _numbers = numbers;
        _clock = clock;
    }

    /// <summary>Changing the prefix only affects orders created afterwards; existing numbers never change.
    /// Each prefix has its own yearly counter, so switching back to an earlier prefix continues where it stopped.</summary>
    public async Task<OrderSettingsResponse> Execute(UpdateOrderSettingsRequest request, Guid? userId)
    {
        var prefix = OrderNumberPrefix.Normalize(request.OrderNumberPrefix);
        var setting = await _settings.Get(SettingKeys.OrderNumberPrefix)
            ?? throw new KeyNotFoundException("Order number prefix setting is missing");
        setting.Update(prefix, userId);
        await _settings.Save(setting);
        return await OrderSettingsMapper.ToResponse(setting, _numbers, _clock);
    }
}

internal static class OrderSettingsMapper
{
    public static async Task<OrderSettingsResponse> ToResponse(Setting setting, IOrderNumberGeneratorPort numbers, TimeProvider clock) => new(
        setting.Value,
        await numbers.Peek(setting.Value, clock.GetUtcNow().UtcDateTime.Year),
        setting.UpdatedAt,
        setting.UpdatedBy);
}
