# Tracing with Open Telemetry

The examples here cover how to trace your serverless applications using Open Telemetry.

![](../../image/sqs-o11y.png)

The sample application takes a user creation request from API Gateway, stores the user in DynamoDB and then publises a message to SNS. From SNS the message is fanned out to two seperate SQS queues.

These SQS queues are configured to trigger two seperate Lambda functions. One Lambda function is publishes a batch of messages to EventBridge, the other takes a single message and uses that to start a user on-boarding workflow built using Step Functions.

This is an interesting use case, as there are two different units of work. When publishing to EventBridge, we want to process the entire batch of messages as an independent trace. Whereas with the Step Functions use case, we want to trace the single message back to the original API request.

## Pre Requisites
- .NET 6
- AWS SAM CLI
- An AWS account
- A valid API Key for Honeycomb

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