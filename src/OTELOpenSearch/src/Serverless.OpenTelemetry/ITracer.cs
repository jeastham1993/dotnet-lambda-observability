namespace Serverless.OpenTelemetry;

using System.Diagnostics;

public interface ITracer
{
    Activity StartTrace(ActivitySource source, string name, ActivityContext? parentContext = null);
}