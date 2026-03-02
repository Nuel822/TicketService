namespace TicketService.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;

    /// <summary>
    /// Optional: stores the Idempotency-Key from the originating HTTP request for traceability.
    /// </summary>
    public string? CorrelationId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    // EF Core requires a parameterless constructor
    private OutboxMessage() { }

    public static OutboxMessage Create(string eventType, string payload, string? correlationId = null)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = payload,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
        RetryCount++;
    }

    public bool IsDeadLettered(int maxRetries = 5) => RetryCount >= maxRetries;
}


