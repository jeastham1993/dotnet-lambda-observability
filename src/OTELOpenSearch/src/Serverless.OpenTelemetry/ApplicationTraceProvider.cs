namespace OtelQueueProcessor.Observability;

using OpenTelemetry.Trace;

public class ApplicationTraceProvider : IApplicationTraceProviders
{
    public ApplicationTraceProvider(TracerProvider applicationTracerProvider, TracerProvider sqsTracerProvider)
    {
        this.ApplicationTracerProvider = applicationTracerProvider;
        this.SqsTracerProvider = sqsTracerProvider;
    }
    
    /// <inheritdoc />
    public TracerProvider SqsTracerProvider { get; set; }

    /// <inheritdoc />
    public TracerProvider ApplicationTracerProvider { get; set; }

    /// <inheritdoc />
    public void ForceFlush()
    {
        this.SqsTracerProvider.ForceFlush();
        this.ApplicationTracerProvider.ForceFlush();
    }
}