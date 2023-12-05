namespace OtelQueueProcessor.Observability;

using OpenTelemetry.Trace;

public interface IApplicationTraceProviders
{
    public TracerProvider SqsTracerProvider { get; set; }
    
    public TracerProvider ApplicationTracerProvider { get; set; }

    public void ForceFlush();
}