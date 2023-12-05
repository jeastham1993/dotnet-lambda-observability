namespace OtelQueueProcessor;

using System.Text.Json;

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

using AWS.Lambda.Powertools.Logging;

using Messaging.Shared;

using OtelQueueProcessor.Observability;

using Serverless.OpenTelemetry;

public class Function
{
    private readonly IApplicationTraceSources sources;
    private readonly IApplicationTraceProviders tracerProvider;
    private readonly ITracer tracer;

    public Function(
        IApplicationTraceSources sources,
        IApplicationTraceProviders tracerProvider,
        ITracer tracer)
    {
        this.sources = sources;
        this.tracerProvider = tracerProvider;
        this.tracer = tracer;
    }

    [LambdaFunction]
    [Logging(LogEvent = true)]
    public async Task<SQSBatchResponse> QueueProcessor(SQSEvent evt, ILambdaContext lambdaContext)
    {
        try
        {
            var batchItemFailures = new List<SQSBatchResponse.BatchItemFailure>();

            foreach (var message in evt.Records)
            {
                var messageBody = JsonSerializer.Deserialize<MessageWrapper<string>>(message.Body);
                
                var context = message.ParseActivityContext(this.sources.SqsSource, messageBody.Metadata.ParentTrace, messageBody.Metadata.ParentSpan, messageBody.Metadata.MessageDate);

                var rootSpan = this.tracer.StartTrace(
                    this.sources.ApplicationSource,
                    $"{Environment.GetEnvironmentVariable("SERVICE_NAME")}Handler",
                    context);

                rootSpan.AddFunctionDetails(lambdaContext);

                rootSpan.AddTag("messaging.message_id", message.MessageId);

                try
                {
                    Logger.LogInformation($"Processing message {message.MessageId}");
                        
                    Logger.AppendKey(
                        "traceParent",
                        context.TraceId);

                    if (messageBody.Data == "sqserror")
                    {
                        throw new Exception("Simulated failure");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    rootSpan.Stop();
                    
                    this.tracerProvider.ForceFlush();
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failure processing message");

                    rootSpan.AddTag(
                        "error",
                        true);
                    
                    rootSpan.Stop();
                    
                    batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });

                    this.tracerProvider.ForceFlush();
                }
                finally
                {
                    Logger.RemoveKeys("traceParent");
                }
            }

            return new SQSBatchResponse(batchItemFailures);
        }
        catch (Exception e)
        {
            this.tracerProvider.ForceFlush();

            throw;
        }
    }
}