namespace TicketService.Domain.Entities;

public class IdempotencyKey
{
    public string Key { get; private set; } = string.Empty;
    public string RequestPath { get; private set; } = string.Empty;
    public int ResponseStatusCode { get; private set; }
    public string ResponseBody { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    // EF Core requires a parameterless constructor
    private IdempotencyKey() { }

    public static IdempotencyKey Create(
        string key,
        string requestPath,
        int responseStatusCode,
        string responseBody,
        TimeSpan? ttl = null)
    {
        var now = DateTime.UtcNow;
        return new IdempotencyKey
        {
            Key = key,
            RequestPath = requestPath,
            ResponseStatusCode = responseStatusCode,
            ResponseBody = responseBody,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl ?? TimeSpan.FromHours(24))
        };
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
}


