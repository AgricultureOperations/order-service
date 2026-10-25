using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Ports.Driven;
using Infrastructure.Persistence;

namespace Infrastructure.Adapters.Driven;

public class SettingsPersistenceAdapter : ISettingsPersistencePort
{
    private readonly OrderDbContext _orderDbContext;

    public SettingsPersistenceAdapter(OrderDbContext orderDbContext)
    {
        _orderDbContext = orderDbContext;
    }

    public Task<Setting?> Get(string key) => _orderDbContext.Settings.FirstOrDefaultAsync(s => s.Key == key);

    public Task Save(Setting setting) => _orderDbContext.SaveChangesAsync();
}
