using System.Text.Json.Serialization;

namespace ObservableLambda;

public class StepFunctionStateChangeDetail
{
    [JsonPropertyName("executionArn")]
    public string ExecutionArn { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; }
    
    [JsonPropertyName("input")]
    public string Input { get; set; }
    
    [JsonPropertyName("name")]
    public string ExecutionName { get; set; }
}