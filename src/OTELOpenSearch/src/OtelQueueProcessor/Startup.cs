namespace OtelQueueProcessor;

using Amazon.DynamoDBv2;

using Microsoft.Extensions.DependencyInjection;

using Serverless.OpenTelemetry;

[Amazon.Lambda.Annotations.LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddServerlessOpenTelemetry(() => new TraceOptions(Environment.GetEnvironmentVariable("OPEN_SEARCH_ENDPOINT"), true))
            .AddSingleton(new AmazonDynamoDBClient());
    }
}