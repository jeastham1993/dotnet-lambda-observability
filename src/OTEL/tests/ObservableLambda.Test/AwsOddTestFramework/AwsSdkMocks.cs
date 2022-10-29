using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Moq;

namespace ObservableLambda.Test.AwsOddTestFramework;

public class AwsSdkMocks
{
    public Mock<IAmazonDynamoDB> DynamoDb { get; set; }
    
    public Mock<IAmazonStepFunctions> StepFunctions { get; set; }
    
    public Mock<IAmazonEventBridge> EventBridge { get; set; }
   
    public AwsSdkMocks()
    {
        DynamoDb = new Mock<IAmazonDynamoDB>();
        DynamoDb.Setup(p => p.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse()
            {
                ConsumedCapacity = new ConsumedCapacity() {ReadCapacityUnits = 1, WriteCapacityUnits = 1}
            });

        StepFunctions = new Mock<IAmazonStepFunctions>();
        StepFunctions.Setup(p => p.StartExecutionAsync(It.IsAny<StartExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StartExecutionResponse()
            {
                ExecutionArn = "arn::123456",
            });

        EventBridge = new Mock<IAmazonEventBridge>();
        EventBridge.Setup(p => p.PutEventsAsync(It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutEventsResponse()
            {
                FailedEntryCount = 0
            });
    }
}