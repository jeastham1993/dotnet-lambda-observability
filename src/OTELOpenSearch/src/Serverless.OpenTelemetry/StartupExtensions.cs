namespace Serverless.OpenTelemetry;

using System.Diagnostics;

using global::OpenTelemetry;
using global::OpenTelemetry.Exporter;
using global::OpenTelemetry.Resources;
using global::OpenTelemetry.Trace;

using Microsoft.Extensions.DependencyInjection;

using OtelQueueProcessor.Observability;

public static class StartupExtensions
{
    public static IServiceCollection AddServerlessOpenTelemetry(this IServiceCollection services, Func<TraceOptions> opt)
    {
        var options = opt.Invoke();
        
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var traceConfig = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(
                opt =>
                {
                    opt.AddService(Environment.GetEnvironmentVariable("SERVICE_NAME"));
                })
            .SetErrorStatusOnException()
            .AddSource(Environment.GetEnvironmentVariable("SERVICE_NAME"))
            .AddAWSInstrumentation();

        var sqsTraceConfig = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(
                opt =>
                {
                    opt.AddService("SQS");
                })
            .AddSource("SQS");

        if (!string.IsNullOrEmpty(options.OtlpExportEndpoint))
        {
            var otlpExportOptions = new OtlpExporterOptions()
            {
                Endpoint = new Uri(options.OtlpExportEndpoint),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                ExportProcessorType = ExportProcessorType.Simple,
                HttpClientFactory = options.SigV4SignExport
                    ? (
                        () => new HttpClient(
                            new SignedRequestHandler()))
                    : null
            };
            
            traceConfig
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = otlpExportOptions.Endpoint;
                    opt.Protocol = otlpExportOptions.Protocol;
                    opt.HttpClientFactory = otlpExportOptions.HttpClientFactory;
                });
            
            sqsTraceConfig
                .AddOtlpExporter(
                    opt =>
                    {
                        opt.Endpoint = otlpExportOptions.Endpoint;
                        opt.Protocol = otlpExportOptions.Protocol;
                        opt.HttpClientFactory = otlpExportOptions.HttpClientFactory;
                    });
        }

        services.AddSingleton<IApplicationTraceProviders>(
            new ApplicationTraceProvider(
                traceConfig.Build(),
                sqsTraceConfig.Build()));

        services.AddSingleton<IApplicationTraceSources>(
            new ApplicationTraceSources(
                new ActivitySource(Environment.GetEnvironmentVariable("SERVICE_NAME")),
                new ActivitySource("SQS")));

        services.AddSingleton<ITracer, ServerlessTracer>();

        return services;
    }
}