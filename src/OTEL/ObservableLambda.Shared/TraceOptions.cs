using System.Collections.Generic;
using System.Diagnostics;

namespace ObservableLambda.Shared;

public class TraceOptions
{
    public List<Activity> CollectedSpans { get; set; }
    
    public string ServiceName { get; set; }
}