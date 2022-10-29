using System.Diagnostics;
using Amazon.Lambda.TestUtilities;
using Amazon.SimpleNotificationService.Model;
using FluentAssertions;
using ObservableLambda.Test.AwsOddTestFramework;
using ObservableLambda.Test.Utilities;

namespace ObservableLambda.Test;

public class TraceTests
{
    [Fact]
    public async Task TestApiAndAsyncActions_ShouldTraceAll()
    {
        var mockAws = new AwsSdkMocks();
        var activities = new List<Activity>();
        var traceOptions = new TestTraceOptions(activities);
        var snsPublisher = new SnsPublishing();
        
        var apiFunction = new ObservableLambda.CreateUser.Function(snsPublisher.MockSnsClient.Object, mockAws.DynamoDb.Object, traceOptions.TraceOptions);

        await apiFunction.TracedFunctionHandler(TestInputGenerator.ApiWithEmailAddress, new TestLambdaContext());
        
        validateApiFunction(activities);

        var asyncInvokes = new List<LambdaInvokeProps>();
        asyncInvokes.Add(new LambdaInvokeProps("ObservableLambda.Processor.Batch::ObservableLambda.Processor.Batch.Function::TracedFunctionHandler", mockAws.EventBridge.Object, traceOptions.TraceOptions));
        asyncInvokes.Add(new LambdaInvokeProps("ObservableLambda.Processor.SingleMessage::ObservableLambda.Processor.SingleMessage.Function::TracedFunctionHandler", mockAws.StepFunctions.Object, traceOptions.TraceOptions));

        var tasks = snsPublisher.ShouldInvokeViaSqs(asyncInvokes, activities);
        Task.WaitAll(tasks);
        
        validateSingleMessageProcessor(activities);
        validateBatchMessageProcessing(activities, snsPublisher.RawMessages);
    }

    private void validateBatchMessageProcessing(List<Activity> activities, List<PublishRequest> publishedMessages)
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