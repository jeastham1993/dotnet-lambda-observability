using System.Collections.Generic;
using System.Diagnostics;

namespace ObservableLambda.Shared;

public class TraceOptions
{
    public TraceOptions()
    {
        
    }

    public TraceOptions(string serviceName)
    {
        this.ServiceName = serviceName;
    }
    
    public List<Activity> CollectedSpans { get; set; }
    
    public string ServiceName { get; set; }

    public bool AddAwsInstrumentation { get; set; } = true;
    
    public bool AddLambdaConfiguration { get; set; } = true;
    
    public bool AutoStartTrace { get; set; } = true;
}