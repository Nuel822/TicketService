using FluentAssertions;
using Moq;
using TicketService.Application.Common.Exceptions;
using TicketService.Application.Common.Interfaces;
using TicketService.Application.Tickets.Queries;
using TicketService.Domain.Entities;

namespace TicketService.UnitTests.Application.Queries;

public class GetSalesReportQueryTests
{
    private readonly Mock<IReportingRepository> _reportingRepositoryMock = new();
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly GetSalesReportQuery _query;

    private readonly Guid _eventId = Guid.NewGuid();

    public GetSalesReportQueryTests()
    {
        _query = new GetSalesReportQuery(
            _reportingRepositoryMock.Object,
            _eventRepositoryMock.Object);
    }

    private Event CreateEvent()
        => Event.Create("Rock Night", "Desc", "O2 Arena",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            new TimeOnly(20, 0), 100);

    private EventSalesSummary CreateSummary(Guid? eventId = null) => new()
    {
        EventId = eventId ?? _eventId,
        EventName = "Rock Night",
        Venue = "O2 Arena",
        EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        EventTime = new TimeOnly(20, 0),
        TotalCapacity = 100,
        TotalTicketsSold = 20,
        AvailableTickets = 80,
        TotalRevenue = 1000m,
        LastUpdatedAt = DateTime.UtcNow,
        TierSummaries = new List<TierSalesSummary>
        {
            new()
            {
                PricingTierId = Guid.NewGuid(),
                EventId = eventId ?? _eventId,
                TierName = "General",
                UnitPrice = 50m,
                TotalQuantity = 100,
                QuantitySold = 20,
                QuantityAvailable = 80,
                Revenue = 1000m,
                LastUpdatedAt = DateTime.UtcNow
            }
        }
    };

    // ── ExecuteAsync (single event) ───────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenEventExistsAndSummaryExists_ShouldReturnReport()
    {
        var summary = CreateSummary();

        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _reportingRepositoryMock
            .Setup(r => r.GetEventSalesSummaryAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var result = await _query.ExecuteAsync(_eventId);

        result.EventId.Should().Be(_eventId);
        result.TotalTicketsSold.Should().Be(20);
        result.TotalRevenue.Should().Be(1000m);
        result.SalesByTier.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventExistsButNoSummary_ShouldReturnZeroedReport()
    {
        var @event = CreateEvent();

        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _reportingRepositoryMock
            .Setup(r => r.GetEventSalesSummaryAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSalesSummary?)null);

        _eventRepositoryMock
            .Setup(r => r.GetByIdAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(@event);

        var result = await _query.ExecuteAsync(_eventId);

        result.TotalTicketsSold.Should().Be(0);
        result.TotalRevenue.Should().Be(0m);
        result.SalesByTier.Should().BeEmpty();
        result.Note.Should().Contain("No tickets have been sold");
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventDoesNotExist_ShouldThrowNotFoundException()
    {
        _eventRepositoryMock
            .Setup(r => r.ExistsAsync(_eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = async () => await _query.ExecuteAsync(_eventId);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Event*not found*");
    }

    // ── ExecuteAllAsync (paginated) ───────────────────────────────────────────

    [Fact]
    public async Task ExecuteAllAsync_ShouldReturnPagedResult()
    {
        // Page 1 of 3: DB returns 10 items, total = 25
        var page1Items = Enumerable.Range(1, 10)
            .Select(_ => CreateSummary(Guid.NewGuid()))
            .ToList<EventSalesSummary>();

        _reportingRepositoryMock
            .Setup(r => r.GetPagedEventSalesSummariesAsync(0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((page1Items, 25));

        var result = await _query.ExecuteAllAsync(page: 1, pageSize: 10);

        result.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAllAsync_WhenOnLastPage_ShouldIndicateNoNextPage()
    {
        // Page 3 of 3: DB returns 5 items (the remainder), total = 25
        var lastPageItems = Enumerable.Range(1, 5)
            .Select(_ => CreateSummary(Guid.NewGuid()))
            .ToList<EventSalesSummary>();

        _reportingRepositoryMock
            .Setup(r => r.GetPagedEventSalesSummariesAsync(20, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((lastPageItems, 25));

        var result = await _query.ExecuteAllAsync(page: 3, pageSize: 10);

        result.Items.Should().HaveCount(5);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAllAsync_WhenPageSizeExceedsMax_ShouldClampTo100()
    {
        // pageSize 999 is clamped to 100; DB is called with take=100
        var items = Enumerable.Range(1, 5)
            .Select(_ => CreateSummary(Guid.NewGuid()))
            .ToList<EventSalesSummary>();

        _reportingRepositoryMock
            .Setup(r => r.GetPagedEventSalesSummariesAsync(0, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 5));

        var result = await _query.ExecuteAllAsync(page: 1, pageSize: 999);

        result.Items.Should().HaveCount(5);
        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAllAsync_WhenPageIsZeroOrNegative_ShouldDefaultToPage1()
    {
        // page 0 is clamped to 1; DB is called with skip=0
        var items = Enumerable.Range(1, 3)
            .Select(_ => CreateSummary(Guid.NewGuid()))
            .ToList<EventSalesSummary>();

        _reportingRepositoryMock
            .Setup(r => r.GetPagedEventSalesSummariesAsync(0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 3));

        var result = await _query.ExecuteAllAsync(page: 0, pageSize: 10);

        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(3);
    }
}


