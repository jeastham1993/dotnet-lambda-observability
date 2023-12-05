namespace Messaging.Shared;

using System.Diagnostics;

public class MessageWrapper<T>
{
    public MessageWrapper(){}

    public MessageWrapper(string messageType, string versionNumber, T data)
    {
        this.Metadata = new Metadata()
        {
            MessageType = messageType,
            VersionNumber = versionNumber
        };
        this.Data = data;
    }

    public Metadata Metadata { get; set; }
    
    public T Data { get; set; }
}

public class Metadata
{
    public string ParentTrace { get; set; } = Activity.Current?.TraceId.ToString();
    
    public string ParentSpan { get; set; } = Activity.Current?.SpanId.ToString();

    public string EventId { get; set; } = Guid.NewGuid().ToString();

    public DateTime MessageDate { get; set; } = DateTime.Now;
    
    public string VersionNumber { get; set; }

    public string MessageType { get; set; }
}