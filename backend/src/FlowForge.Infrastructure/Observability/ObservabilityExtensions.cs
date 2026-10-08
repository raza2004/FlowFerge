using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace FlowForge.Infrastructure.Observability;

/// <summary>
/// Logging and tracing setup shared by the API and the background worker, so both emit the
/// same shape of data and can be read side by side in one place (Seq in local development).
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Console always; Seq as well when Seq:ServerUrl is set. Seq is optional on purpose: the app
    /// must run fine without it, and the Seq sink buffers and fails quietly if it's unreachable.
    /// </summary>
    public static LoggerConfiguration ConfigureFlowForgeLogging(
        this LoggerConfiguration logger, IConfiguration config, string application)
    {
        logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", application)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

        var seqUrl = config["Seq:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(seqUrl))
            logger.WriteTo.Seq(seqUrl);

        return logger;
    }

    /// <summary>
    /// Distributed tracing over OTLP, only when OpenTelemetry:Endpoint is set (nothing is registered
    /// otherwise, so there's no cost when it's off). MassTransit's activity source is included, which
    /// links an API request to the message it published and the worker that handled it in one trace.
    /// </summary>
    public static IServiceCollection AddFlowForgeTracing(
        this IServiceCollection services, IConfiguration config, string serviceName, Action<TracerProviderBuilder>? configure = null)
    {
        var endpoint = config["OpenTelemetry:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint)) return services;

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddSource("MassTransit");
                configure?.Invoke(tracing);
                tracing.AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(endpoint);
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            });

        return services;
    }
}
