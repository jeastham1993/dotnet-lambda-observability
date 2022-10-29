# Tracing with Open Telemetry

The examples here cover how to trace your serverless applications using Open Telemetry.

![](../../image/sqs-o11y.png)

The sample application takes a user creation request from API Gateway, stores the user in DynamoDB and then publises a message to SNS. From SNS the message is fanned out to two seperate SQS queues.

These SQS queues are configured to trigger two seperate Lambda functions. One Lambda function is publishes a batch of messages to EventBridge, the other takes a single message and uses that to start a user on-boarding workflow built using Step Functions.

This is an interesting use case, as there are two different units of work. When publishing to EventBridge, we want to process the entire batch of messages as an independent trace. Whereas with the Step Functions use case, we want to trace the single message back to the original API request.

This example also includes tracing the individual steps of your Step Function workflow. An additional Lambda function can be found under the [ObservableLambda.StepFunctionTracer](./src/ObservableLambda.StepFunctionTracer/) folder. This function is triggered based on a Step Functions Execution Status Change event, when the status is 'SUCEEDED'. Once a Step Function successfully completes the function will query Cloud Watch Logs to generate trace data for each individual step.

## Pre Requisites
- .NET 6
- AWS SAM CLI
- An AWS account
- A valid API Key for Honeycomb

## Testing

Unit tests exist under the [tests/ObservableLambda.Test](./tests/ObservableLambda.Test/) folder. The unit tests demonstrate observability driven development (ODD). ODD is a form of test driven development in which all assertions are based on the traces. OTEL on .NET supports exporting trace data to an in-memory collection of spans. This collection of spans can then be queried to ensure functionality is as expected.

The unit test example also includes examples of invoking the queue processing Lambda's using the Handler string and tracing the messages that get passed in. In the example below activities are queried to find all traces with the name 'event-bridge-put-events' and ensure that the total events published on that span match what is expected.

The example uses mock implementations of the AWS SDK, but the unit tests could easily be changed to execute against actual cloud resources.

```c#
var asyncInvokes = new List<LambdaInvokeProps>();
asyncInvokes.Add(new LambdaInvokeProps("ObservableLambda.Processor.Batch::ObservableLambda.Processor.Batch.Function::TracedFunctionHandler", mockAws.EventBridge.Object, traceOptions.TraceOptions));
asyncInvokes.Add(new LambdaInvokeProps("ObservableLambda.Processor.SingleMessage::ObservableLambda.Processor.SingleMessage.Function::TracedFunctionHandler", mockAws.StepFunctions.Object, traceOptions.TraceOptions));

var tasks = snsPublisher.ShouldInvokeViaSqs(asyncInvokes, activities);

Task.WaitAll(tasks);

var eventBridgeActivity = activities.FirstOrDefault(p => (p.DisplayName ?? "").Contains("event-bridge-put-events"));

eventBridgeActivity.Should().NotBeNull();

var ebTotalEventsTag = eventBridgeActivity.Tags.First(p => p.Key == "eventbridge.total_events");
var ebFailedEventsTag = eventBridgeActivity.Tags.First(p => p.Key == "eventbridge.failed_entry_count");

ebTotalEventsTag.Value.Should().Be(publishedMessages.Count.ToString());
ebFailedEventsTag.Value.Should().Be("0");
```

## Deployment

To deploy this into your own AWS account use the below commands:

``` bash
sam build
sam deploy --guided
```

When running SAM deploy you will need to provide a HONEYCOMB_API_KEY for the traces to be exported.

## Testing

An API endpoint will be output after SAM successfully deploys the application. Make a POST request to the endpoint using the below request body. The email address used can be any valid string.

```json
{
  "emailAddress": "test@test.com"
}
```
In the API response you will receive a 'traceparent' header. This contains the trace id that you can then use to search the Honeycomb UI. 