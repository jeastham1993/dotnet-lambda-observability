namespace Serverless.OpenTelemetry;

using System.Diagnostics;

public interface IApplicationTraceSources
{
    public ActivitySource ApplicationSource { get; }
    
    public ActivitySource SqsSource { get; }
}