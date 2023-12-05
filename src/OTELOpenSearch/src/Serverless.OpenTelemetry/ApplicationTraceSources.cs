namespace Serverless.OpenTelemetry;

using System.Diagnostics;

using OtelQueueProcessor.Observability;

public class ApplicationTraceSources : IApplicationTraceSources
{
    public ApplicationTraceSources(ActivitySource applicationSource, ActivitySource sqsSource)
    {
        this.ApplicationSource = applicationSource;
        this.SqsSource = sqsSource;
    }

    /// <inheritdoc />
    public ActivitySource ApplicationSource { get; private set; }

    /// <inheritdoc />
    public ActivitySource SqsSource { get; private set; }
}