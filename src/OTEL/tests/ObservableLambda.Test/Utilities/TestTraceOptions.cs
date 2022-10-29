using System.Diagnostics;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

namespace ObservableLambda.Test.Utilities;

public class TestTraceOptions
{
    public TestTraceOptions(List<Activity> activities)
    {
        TraceOptions = new TraceOptions()
        {
            CollectedSpans = activities,
            ServiceName = "ObservableLambda.UnitTest",
            AddAwsInstrumentation = false,
            AddLambdaConfiguration = false
        };
    }
    
    public TraceOptions TraceOptions { get; private set; }
}