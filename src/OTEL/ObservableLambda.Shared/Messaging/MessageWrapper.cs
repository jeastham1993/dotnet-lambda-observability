using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObservableLambda.Shared.Messaging;

public class MessageWrapper<T>
{
    public MessageWrapper()
    {
        this.Metadata = new MessageMetadata();
    }

    public MessageWrapper(T payload)
    {
        this.Metadata = new MessageMetadata();
        this.Payload = payload;
    }
    
    public MessageMetadata Metadata { get; set; }
    
    public T Payload { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}

public record MessageMetadata
{
    public MessageMetadata()
    {
        if (Activity.Current != null)
        {
            this.TraceParent = Activity.Current.TraceId.ToString();
            this.ParentSpan = Activity.Current.SpanId.ToString();   
        }
    }
    
    [JsonPropertyName("traceparent")]
    public string TraceParent { get; set; }
    
    [JsonPropertyName("parentspan")]
    public string ParentSpan { get; set; }
}