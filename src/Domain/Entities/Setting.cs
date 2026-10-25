namespace Domain.Entities;

/// <summary>A key/value configuration parameter stored in the database and edited from the back office.</summary>
public class Setting
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private Setting() {}

    public Setting(string key, string value)
    {
        Key = key;
        Value = value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string value, Guid? userId)
    {
        Value = value;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = userId;
    }
}

public static class SettingKeys
{
    public const string OrderNumberPrefix = "orders.number.prefix";
}
