using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShelfLife.Infrastructure.Messaging;
using ShelfLife.Lending.Contracts;
using ShelfLife.Lending.Domain;

namespace ShelfLife.OverdueWorker;

public sealed class OverdueReminderWorker : BackgroundService
{
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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ILoanRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var uow = scope.ServiceProvider.GetRequiredService<ShelfLife.SharedKernel.IUnitOfWork>();

        var overdue = await repo.GetOverdueLoansAsync(ct);
        _logger.LogInformation("Overdue check: {Count} loans found", overdue.Count);

        foreach (var loan in overdue)
        {
            var @event = new LoanOverdueEvent(
                Guid.NewGuid(), DateTimeOffset.UtcNow,
                loan.Id, loan.MemberId, loan.BookTitleId,
                loan.Period.DueDate);

            await publisher.PublishAsync("lending.overdue", @event, ct);
            loan.RecordReminderSent();
        }

        await uow.SaveChangesAsync(ct);
    }
}
