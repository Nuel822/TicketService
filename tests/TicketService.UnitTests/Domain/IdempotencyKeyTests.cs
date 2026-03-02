using FluentAssertions;
using TicketService.Domain.Entities;

namespace TicketService.UnitTests.Domain;

/// <summary>
/// Unit tests for the IdempotencyKey domain entity.
///
/// Focuses on TTL / expiry logic that guards against duplicate request
/// processing.  The IdempotencyStore (infrastructure) relies on IsExpired()
/// to decide whether a cached response is still valid.
/// </summary>
public class IdempotencyKeyTests
{
    private static IdempotencyKey CreateKey(
        string key = "test-key",
        string path = "/api/events/123/tickets",
        int statusCode = 200,
        string body = """{"ticketId":"abc"}""",
        TimeSpan? ttl = null)
        => IdempotencyKey.Create(key, path, statusCode, body, ttl);

    // ── TTL / expiry ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithDefaultTtl_ShouldExpireIn24Hours()
    {
        var before = DateTime.UtcNow;
        var key = CreateKey(); // default TTL = 24 h
        var after = DateTime.UtcNow;

        key.ExpiresAt.Should().BeOnOrAfter(before.AddHours(24))
                              .And.BeOnOrBefore(after.AddHours(24));
    }

    [Fact]
    public void Create_WithCustomTtl_ShouldExpireAfterSpecifiedDuration()
    {
        var ttl = TimeSpan.FromMinutes(30);
        var before = DateTime.UtcNow;
        var key = CreateKey(ttl: ttl);
        var after = DateTime.UtcNow;

        key.ExpiresAt.Should().BeOnOrAfter(before.Add(ttl))
                              .And.BeOnOrBefore(after.Add(ttl));
    }

    [Fact]
    public void IsExpired_WhenKeyIsNew_ShouldReturnFalse()
    {
        var key = CreateKey();

        key.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenTtlIsInThePast_ShouldReturnTrue()
    {
        var key = CreateKey(ttl: TimeSpan.FromMilliseconds(-1));

        key.IsExpired().Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenTtlIsVeryShort_ShouldEventuallyExpire()
    {
        var key = CreateKey(ttl: TimeSpan.FromMilliseconds(1));

        Thread.Sleep(10);

        key.IsExpired().Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenTtlIsLong_ShouldNotBeExpiredYet()
    {
        var key = CreateKey(ttl: TimeSpan.FromDays(365));

        key.IsExpired().Should().BeFalse();
    }
}


