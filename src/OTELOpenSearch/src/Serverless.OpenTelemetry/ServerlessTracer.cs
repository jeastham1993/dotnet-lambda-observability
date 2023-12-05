namespace Serverless.OpenTelemetry;

using System.Diagnostics;

public class ServerlessTracer : ITracer
{
    /// <inheritdoc/>
    public Activity StartTrace(
        ActivitySource source,
        string name,
        ActivityContext? parentContext = null)
    {
        if (parentContext.HasValue)
        {
            return source.StartActivity(
                name,
                ActivityKind.Server,
                parentContext.Value);
        }

        return source.StartActivity(
            name,
            ActivityKind.Server);
    }
}