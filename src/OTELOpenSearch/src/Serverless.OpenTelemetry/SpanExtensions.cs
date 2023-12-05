namespace OtelQueueProcessor.Observability;

using System.Diagnostics;

using Amazon.Lambda.Core;

public static class SpanExtensions
{
    public static void AddHttpTags(this Activity activity, string httpMethod, string path)
    {
        activity.AddTag("http.method", "GET");
        activity.AddTag("http.path", "/{input}");
    }
    
    public static Activity AddFunctionDetails(this Activity activity, ILambdaContext context)
    {
        activity.AddTag(
            "aws.lambda.invoked_arn",
            context.InvokedFunctionArn);
        activity.AddTag(
            "faas.id",
            context.InvokedFunctionArn);
        activity.AddTag(
            "faas.execution",
            context.AwsRequestId);
        activity.AddTag(
            "cloud.account.id",
            context.InvokedFunctionArn?.Split(":")[4]);
        activity.AddTag(
            "cloud.region",
            Environment.GetEnvironmentVariable("AWS_REGION"));
        activity.AddTag(
            "cloud.provider",
            "aws");
        activity.AddTag(
            "faas.name",
            context.FunctionName);
        activity.AddTag(
            "faas.version",
            context.FunctionVersion);

        return activity;
    }
}