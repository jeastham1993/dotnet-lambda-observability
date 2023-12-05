namespace OtelQueueProcessor.Observability;

using System.Diagnostics;

using Amazon.Lambda.SQSEvents;

public static class MessageExtensions
{
    public static ActivityContext ParseActivityContext(this SQSEvent.SQSMessage message, ActivitySource sqsSource, string traceId, string spanId, DateTime publishDate)
    {
        var hydratedContext = new ActivityContext(ActivityTraceId.CreateFromString(traceId),
            ActivitySpanId.CreateFromString(spanId), ActivityTraceFlags.Recorded);

        var sqsSpan = sqsSource.StartActivity(
            "SQS",
            ActivityKind.Server,
            parentContext: hydratedContext,
            startTime: publishDate);
        sqsSpan.Stop();
        
        var sqsContext = new ActivityContext(sqsSpan.TraceId,
            sqsSpan.SpanId, ActivityTraceFlags.Recorded);

        return sqsContext;
    }
}