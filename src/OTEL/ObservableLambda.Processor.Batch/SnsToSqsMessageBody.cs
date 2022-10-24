using System.Collections.Generic;

namespace ObservableLambda.Processor.Batch;

public record SnsToSqsMessageBody
{
    public string MessageId { get; set; }

    public string TopicArn { get; set; }

    public string Message { get; set; }

    public Dictionary<string, StringMessageAttribute> MessageAttributes { get; set; }
}

public record StringMessageAttribute
{
    public string Type { get; set; }

    public string Value { get; set; }
}