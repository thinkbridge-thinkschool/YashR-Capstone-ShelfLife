using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Lending.Contracts;
using ShelfLife.Lending.Domain;
using ShelfLife.Lending.Infrastructure;
using System.Diagnostics;
using System.Text.Json;

namespace ShelfLife.OverdueWorker;

public sealed class OverdueReminderWorker : BackgroundService
{
    // Shared ActivitySource — registered in Program.cs so every span is exported.
    internal static readonly ActivitySource ActivitySource =
        new("ShelfLife.OverdueWorker", "1.0.0");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueReminderWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public OverdueReminderWorker(IServiceScopeFactory scopeFactory, ILogger<OverdueReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // Root span for one overdue-check cycle.  The SQL queries fired by
        // GetOverdueLoansAsync and SaveChangesAsync become child spans via
        // SqlClient instrumentation, giving us API → Worker → DB in the trace.
        using var activity = ActivitySource.StartActivity(
            "OverdueWorker.ProcessCycle", ActivityKind.Server);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ILoanRepository>();
        var db   = scope.ServiceProvider.GetRequiredService<LendingDbContext>();
        var uow  = scope.ServiceProvider.GetRequiredService<ShelfLife.SharedKernel.IUnitOfWork>();

        var overdue = await repo.GetOverdueLoansAsync(ct);
        activity?.SetTag("overdue.count", overdue.Count);
        _logger.LogInformation("Overdue check: {Count} loans found", overdue.Count);

        foreach (var loan in overdue)
        {
            var @event = new LoanOverdueEvent(
                Guid.NewGuid(), DateTimeOffset.UtcNow,
                loan.Id, loan.MemberId, loan.BookTitleId,
                loan.Period.DueDate);

            // Write to outbox so OutboxRelayWorker publishes to Service Bus
            // and NotificationDispatchWorker delivers the overdue alert.
            db.OutboxMessages.Add(new OutboxMessage
            {
                TopicName = "shelflife.lending.loan-overdue",
                Type      = nameof(LoanOverdueEvent),
                Payload   = JsonSerializer.Serialize(@event)
            });

            loan.RecordReminderSent();
        }

        await uow.SaveChangesAsync(ct);
    }
}
