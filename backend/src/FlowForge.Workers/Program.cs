using FlowForge.Infrastructure;
using FlowForge.Workers;
using FlowForge.Workers.Consumers;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddInfrastructure(builder.Configuration, x => x.AddConsumer<DeliverNotificationConsumer>());

var host = builder.Build();
host.Run();
