using Serilog;
using ShelfLife.Lending.Infrastructure;
using ShelfLife.OverdueWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg.WriteTo.Console());
builder.Services.AddLendingModule(builder.Configuration);
builder.Services.AddHostedService<OverdueReminderWorker>();

var host = builder.Build();
host.Run();
