namespace Infrastructure.Persistence;

/// <summary>Maps a client's Idempotency-Key to the order it created, so a retried POST returns that order.</summary>
public class IdempotencyRecord
{
    public string Key { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
