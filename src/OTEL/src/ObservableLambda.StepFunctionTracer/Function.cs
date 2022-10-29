using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime;
using Honeycomb.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda.StepFunctionTracer
{
    public class Function
    {
        private AmazonCloudWatchLogsClient _cloudWatchClient;
        private const string SERVICE_NAME = "StepFunctionTracer";
        private TracerProvider _tracerProvider;

        public Function()
        {
            this._cloudWatchClient = new AmazonCloudWatchLogsClient(new EnvironmentVariablesAWSCredentials());

            var resourceBuilder = ResourceBuilder.CreateDefault().AddService(SERVICE_NAME);

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(SERVICE_NAME)
                .AddHoneycomb(new HoneycombOptions
                {
                    ServiceName = SERVICE_NAME,
                    ApiKey = Environment.GetEnvironmentVariable("HONEYCOMB_API_KEY"),
                    EnableLocalVisualizations = true
                })
                .Build();
        }

        public async Task FunctionHandler(CloudWatchEvent<StepFunctionStateChangeDetail> request,
            ILambdaContext context)
        {
            await this.generateStepFunctionTraces(executionArn: request.Detail.ExecutionArn);
        }

        private async Task generateStepFunctionTraces(string executionArn)
        {
            // Delay for 5 to allow CloudWatch logs to catch up.
            await Task.Delay(5);
            
            TimeSpan epochNow = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsToNowSinceEpoch = (int) epochNow.TotalSeconds;

            TimeSpan epochYesterday = DateTime.UtcNow.AddDays(-1) - new DateTime(1970, 1, 1);
            int secondsToYesterdaySinceEpoch = (int) epochYesterday.TotalSeconds;

            var cloudWatchClient = new AmazonCloudWatchLogsClient();
            var startQueryApiResult = await cloudWatchClient.StartQueryAsync(new StartQueryRequest()
            {
                QueryString = string.Format(@"fields @timestamp, @message
| filter execution_arn = '{0}'
| sort @timestamp desc
| limit 20", executionArn),
                LogGroupName = "UserOnBoardingStepFunctionLogGroup",
                StartTime = secondsToYesterdaySinceEpoch,
                EndTime = secondsToNowSinceEpoch
            });


            QueryStatus queryStatus = QueryStatus.Scheduled;
            GetQueryResultsResponse queryResult = null;

            while (queryStatus == QueryStatus.Running || queryStatus == QueryStatus.Scheduled)
            {
                queryResult = await cloudWatchClient.GetQueryResultsAsync(new GetQueryResultsRequest()
                {
                    QueryId = startQueryApiResult.QueryId
                });

                queryStatus = queryResult.Status;

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            ActivitySource source = new ActivitySource(SERVICE_NAME);

            var stepFunctionLogs = new List<StepFunctionLogStructure>();

            foreach (var result in queryResult.Results)
            {
                var messageField = result.FirstOrDefault(p => p.Field == "@message");
                var stepFunctionLog = JsonSerializer.Deserialize<StepFunctionLogStructure>(messageField.Value);
                stepFunctionLogs.Add(stepFunctionLog);
            }

            var startStepFunctionLog = stepFunctionLogs.FirstOrDefault(p => p.Type == "ExecutionStarted");
            var endStepFunctionLog = stepFunctionLogs.FirstOrDefault(p => p.Type == "ExecutionSucceeded");

            var details = JsonSerializer.Deserialize<StepFunctionDetailStructure>(startStepFunctionLog.Details.Input);

            Console.WriteLine($"Trace parent: {details.Metadata.TraceParent}");
            Console.WriteLine($"Parent span: {details.Metadata.ParentSpan}");

            var context = new SpanContext(ActivityTraceId.CreateFromString(details.Metadata.TraceParent.AsSpan()),
                ActivitySpanId.CreateFromString(details.Metadata.ParentSpan.AsSpan()), ActivityTraceFlags.Recorded,
                true);

            var tracer = _tracerProvider.GetTracer(SERVICE_NAME);
            var rootSpan = tracer.StartActiveSpan("ExecutionStarted", SpanKind.Server, parentContext: context,
                startTime: DateTimeOffset.FromUnixTimeMilliseconds(startStepFunctionLog.EventTimestampAsLong));

            var enteredLogs = stepFunctionLogs.Where(p => p.Type.Contains("Entered")).ToList();
            Console.WriteLine($"Entered logs {enteredLogs.Count}");

            foreach (var logRecord in enteredLogs)
            {
                var endingLogRecord =
                    stepFunctionLogs.FirstOrDefault(p => p.Type.StartsWith(logRecord.Type.Replace("Entered", "")));

                Console.WriteLine($"Processing {logRecord.Type} with next record as {endingLogRecord.Type}");

                var stepSpan = tracer.StartSpan(logRecord.Type.Replace("Entered", ""), SpanKind.Server, context,
                    startTime: DateTimeOffset.FromUnixTimeMilliseconds(logRecord.EventTimestampAsLong));
                stepSpan.End(DateTimeOffset.FromUnixTimeMilliseconds(endingLogRecord.EventTimestampAsLong));
            }

            rootSpan.End(DateTimeOffset.FromUnixTimeMilliseconds(endStepFunctionLog.EventTimestampAsLong));
            ;

            Console.WriteLine(
                $"Start time is {DateTimeOffset.FromUnixTimeMilliseconds(startStepFunctionLog.EventTimestampAsLong).DateTime}");
            Console.WriteLine(
                $"End time is {DateTimeOffset.FromUnixTimeMilliseconds(endStepFunctionLog.EventTimestampAsLong).DateTime}");

            _tracerProvider.ForceFlush();
        }
    }
}