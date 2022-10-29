using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace ObservableLambda.Shared;

public class TraceOptions
{
    private string _serviceName;

    public TraceOptions()
    {
        this._serviceName = Assembly.GetExecutingAssembly().FullName;
    }

    public TraceOptions(string serviceName)
    {
        this.ServiceName = serviceName;
    }
    
    public List<Activity> CollectedSpans { get; init; }

    public string ServiceName
    {
        get => _serviceName;
        init => _serviceName = value ?? Assembly.GetExecutingAssembly().FullName;
    }

    public bool AddAwsInstrumentation { get; init; } = true;
    
    public bool AddLambdaConfiguration { get; init; } = true;
    
    public bool AutoStartTrace { get; set; } = true;
}