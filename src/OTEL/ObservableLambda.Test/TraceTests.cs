using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using FluentAssertions;
using Moq;
using ObservableLambda.Processor.Batch;
using ObservableLambda.Test.Utilities;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

namespace ObservableLambda.Test;

public class TraceTests
{
    private string batchHandler =
        @"ObservableLambda.Processor.Batch::ObservableLambda.Processor.Batch.Function::TracedFunctionHandler";

    [Fact]
    public async Task InvokeFromHandlerString()
    {
        var activities = new List<Activity>();
            
        var mockEventBridgeClient = new Mock<IAmazonEventBridge>();
        mockEventBridgeClient.Setup(p => p.PutEventsAsync(It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutEventsResponse()
            {
                FailedEntryCount = 0
            });

        var traceOptions = new TraceOptions()
        {
            CollectedSpans = activities,
            ServiceName = "ObservableLambda.UnitTest",
            AddAwsInstrumentation = false,
            AddLambdaConfiguration = false
        };
        
        var components = batchHandler.Split("::");
        var assembly = components[0];
        var classPath = components[1];
        var method = components[2];
            
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        var loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assembly));
        var type = loadedAssembly.GetType(classPath);
        var instance = (Processor.Batch.Function)Activator.CreateInstance(type, flags, null, new object?[2]{mockEventBridgeClient.Object, traceOptions}, null);

        await instance.TracedFunctionHandler(null, new TestLambdaContext());
    }
    
    [Fact]
    public async Task TestTracing()
    {
        var activities = new List<Activity>();
        var publishedMessages = new List<SnsToSqsMessageBody>();

        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        mockSnsClient.Setup(p => p.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((data, token) =>
            {
                publishedMessages.Add(new SnsToSqsMessageBody(){Message = data.Message});
            })
            .ReturnsAsync(new PublishResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                MessageId = "test-message-id",
                SequenceNumber = "001",
                ContentLength = 150,
            });

        var mockDynamoDbClient = new Mock<IAmazonDynamoDB>();
        mockDynamoDbClient.Setup(p => p.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutItemResponse()
            {
                ConsumedCapacity = new ConsumedCapacity() {ReadCapacityUnits = 1, WriteCapacityUnits = 1}
            });

        var mockStepFunctionsClient = new Mock<IAmazonStepFunctions>();
        mockStepFunctionsClient.Setup(p => p.StartExecutionAsync(It.IsAny<StartExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StartExecutionResponse()
            {
                ExecutionArn = "arn::123456",
            });

        var mockEventBridgeClient = new Mock<IAmazonEventBridge>();
        mockEventBridgeClient.Setup(p => p.PutEventsAsync(It.IsAny<PutEventsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutEventsResponse()
            {
                FailedEntryCount = 0
            });

        var traceOptions = new TraceOptions()
        {
            CollectedSpans = activities,
            ServiceName = "ObservableLambda.UnitTest",
            AddAwsInstrumentation = false,
            AddLambdaConfiguration = false
        };
        
        var apiFunction = new ObservableLambda.Function(mockSnsClient.Object, mockDynamoDbClient.Object, traceOptions);
        var singleMessageProcessor = new ObservableLambda.Processor.SingleMessage.Function(mockStepFunctionsClient.Object, traceOptions);
        var batchMessageProcessor = new ObservableLambda.Processor.Batch.Function(mockEventBridgeClient.Object, traceOptions);

        await apiFunction.TracedFunctionHandler(TestInputGenerator.ApiWithEmailAddress, new TestLambdaContext());
        
        validateApiFunction(activities);
        
        var sqsEvent = new SQSEvent() {Records = new List<SQSEvent.SQSMessage>(publishedMessages.Count)};

        foreach (var message in publishedMessages)
        {
            sqsEvent.Records.Add(new SQSEvent.SQSMessage()
            {
                Body = JsonSerializer.Serialize(message)
            });
        }
        
        await singleMessageProcessor.TracedFunctionHandler(sqsEvent, new TestLambdaContext());
        await batchMessageProcessor.TracedFunctionHandler(sqsEvent, new TestLambdaContext());
        
        validateSingleMessageProcessor(activities);
        validateBatchMessageProcessing(activities, publishedMessages);
    }

    private void validateBatchMessageProcessing(List<Activity> activities, List<SnsToSqsMessageBody> publishedMessages)
    {
        var eventBridgeActivity =
            activities.FirstOrDefault(p => (p.DisplayName ?? "").Contains("event-bridge-put-events"));

        eventBridgeActivity.Should().NotBeNull();

        var ebTotalEventsTag = eventBridgeActivity.Tags.First(p => p.Key == "eventbridge.total_events");
        var ebFailedEventsTag = eventBridgeActivity.Tags.First(p => p.Key == "eventbridge.failed_entry_count");

        ebTotalEventsTag.Value.Should().Be(publishedMessages.Count.ToString());
        ebFailedEventsTag.Value.Should().Be("0");
    }

    private void validateSingleMessageProcessor(List<Activity> activities)
    {
        var stepFunctionsActivity = activities.FirstOrDefault(p => (p.DisplayName ?? "").Contains("step-functions-start-workflow"));

        stepFunctionsActivity.Should().NotBeNull();

        var executionArnTag = stepFunctionsActivity.Tags.FirstOrDefault(p => p.Key == "states.execution_arn");
        executionArnTag.Value.Should().Be("arn::123456");
    }

    private void validateApiFunction(List<Activity> activities)
    {
        var snsPublishActivity = activities.FirstOrDefault(p => p.DisplayName.Contains("sns-publish"));
        var dynamoPutItem = activities.FirstOrDefault(p => p.DisplayName.Contains("dynamo-db-put-item"));
        
        Assert.NotNull(snsPublishActivity);
        Assert.NotNull(dynamoPutItem);

        var dynamoReadCapacityTag = dynamoPutItem.Tags.FirstOrDefault(p => p.Key == "dynamodb.read_capacity_units");
        var dynamoWriteCapacityTag = dynamoPutItem.Tags.FirstOrDefault(p => p.Key == "dynamodb.write_capacity_units");
        var dynamoTableNameTag = dynamoPutItem.Tags.FirstOrDefault(p => p.Key == "dynamodb.table_name");
        var dynamoPartition = dynamoPutItem.Tags.FirstOrDefault(p => p.Key == "dynamodb.partition");

        var resultTag = snsPublishActivity.Tags.FirstOrDefault(p => p.Key == "sns.result");

        resultTag.Value.Should().Be("200");
        dynamoReadCapacityTag.Value.Should().Be("1");
        dynamoWriteCapacityTag.Value.Should().Be("1");
        dynamoTableNameTag.Value.Should().Be("ObservableLambda");
        dynamoPartition.Value.Should().Be(Utilities.HashGenerator.Base64Encode("test@test.com"));
    }
}