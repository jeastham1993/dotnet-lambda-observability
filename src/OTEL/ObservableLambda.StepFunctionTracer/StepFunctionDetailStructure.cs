using System.Text.Json.Serialization;

namespace ObservableLambda.StepFunctionTracer;

public class StepFunctionDetailStructure
{
    public MessageMetadata Metadata { get; set; }
}

public record MessageMetadata
{
    [JsonPropertyName("traceparent")]
    public string TraceParent { get; set; }
    
    [JsonPropertyName("parentspan")]
    public string ParentSpan { get; set; }
}