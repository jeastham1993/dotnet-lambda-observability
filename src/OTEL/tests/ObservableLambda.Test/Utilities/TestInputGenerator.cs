using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.SQSEvents;
using ObservableLambda.Processor.Batch;
using ObservableLambda.Shared.Messaging;
using ObservableLambda.Shared.ViewModel;

namespace ObservableLambda.Test.Utilities;

public static class TestInputGenerator
{
    public static APIGatewayProxyRequest ApiWithEmailAddress => new APIGatewayProxyRequest()
    {
        Body = "{\"emailAddress\": \"test@test.com\"}"
    };

    public static SQSEvent SQSEventWithMultiple => new SQSEvent()
    {
        Records = new List<SQSEvent.SQSMessage>(2)
        {
            new SQSEvent.SQSMessage()
            {
                Body = JsonSerializer.Serialize(new SnsToSqsMessageBody()
                {
                    Message = JsonSerializer.Serialize(new MessageWrapper<UserDTO>(new UserDTO()
                    {
                        EmailAddress = "test@test.com", UserId = Utilities.HashGenerator.Base64Encode("test@test.com")
                    }))
                })
            },
            new SQSEvent.SQSMessage()
            {
                Body = JsonSerializer.Serialize(new SnsToSqsMessageBody()
                {
                    Message = JsonSerializer.Serialize(new MessageWrapper<UserDTO>(new UserDTO()
                    {
                        EmailAddress = "test2@test.com", UserId = Utilities.HashGenerator.Base64Encode("test2@test.com")
                    }))
                })
            }
        }
    };
}