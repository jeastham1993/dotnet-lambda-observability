# Tracing with Open Telemetry

The examples here cover how to trace your serverless applications using Open Telemetry.

![](../../image/sqs-o11y.png)

The sample application takes a user creation request from API Gateway, stores the user in DynamoDB and then publises a message to SQS.

These SQS queue is configured to trigger a Lambda function. The Lambda function then processes the message, simulating the work using a 5 second delay.

All of the OpenTelemetry setup and processing logic is moved into a `Serverless.OpenTelemetry` package.

## Pre Requisites
- .NET 6
- AWS SAM CLI
- An AWS account
- A configured OpenSearch domain
- A configured OpenSearch ingestion pipeline
  - Details on deploying the [OpenSearch Ingestion Pipeline is available on the AWS docs](https://docs.aws.amazon.com/opensearch-service/latest/developerguide/configure-client-otel.html). This example also includes an example OpenTelemetry collector configuration. This Lambda examples uses SigV4 auth to send trace data direct to the ingestion endpoint.

## Deployment

To deploy this into your own AWS account use the below commands:

``` bash
sam build
sam deploy --guided
```

When running SAM deploy you will need to provide a HONEYCOMB_API_KEY for the traces to be exported.

## Testing

An API endpoint will be output after SAM successfully deploys the application. Make a POST request to the endpoint using the below request body. The email address used can be any valid string.

In the API response you will receive a 'traceparent' property in the body. This contains the trace id that you can then use to search the Honeycomb UI. 