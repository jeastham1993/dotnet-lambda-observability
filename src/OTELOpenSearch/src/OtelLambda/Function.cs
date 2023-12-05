namespace OtelLambda;

using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Amazon.SQS;

using AWS.Lambda.Powertools.Logging;

using Messaging.Shared;

using OtelQueueProcessor.Observability;

using Serverless.OpenTelemetry;

public class Function
{
    private readonly AmazonDynamoDBClient dynamoDbClient;
    private readonly AmazonSQSClient sqsClient;
    private readonly IApplicationTraceSources sources;
    private readonly IApplicationTraceProviders traceProvider;
    private readonly ITracer tracer;

    public Function(
        AmazonDynamoDBClient dynamoDbClient,
        AmazonSQSClient sqsClient,
        IApplicationTraceProviders traceProvider,
        IApplicationTraceSources sources,
        ITracer tracer)
    {
        this.dynamoDbClient = dynamoDbClient;
        this.sqsClient = sqsClient;
        this.traceProvider = traceProvider;
        this.sources = sources;
        this.tracer = tracer;
    }

    [LambdaFunction]
    [RestApi(
        LambdaHttpMethod.Get,
        "/{input}")]
    public async Task<IHttpResult> FunctionHandler(string input, ILambdaContext context)
    {
        using var rootSpan = this.tracer.StartTrace(
            this.sources.ApplicationSource,
            $"{Environment.GetEnvironmentVariable("SERVICE_NAME")}Handler");

        rootSpan.AddHttpTags(
            "GET",
            "/{input}");
        rootSpan.AddFunctionDetails(context);

        IHttpResult result = null;

        try
        {
            if (input == "error")
            {
                throw new Exception("Custom error to demonstrate failure handling");
            }

            var getResult = await this.dynamoDbClient.GetItemAsync(
                Environment.GetEnvironmentVariable("TABLE_NAME"),
                new Dictionary<string, AttributeValue>(1)
                {
                    { "id", new AttributeValue(input) }
                });

            rootSpan.AddTag(
                "http.statusCode",
                getResult.IsItemSet ? 200 : 404);

            result = getResult.IsItemSet
                ? HttpResults.Ok(
                    new ApiResponse(
                        rootSpan.TraceId.ToString(),
                        getResult.Item["value"].S))
                : HttpResults.NotFound(
                    new ApiResponse(
                        rootSpan.TraceId.ToString(),
                        null));
        }
        catch (Exception e)
        {
            rootSpan.AddTag(
                "http.statusCode",
                500);

            Logger.LogError(
                e,
                "Failure processing GET request");

            result = HttpResults.InternalServerError(
                new ApiResponse(
                    rootSpan.TraceId.ToString(),
                    null));
        }

        rootSpan.Stop();

        this.traceProvider.ForceFlush();

        return result;
    }

    [LambdaFunction]
    [RestApi(
        LambdaHttpMethod.Post,
        "/{input}/{value}")]
    public async Task<IHttpResult> PostHandler(
        string input,
        string value,
        ILambdaContext context)
    {
        using var rootSpan = this.tracer.StartTrace(
            this.sources.ApplicationSource,
            $"{Environment.GetEnvironmentVariable("SERVICE_NAME")}Handler");

        rootSpan.AddHttpTags("POST",
            "/{input}/{value}");
        rootSpan.AddFunctionDetails(context);

        IHttpResult result = null;

        try
        {
            await this.dynamoDbClient.PutItemAsync(
                Environment.GetEnvironmentVariable("TABLE_NAME"),
                new Dictionary<string, AttributeValue>(2)
                {
                    { "id", new AttributeValue(input) },
                    { "value", new AttributeValue(value) }
                });

            await this.sqsClient.SendMessageAsync(
                Environment.GetEnvironmentVariable("QUEUE_URL"),
                JsonSerializer.Serialize(
                    new MessageWrapper<string>(
                        "Order",
                        "v1",
                        value)));

            result = HttpResults.Ok(
                new ApiResponse(
                    rootSpan.TraceId.ToString(),
                    input.ToUpper()));
        }
        catch (Exception e)
        {
            Logger.LogError(
                e,
                "Failure processing POST request");

            result = HttpResults.InternalServerError(
                new ApiResponse(
                    rootSpan.TraceId.ToString(),
                    null));
        }
        
        rootSpan.Stop();

        this.traceProvider.ForceFlush();

        return result;
    }
}