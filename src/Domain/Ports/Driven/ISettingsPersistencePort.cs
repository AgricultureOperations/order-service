using Domain.Entities;

namespace Domain.Ports.Driven;

public interface ISettingsPersistencePort
{
    /// <summary>Loads a setting, tracked for changes. Null when the key was never seeded.</summary>
    Task<Setting?> Get(string key);
    Task Save(Setting setting);
}
