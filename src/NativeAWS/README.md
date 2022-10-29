# Native AWS tooling

Observability is an important part of serverless applications. As the levels of decomposition get smaller tracing and structured logging become all the more important.

## Tracing

When using AWS SAM tracing is configured by enabling two properties, one against your AWS Lambda function and one against API Gateway. Tracing is configured as Active against the function, and enabled on the API if using API Gateway.

``` yaml
Globals:
  Function:
    Timeout: 10
    Tracing: Active
    MemorySize: 512
  Api:
    TracingEnabled: True
```

Within your function code, [auto-instrumentation can be added using the AWS X-Ray SDK](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet.html). As an example, to configure instrumentation of all AWS SDK calls one line of code is required. A caveat, this line of code needs to execute before the SDK client is initialized.

```c#
public Function() : base() {
    AWSSDKHandler.RegisterXRayForAllServices();
}
```

When using AWS X-Ray with API Gateway the trace id will be returned in the AMZN_X_TRACE_ID property of the response headers.

## Logging

Structured logs are a vital component of logging when using AWS Lambda. Structured logs give a defined format to all log messages, making them queryable. When your log messages may be spread across multiple log groups being able to search on a common identifier is important.

In this example, [Serilog](https://serilog.net/) is used to add logging. All log implementations are abstracted into a static Logging class.

```c#
public static class Logging
{
    static string _traceId;

    public static void Init()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateLogger();
    }

    public static void AddTraceToLogContext(string traceId) => _traceId = traceId;

    public static void Info(string message)
    {
        Log.ForContext("TraceId", _traceId).Information(message);
    }
}
```

One of the benefits of using Lambda is with that guarantee that each execution environment is guaranteed to only process one event at any one time. This opens up the possibility to use static variables to store constant information.

```c#
public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
{
    Logging.AddTraceToLogContext(AWSXRayRecorder.Instance.GetEntity().TraceId);
}
```

The opening line of our function handler adds the current TraceId to our Logging object which then adds that TraceId to all of our log messages. This allows the trace id to be used as a searchable property across all of our function executions.

