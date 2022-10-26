using System.Text.Json.Serialization;

namespace ObservableLambda.StepFunctionTracer;

public class StepFunctionLogStructure
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("type")]
    public string Type { get; set; }
    
    [JsonPropertyName("details")]
    public StepFunctionLogDetails Details { get; set; }
    
    [JsonPropertyName("event_timestamp")]
    public string EventTimestamp { get; set; }

    public long EventTimestampAsLong
    {
        get
        {
            return long.Parse(this.EventTimestamp);
        }
    }
}

public record StepFunctionLogDetails
{
    [JsonPropertyName("input")]
    public string Input { get; set; }
}